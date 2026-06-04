using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;

namespace nettest.Controllers;

[ApiController]
[Route("api/units")]

public class UnitsController(AppDbContext db) : ControllerBase
{


    private readonly AppDbContext _db = db;

    
    [HttpPost]

     public IActionResult CreateUnit(CreateUnitDto dto)
    {
        var unit = new Unit
        {
            UnitNumber = dto.UnitNumber
            // PropertyId = PropertyId
        };

        _db.Units.Add(unit);
        _db.SaveChanges();

        return Ok(unit);
    }

    [HttpGet]
    public IActionResult GetUnits()
    {
        return Ok(_db.Properties.ToList());
    }
    
}