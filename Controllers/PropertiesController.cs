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

        return Ok(property);
    }

    [HttpGet]
    public IActionResult GetProperties()
    {
        var landlordId = GetCurrentUserId();

        var properties = _db.Properties
            .Include(p => p.Landlord)
            .Where(p => p.LandlordId == landlordId)
            .ToList();

        return Ok(properties);
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}