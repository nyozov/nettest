using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;

namespace nettest.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public IActionResult CreateUser(CreateUserDto dto)
    {
        var user = new User
        {
            Email = dto.Email,
            PasswordHash = dto.Password, // (we'll fix hashing later)
            Role = dto.Role
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        return Ok(user);
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        return Ok(_db.Users.ToList());
    }
}