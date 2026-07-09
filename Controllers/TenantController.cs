using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using nettest.Data;
using System.Security.Claims;

namespace nettest.Controllers;

[ApiController]
[Authorize(Roles = "Tenant")]
[Route("api/tenant")]
public class TenantController(AppDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentTenant()
    {
        var userId = int.Parse(User.FindFirstValue("sub")!);

        var user = db.Users
            .Include(u => u.Unit)
            .ThenInclude(unit => unit!.Property)
            .FirstOrDefault(u => u.Id == userId);

        if (user == null)
        {
            return NotFound("Tenant not found");
        }

        if (user.Unit == null)
        {
            return NotFound("Tenant is not assigned to a unit");
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Role,
            Unit = new
            {
                user.Unit.Id,
                user.Unit.UnitNumber,
                user.Unit.PropertyId,
                Property = new
                {
                    user.Unit.Property.Id,
                    user.Unit.Property.Name,
                    user.Unit.Property.Address
                }
            }
        });
    }
}
