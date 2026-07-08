using nettest.Models;
using nettest.Services;

namespace nettest.Tests;

public class InviteServiceTests
{
    [Fact]
    public async Task CreateInviteAsync_sends_invite_email_when_recipient_is_provided()
    {
        await using var db = TestHelpers.CreateDbContext();
        var unit = new Unit
        {
            Id = 10,
            UnitNumber = 101,
            Property = new Property
            {
                Id = 20,
                Name = "Main Street",
                Address = "123 Main Street",
                Landlord = new User
                {
                    Id = 30,
                    Email = "landlord@example.com",
                    PasswordHash = "hash",
                    Role = "Landlord"
                }
            }
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        var emailSender = new CapturingInviteEmailSender();
        var service = new InviteService(db, emailSender);

        var invite = await service.CreateInviteAsync(unit.Id, "tenant@example.com");

        Assert.Equal("tenant@example.com", invite.SentToEmail);
        Assert.NotNull(emailSender.SentInvite);
        Assert.Equal(invite.Id, emailSender.SentInvite.Id);
        Assert.Equal(invite.Code, emailSender.SentInvite.Code);
    }

    private sealed class CapturingInviteEmailSender : IInviteEmailSender
    {
        public Invite? SentInvite { get; private set; }

        public Task SendInviteAsync(Invite invite, CancellationToken cancellationToken = default)
        {
            SentInvite = invite;
            return Task.CompletedTask;
        }
    }
}
