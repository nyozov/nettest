using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;
using System.Security.Claims;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/properties/{propertyId:int}/units")]
public class UnitsController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;
    
    [HttpPost]
    public IActionResult CreateUnit(int propertyId, CreateUnitDto dto)
    {
        var landlordId = GetCurrentUserId();
        var property = _db.Properties
            .FirstOrDefault(p => p.Id == propertyId && p.LandlordId == landlordId);

        if (property == null)
            return NotFound("Property not found");

        var unit = new Unit
        {
            UnitNumber = dto.UnitNumber,
            PropertyId = propertyId
        };

        _db.Units.Add(unit);
        _db.SaveChanges();

        return Ok(unit);
    }

    [HttpGet]
    public IActionResult GetUnits(int propertyId)
    {
        var landlordId = GetCurrentUserId();

        var units = _db.Units
            .Include(u => u.Property)
            .Where(u => u.PropertyId == propertyId && u.Property.LandlordId == landlordId)
            .ToList();

        return Ok(units);
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}