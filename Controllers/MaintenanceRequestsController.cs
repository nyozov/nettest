using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Dtos;
using nettest.Models;
using System.Security.Claims;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/units/{unitId:int}/requests")]
public class MaintenanceRequestsController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;

    [HttpGet("/api/maintenance-requests")]
    [Authorize(Roles = "Admin,Landlord")]
    public IActionResult GetRequests()
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var requests = _db.MaintenanceRequests
            .Include(request => request.Unit)
            .ThenInclude(unit => unit.Property)
            .Include(request => request.CreatedByUser)
            .Where(request =>
                isAdmin || request.Unit.Property.LandlordId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new MaintenanceRequestListItemDto(
                request.Id,
                request.Title,
                request.Description,
                request.Status,
                request.UnitId,
                request.Unit.UnitNumber,
                request.Unit.PropertyId,
                request.Unit.Property.Name,
                request.CreatedByUserId,
                request.CreatedByUser == null
                    ? null
                    : new UserResponseDto(
                        request.CreatedByUser.Id,
                        request.CreatedByUser.Email,
                        request.CreatedByUser.Role,
                        request.CreatedByUser.CreatedAt),
                request.CreatedAt,
                request.CompletedAt))
            .ToList();

        return Ok(requests);
    }

    [HttpPost]
    public IActionResult CreateRequest(
        int unitId,
        CreateMaintenanceRequestDto dto)
    {
        var userId = int.Parse(
            User.FindFirst("sub")!.Value);

        var unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
            return NotFound("Unit not found");

        if (User.IsInRole("Landlord") && !CanAccessUnit(unit))
            return NotFound("Unit not found");

        if (User.IsInRole("Tenant"))
        {
            var user = _db.Users.FirstOrDefault(user => user.Id == userId);
            if (user?.UnitId != unitId)
                return NotFound("Unit not found");
        }

        var request = new MaintenanceRequest
        {
            Title = dto.Title,
            Description = dto.Description,
            UnitId = unitId,
            CreatedByUserId = userId,
            Status = 0,
            Urgency = 0,
        };

        _db.MaintenanceRequests.Add(request);
        _db.SaveChanges();

        request.Unit = unit;

        return CreatedAtAction(
            nameof(GetRequest),
            new { unitId, id = request.Id },
            ToMaintenanceRequestResponse(request));
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRequest(int unitId, int id)
    {
        var request = _db.MaintenanceRequests
            .Include(r => r.Unit)
            .ThenInclude(u => u.Property)
            .Include(r => r.CreatedByUser)
            .FirstOrDefault(r => r.UnitId == unitId && r.Id == id);

        if (request == null)
            return NotFound();

        if (!CanAccessRequest(request))
            return NotFound();

        return Ok(ToMaintenanceRequestResponse(request));
    }

    [HttpGet]
    public IActionResult GetRequestsForUnit(int unitId)
    {
        var unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
            return NotFound("Unit not found");

        if (User.IsInRole("Landlord") && !CanAccessUnit(unit))
            return NotFound("Unit not found");

        var userId = GetCurrentUserId();
        var isTenant = User.IsInRole("Tenant");

        var requests = _db.MaintenanceRequests
            .Include(r => r.CreatedByUser)
            .Where(r => r.UnitId == unitId)
            .Where(r => !isTenant || r.CreatedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(request => ToMaintenanceRequestResponse(request))
            .ToList();

        return Ok(requests);
    }

    private bool CanAccessRequest(MaintenanceRequest request)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (User.IsInRole("Landlord"))
            return CanAccessUnit(request.Unit);

        return request.CreatedByUserId == GetCurrentUserId();
    }

    private bool CanAccessUnit(Unit unit)
    {
        return User.IsInRole("Admin") || unit.Property.LandlordId == GetCurrentUserId();
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue("sub")!);
    }

    private static MaintenanceRequestResponseDto ToMaintenanceRequestResponse(MaintenanceRequest request)
    {
        return new MaintenanceRequestResponseDto(
            request.Id,
            request.Title,
            request.Description,
            request.Status,
            request.UnitId,
            request.CreatedByUserId,
            request.CreatedByUser == null
                ? null
                : new UserResponseDto(
                    request.CreatedByUser.Id,
                    request.CreatedByUser.Email,
                    request.CreatedByUser.Role,
                    request.CreatedByUser.CreatedAt),
            request.CreatedAt,
            request.CompletedAt);
    }
}
