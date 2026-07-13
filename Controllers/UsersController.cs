using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Models;
using nettest.Dtos;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult CreateUser(CreateUserDto dto)
    {
        if (_db.Users.Any(u => u.Email == dto.Email))
            return Conflict("A user with that email already exists");

        var user = new User
        {
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            IsEmailConfirmed = true,
            EmailConfirmedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        return Ok(ToUserResponse(user));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult GetUsers()
    {
        var users = _db.Users
            .Select(user => ToUserResponse(user))
            .ToList();

        return Ok(users);
    }

    private static UserResponseDto ToUserResponse(User user)
    {
        return new UserResponseDto(
            user.Id,
            user.Email,
            user.Role,
            user.CreatedAt);
    }
}
