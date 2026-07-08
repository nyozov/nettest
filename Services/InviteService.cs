namespace nettest.Services;

using Microsoft.EntityFrameworkCore;
using nettest.Data;
using nettest.Models;
using System.Security.Cryptography;

public class InviteService(AppDbContext db, IInviteEmailSender inviteEmailSender)
{
    // Excludes 0/O, 1/I/L and similar look-alikes so codes are easy to read off a printed letter
    private const string CodeChars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public async Task<Invite> CreateInviteAsync(int unitId, string? sentToEmail = null, int maxUses = 1)
    {
        var unit = await db.Units.FindAsync(unitId)
            ?? throw new ArgumentException($"Unit {unitId} not found");

        var invite = new Invite
        {
            UnitId = unit.Id,
            Code = await GenerateUniqueCodeAsync(),
            SentToEmail = sentToEmail,
            MaxUses = maxUses
        };

        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        await inviteEmailSender.SendInviteAsync(invite);

        return invite;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateCode();
            var exists = await db.Invites.AnyAsync(i => i.Code == code);
            if (!exists) return code;
        }

        throw new InvalidOperationException("Could not generate a unique invite code after 10 attempts");
    }

    private static string GenerateCode()
    {
        // Format: XXXX-XXXX (8 chars, easy to split visually and read aloud)
        Span<char> buffer = stackalloc char[9];
        for (var i = 0; i < 9; i++)
        {
            buffer[i] = i == 4 ? '-' : CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)];
        }
        return new string(buffer);
    }
}
