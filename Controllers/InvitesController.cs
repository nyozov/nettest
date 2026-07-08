using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nettest.Services;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/units/{unitId}/invites")]
public class InvitesController(InviteService inviteService) : ControllerBase
{
    public record CreateInviteRequest(string? SentToEmail, int MaxUses = 1);

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
    }
}