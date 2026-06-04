using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;

namespace nettest.Controllers;

[ApiController]
[Route("api/properties")]

public class PropertiesController(AppDbContext db) : ControllerBase
{


    private readonly AppDbContext _db = db;

    
    [HttpPost]

     public IActionResult CreateProperty(CreatePropertyDto dto)
    {
        var property = new Property
        {
            Name = dto.Name,
            Address = dto.Address,
            // LandlordId = landlordId
        };

        _db.Properties.Add(property);
        _db.SaveChanges();

        return Ok(property);
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        return Ok(_db.Properties.ToList());
    }
    
}