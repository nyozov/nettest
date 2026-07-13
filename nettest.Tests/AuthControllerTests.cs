using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;
using nettest.Services;

namespace nettest.Tests;

public class AuthControllerTests
{
    [Fact]
    public void Login_returns_jwt_with_user_claims_for_valid_credentials()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.Add(new User
        {
            Email = "landlord@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Landlord"
        });
        db.SaveChanges();

        var controller = new AuthController(db, CreateConfiguration(), new NoopEmailConfirmationSender());

        var result = controller.Login(new LoginDto
        {
            Email = "landlord@example.com",
            Password = "password123"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value) as string;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("1", jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("landlord@example.com", jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Landlord", jwt.Claims.Single(c => c.Type == "role").Value);
    }

    [Fact]
    public void Login_returns_unauthorized_for_bad_password()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.Add(new User
        {
            Email = "tenant@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Tenant"
        });
        db.SaveChanges();

        var controller = new AuthController(db, CreateConfiguration(), new NoopEmailConfirmationSender());

        var result = controller.Login(new LoginDto
        {
            Email = "tenant@example.com",
            Password = "wrongpassword"
        });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Login_returns_forbidden_when_email_is_not_confirmed()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.Add(new User
        {
            Email = "landlord@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            Role = "Landlord",
            IsEmailConfirmed = false
        });
        db.SaveChanges();

        var controller = new AuthController(db, CreateConfiguration(), new NoopEmailConfirmationSender());

        var result = controller.Login(new LoginDto
        {
            Email = "landlord@example.com",
            Password = "password123"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_creates_unconfirmed_landlord_and_sends_code()
    {
        using var db = TestHelpers.CreateDbContext();
        var emailSender = new CapturingEmailConfirmationSender();
        var controller = new AuthController(db, CreateConfiguration(), emailSender);

        var result = await controller.Register(
            new RegisterDto
            {
                Email = "Landlord@Example.com",
                Password = "Password123"
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var user = Assert.Single(db.Users);
        Assert.Equal("landlord@example.com", user.Email);
        Assert.Equal("Landlord", user.Role);
        Assert.False(user.IsEmailConfirmed);
        Assert.NotNull(emailSender.Code);
        Assert.Single(db.EmailConfirmationCodes);
    }

    [Fact]
    public async Task VerifyEmail_confirms_user_and_returns_token()
    {
        using var db = TestHelpers.CreateDbContext();
        var emailSender = new CapturingEmailConfirmationSender();
        var controller = new AuthController(db, CreateConfiguration(), emailSender);

        await controller.Register(
            new RegisterDto
            {
                Email = "landlord@example.com",
                Password = "Password123"
            },
            CancellationToken.None);

        var result = await controller.VerifyEmail(
            new VerifyEmailDto
            {
                Email = "landlord@example.com",
                Code = emailSender.Code!
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value) as string;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var user = Assert.Single(db.Users);

        Assert.True(user.IsEmailConfirmed);
        Assert.NotNull(user.EmailConfirmedAt);
        Assert.Equal("landlord@example.com", jwt.Claims.Single(c => c.Type == "email").Value);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "this-is-a-long-enough-test-signing-key",
                ["Jwt:Issuer"] = "nettest",
                ["Jwt:Audience"] = "nettest-client"
            })
            .Build();
    }

    private sealed class NoopEmailConfirmationSender : IEmailConfirmationSender
    {
        public Task SendConfirmationCodeAsync(
            User user,
            string code,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingEmailConfirmationSender : IEmailConfirmationSender
    {
        public string? Code { get; private set; }

        public Task SendConfirmationCodeAsync(
            User user,
            string code,
            CancellationToken cancellationToken = default)
        {
            Code = code;
            return Task.CompletedTask;
        }
    }
}
