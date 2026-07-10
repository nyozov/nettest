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

        return Ok(ToUnitResponse(unit, []));
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
            .ToList();

        var unitIds = units.Select(unit => unit.Id).ToList();
        var unitTenants = _db.Users
            .Where(user =>
                user.UnitId != null &&
                unitIds.Contains(user.UnitId.Value))
            .OrderBy(user => user.Email)
            .ToList();
        var tenantsByUnit = unitTenants
            .GroupBy(user => user.UnitId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(user => new UserResponseDto(
                        user.Id,
                        user.Email,
                        user.Role,
                        user.CreatedAt))
                    .ToList());

        return Ok(units.Select(unit =>
            ToUnitResponse(
                unit,
                tenantsByUnit.GetValueOrDefault(unit.Id, []))));
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue("sub")!);
    }

    private bool CanAccessProperty(Property property)
    {
        return User.IsInRole("Admin") || property.LandlordId == GetCurrentUserId();
    }

    private static UnitResponseDto ToUnitResponse(
        Unit unit,
        IReadOnlyList<UserResponseDto> tenants)
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
            tenants,
            unit.CreatedAt);
    }
}
