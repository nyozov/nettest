using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using nettest.Data;
using nettest.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace nettest.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;

    [HttpPost("login")]
    public IActionResult Login(LoginDto dto)
    {
        var user = _db.Users.FirstOrDefault(u => u.Email == dto.Email);

        if (user == null)
            return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized();

        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token)
        });
    }
}