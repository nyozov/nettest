using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;

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

        var controller = new AuthController(db, CreateConfiguration());

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

        var controller = new AuthController(db, CreateConfiguration());

        var result = controller.Login(new LoginDto
        {
            Email = "tenant@example.com",
            Password = "wrongpassword"
        });

        Assert.IsType<UnauthorizedResult>(result);
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
}
