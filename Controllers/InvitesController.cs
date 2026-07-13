using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using nettest.Data;
using nettest.Models;
using nettest.Services;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/units/{unitId}/invites")]
public class InvitesController(
    InviteService inviteService,
    AppDbContext db,
    IConfiguration config) : ControllerBase
{
    public record CreateInviteRequest(string? SentToEmail, int MaxUses = 1);

    public class RedeemInviteRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = "";
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Landlord")]
    public async Task<IActionResult> CreateInvite(int unitId, [FromBody] CreateInviteRequest request)
    {
        try
        {
            var invite = await inviteService.CreateInviteAsync(unitId, request.SentToEmail, request.MaxUses);

            return Ok(new
            {
                invite.Id,
                invite.Code,
                invite.UnitId,
                invite.ExpiresAt,
                invite.MaxUses
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    [HttpGet("/api/invites/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvite(string code)
    {
        var invite = await FindInviteAsync(code);
        if (invite == null)
        {
            return NotFound("Invite not found");
        }

        if (!IsInviteRedeemable(invite))
        {
            return BadRequest("Invite is no longer available");
        }

        return Ok(new
        {
            invite.Code,
            invite.SentToEmail,
            invite.ExpiresAt,
            invite.MaxUses,
            invite.UsesCount,
            Unit = new
            {
                invite.Unit.Id,
                invite.Unit.UnitNumber,
                invite.Unit.PropertyId,
                Property = new
                {
                    invite.Unit.Property.Id,
                    invite.Unit.Property.Name,
                    invite.Unit.Property.Address
                }
            }
        });
    }

    [HttpPost("/api/invites/{code}/redeem")]
    [AllowAnonymous]
    public async Task<IActionResult> RedeemInvite(string code, [FromBody] RedeemInviteRequest request)
    {
        var invite = await FindInviteAsync(code);
        if (invite == null)
        {
            return NotFound("Invite not found");
        }

        if (!IsInviteRedeemable(invite))
        {
            return BadRequest("Invite is no longer available");
        }

        var email = request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(invite.SentToEmail) &&
            !string.Equals(invite.SentToEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("This invite was sent to a different email address");
        }

        if (db.Users.Any(u => u.Email == email))
        {
            return Conflict("A user with that email already exists");
        }

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Tenant",
            UnitId = invite.UnitId,
            IsEmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        invite.UsesCount += 1;
        invite.RedeemedAt = DateTime.UtcNow;
        invite.RedeemedByUser = user;
        if (invite.UsesCount >= invite.MaxUses)
        {
            invite.Status = InviteStatus.Redeemed;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            token = CreateToken(user),
            user = new
            {
                user.Id,
                user.Email,
                user.Role,
                user.UnitId
            }
        });
    }

    private async Task<Invite?> FindInviteAsync(string code)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();

        return await db.Invites
            .Include(invite => invite.Unit)
            .ThenInclude(unit => unit.Property)
            .FirstOrDefaultAsync(invite => invite.Code == normalizedCode);
    }

    private static bool IsInviteRedeemable(Invite invite)
    {
        return invite.Status == InviteStatus.Pending &&
               invite.ExpiresAt > DateTime.UtcNow &&
               invite.UsesCount < invite.MaxUses;
    }

    private string CreateToken(User user)
    {
        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
