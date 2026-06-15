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
[Route("api/properties")]
public class PropertiesController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;
    
    [HttpPost]
    public IActionResult CreateProperty(CreatePropertyDto dto)
    {
        var landlordId = GetCurrentUserId();

        var property = new Property
        {
            Name = dto.Name,
            Address = dto.Address,
            LandlordId = landlordId
        };

        _db.Properties.Add(property);
        _db.SaveChanges();

        return Ok(ToPropertyResponse(property));
    }

    [HttpGet]
    public IActionResult GetProperties()
    {
        var landlordId = GetCurrentUserId();
        var isAdmin = IsAdmin();

        var properties = _db.Properties
            .Include(p => p.Landlord)
            .Where(p => isAdmin || p.LandlordId == landlordId)
            .Select(property => ToPropertyResponse(property))
            .ToList();

        return Ok(properties);
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    private static PropertyResponseDto ToPropertyResponse(Property property)
    {
        return new PropertyResponseDto(
            property.Id,
            property.Name,
            property.Address,
            property.LandlordId,
            property.Landlord == null
                ? null
                : new UserResponseDto(
                    property.Landlord.Id,
                    property.Landlord.Email,
                    property.Landlord.Role,
                    property.Landlord.CreatedAt),
            property.CreatedAt);
    }
}
