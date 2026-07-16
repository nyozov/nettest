using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using nettest.Data;
using nettest.Dtos;
using nettest.Models;
using nettest.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;

namespace nettest.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IConfiguration config,
    IEmailConfirmationSender confirmationSender,
    IGoogleTokenVerifier googleTokenVerifier) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;
    private readonly IEmailConfirmationSender _confirmationSender = confirmationSender;
    private readonly IGoogleTokenVerifier _googleTokenVerifier = googleTokenVerifier;

    [HttpPost("login")]
    public IActionResult Login(LoginDto dto)
    {
        var email = NormalizeEmail(dto.Email);
        var user = _db.Users.FirstOrDefault(u => u.Email == email);

        if (user == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized();

        if (!user.IsEmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden, "Email confirmation is required.");

        return Ok(new { token = CreateToken(user) });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(
        GoogleLoginDto dto,
        CancellationToken cancellationToken)
    {
        var googleUser = await _googleTokenVerifier.VerifyAsync(dto.IdToken, cancellationToken);
        if (googleUser == null ||
            string.IsNullOrWhiteSpace(googleUser.Email) ||
            !googleUser.EmailVerified)
        {
            return Unauthorized();
        }

        var email = NormalizeEmail(googleUser.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        var now = DateTime.UtcNow;

        if (user == null)
        {
            user = new User
            {
                Email = email,
                PasswordHash = "",
                Role = "Landlord",
                IsEmailConfirmed = true,
                EmailConfirmedAt = now
            };
            _db.Users.Add(user);
        }
        else if (!user.IsEmailConfirmed)
        {
            user.IsEmailConfirmed = true;
            user.EmailConfirmedAt = now;

            var existingCodes = _db.EmailConfirmationCodes
                .Where(code => code.UserId == user.Id && code.ConsumedAt == null);
            _db.EmailConfirmationCodes.RemoveRange(existingCodes);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { token = CreateToken(user) });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterDto dto,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);

        if (_db.Users.Any(u => u.Email == email))
            return Conflict("A user with that email already exists");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Landlord",
            IsEmailConfirmed = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var code = await CreateConfirmationCodeAsync(user, cancellationToken);
        try
        {
            await _confirmationSender.SendConfirmationCodeAsync(user, code, cancellationToken);
        }
        catch
        {
            _db.EmailConfirmationCodes.RemoveRange(
                _db.EmailConfirmationCodes.Where(confirmationCode => confirmationCode.UserId == user.Id));
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return Ok(new
        {
            email = user.Email,
            expiresInMinutes = 15
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailDto dto,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(dto.Email);
        var user = _db.Users.FirstOrDefault(u => u.Email == email);

        if (user == null)
            return NotFound("Account not found.");

        if (user.IsEmailConfirmed)
            return Ok(new { token = CreateToken(user) });

        var codeHash = HashCode(user.Id, dto.Code);
        var confirmationCode = _db.EmailConfirmationCodes
            .Where(code =>
                code.UserId == user.Id &&
                code.ConsumedAt == null &&
                code.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefault();

        if (confirmationCode == null || confirmationCode.CodeHash != codeHash)
            return BadRequest("Invalid or expired confirmation code.");

        confirmationCode.ConsumedAt = DateTime.UtcNow;
        user.IsEmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { token = CreateToken(user) });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(
        ResendConfirmationDto request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = _db.Users.FirstOrDefault(u => u.Email == email);

        if (user == null)
            return NotFound("Account not found.");

        if (user.IsEmailConfirmed)
            return BadRequest("Email is already confirmed.");

        var code = await CreateConfirmationCodeAsync(user, cancellationToken);
        await _confirmationSender.SendConfirmationCodeAsync(user, code, cancellationToken);

        return Ok(new
        {
            email = user.Email,
            expiresInMinutes = 15
        });
    }

    private string CreateToken(User user)
    {
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

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateConfirmationCodeAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var existingCodes = _db.EmailConfirmationCodes
            .Where(code => code.UserId == user.Id && code.ConsumedAt == null);
        _db.EmailConfirmationCodes.RemoveRange(existingCodes);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _db.EmailConfirmationCodes.Add(new EmailConfirmationCode
        {
            UserId = user.Id,
            CodeHash = HashCode(user.Id, code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        await _db.SaveChangesAsync(cancellationToken);
        return code;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string HashCode(int userId, string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{code.Trim()}"));
        return Convert.ToHexString(bytes);
    }
}
