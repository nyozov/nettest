using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using nettest.Controllers;
using nettest.Models;
using nettest.Services;

namespace nettest.Tests;

public class InvitesControllerTests
{
    [Fact]
    public async Task RedeemInvite_creates_tenant_assigns_unit_and_returns_token()
    {
        await using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db);
        var invite = new Invite
        {
            Code = "ABCD-2345",
            UnitId = unit.Id,
            SentToEmail = "tenant@example.com",
            MaxUses = 1
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.RedeemInvite(
            invite.Code,
            new InvitesController.RedeemInviteRequest
            {
                Email = "tenant@example.com",
                Password = "password123"
            });

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value) as string;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var user = Assert.Single(db.Users.Where(user => user.Role == "Tenant"));

        Assert.Equal(unit.Id, user.UnitId);
        Assert.Equal("tenant@example.com", jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Tenant", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(InviteStatus.Redeemed, invite.Status);
        Assert.Equal(1, invite.UsesCount);
        Assert.NotNull(invite.RedeemedAt);
    }

    [Fact]
    public async Task RedeemInvite_rejects_different_email_when_invite_was_sent_to_specific_address()
    {
        await using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db);
        var invite = new Invite
        {
            Code = "WXYZ-7890",
            UnitId = unit.Id,
            SentToEmail = "tenant@example.com"
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.RedeemInvite(
            invite.Code,
            new InvitesController.RedeemInviteRequest
            {
                Email = "other@example.com",
                Password = "password123"
            });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.Users.Where(user => user.Role == "Tenant"));
        Assert.Equal(0, invite.UsesCount);
    }

    private static InvitesController CreateController(nettest.Data.AppDbContext db)
    {
        var inviteService = new InviteService(db, new NoopInviteEmailSender());
        return new InvitesController(inviteService, db, CreateConfiguration());
    }

    private static Unit AddUnit(nettest.Data.AppDbContext db)
    {
        var property = new Property
        {
            Name = "Maple House",
            Address = "123 Maple Street",
            Landlord = new User
            {
                Email = "landlord@example.com",
                PasswordHash = "hash",
                Role = "Landlord"
            }
        };
        var unit = new Unit
        {
            UnitNumber = 101,
            Property = property
        };
        db.Units.Add(unit);
        db.SaveChanges();
        return unit;
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "this-is-a-long-enough-test-signing-key",
                ["Jwt:Issuer"] = "nettest",
                ["Jwt:Audience"] = "nettest-client"
            })
            .Build();
    }

    private sealed class NoopInviteEmailSender : IInviteEmailSender
    {
        public Task SendInviteAsync(Invite invite, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
