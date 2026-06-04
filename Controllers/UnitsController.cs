using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;
using System.Security.Claims;

namespace nettest.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Landlord")]
[Route("api/properties/{propertyId:int}/units")]
public class UnitsController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;
    
    [HttpPost]
    public IActionResult CreateUnit(int propertyId, CreateUnitDto dto)
    {
        var property = _db.Properties
            .FirstOrDefault(p => p.Id == propertyId);

        if (property == null || !CanAccessProperty(property))
            return NotFound("Property not found");

        var unit = new Unit
        {
            UnitNumber = dto.UnitNumber,
            PropertyId = propertyId
        };

        _db.Units.Add(unit);
        _db.SaveChanges();

        unit.Property = property;

        return Ok(ToUnitResponse(unit));
    }

    [HttpGet]
    public IActionResult GetUnits(int propertyId)
    {
        var property = _db.Properties.FirstOrDefault(p => p.Id == propertyId);

        if (property == null || !CanAccessProperty(property))
            return NotFound("Property not found");

        var units = _db.Units
            .Include(u => u.Property)
            .Where(u => u.PropertyId == propertyId)
            .Select(unit => ToUnitResponse(unit))
            .ToList();

        return Ok(units);
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private bool CanAccessProperty(Property property)
    {
        return User.IsInRole("Admin") || property.LandlordId == GetCurrentUserId();
    }

    private static UnitResponseDto ToUnitResponse(Unit unit)
    {
        return new UnitResponseDto(
            unit.Id,
            unit.UnitNumber,
            unit.PropertyId,
            unit.Property == null
                ? null
                : new PropertyResponseDto(
                    unit.Property.Id,
                    unit.Property.Name,
                    unit.Property.Address,
                    unit.Property.LandlordId,
                    null,
                    unit.Property.CreatedAt),
            unit.CreatedAt);
    }
}