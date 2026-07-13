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

        var duplicateUnitNumber = _db.Units.Any(unit =>
            unit.PropertyId == propertyId &&
            unit.UnitNumber == dto.UnitNumber);
        if (duplicateUnitNumber)
            return Conflict("A unit with that number already exists for this property.");

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

        var tenantsByUnit = GetTenantsByUnit(units.Select(unit => unit.Id).ToList());

        return Ok(units.Select(unit =>
            ToUnitResponse(
                unit,
                tenantsByUnit.GetValueOrDefault(unit.Id, []))));
    }

    [HttpPut("{unitId:int}")]
    public IActionResult UpdateUnit(int propertyId, int unitId, UpdateUnitDto dto)
    {
        var property = _db.Properties.FirstOrDefault(p => p.Id == propertyId);

        if (property == null || !CanAccessProperty(property))
            return NotFound("Property not found");

        var unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId && u.PropertyId == propertyId);

        if (unit == null)
            return NotFound("Unit not found");

        var duplicateUnitNumber = _db.Units.Any(candidate =>
            candidate.PropertyId == propertyId &&
            candidate.Id != unitId &&
            candidate.UnitNumber == dto.UnitNumber);
        if (duplicateUnitNumber)
            return Conflict("A unit with that number already exists for this property.");

        unit.UnitNumber = dto.UnitNumber;
        _db.SaveChanges();

        return Ok(ToUnitResponse(
            unit,
            GetTenantsByUnit([unit.Id]).GetValueOrDefault(unit.Id, [])));
    }

    [HttpDelete("{unitId:int}")]
    public IActionResult DeleteUnit(int propertyId, int unitId)
    {
        var property = _db.Properties.FirstOrDefault(p => p.Id == propertyId);

        if (property == null || !CanAccessProperty(property))
            return NotFound("Property not found");

        var unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId && u.PropertyId == propertyId);

        if (unit == null)
            return NotFound("Unit not found");

        if (_db.Users.Any(user => user.UnitId == unitId))
            return Conflict("This unit has assigned users and cannot be deleted.");

        if (_db.MaintenanceRequests.Any(request => request.UnitId == unitId))
            return Conflict("This unit has maintenance requests and cannot be deleted.");

        if (_db.Invites.Any(invite => invite.UnitId == unitId))
            return Conflict("This unit has invites and cannot be deleted.");

        _db.Units.Remove(unit);
        _db.SaveChanges();

        return NoContent();
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue("sub")!);
    }

    private bool CanAccessProperty(Property property)
    {
        return User.IsInRole("Admin") || property.LandlordId == GetCurrentUserId();
    }

    private Dictionary<int, List<UserResponseDto>> GetTenantsByUnit(IReadOnlyList<int> unitIds)
    {
        var unitTenants = _db.Users
            .Where(user =>
                user.UnitId != null &&
                unitIds.Contains(user.UnitId.Value))
            .OrderBy(user => user.Email)
            .ToList();

        return unitTenants
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
