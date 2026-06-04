using Microsoft.AspNetCore.Authorization;
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

    [HttpPost]
    public IActionResult CreateRequest(
        int unitId,
        CreateMaintenanceRequestDto dto)
    {
        var userId = int.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var unit = _db.Units.FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
            return NotFound("Unit not found");

        var request = new MaintenanceRequest
        {
            Title = dto.Title,
            Description = dto.Description,
            UnitId = unitId,
            CreatedByUserId = userId,
            Status = 0
        };

        _db.MaintenanceRequests.Add(request);
        _db.SaveChanges();

        return CreatedAtAction(
            nameof(GetRequest),
            new { unitId, id = request.Id },
            request);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRequest(int unitId, int id)
    {
        var request = _db.MaintenanceRequests
            .FirstOrDefault(r => r.UnitId == unitId && r.Id == id);

        if (request == null)
            return NotFound();

        return Ok(request);
    }

    [HttpGet]
    public IActionResult GetRequestsForUnit(int unitId)
    {
        var requests = _db.MaintenanceRequests
            .Where(r => r.UnitId == unitId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        return Ok(requests);
    }
}