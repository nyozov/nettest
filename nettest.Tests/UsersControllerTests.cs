using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;

namespace nettest.Tests;

public class UsersControllerTests
{
    [Fact]
    public void CreateUser_hashes_password_and_returns_safe_response()
    {
        using var db = TestHelpers.CreateDbContext();
        var controller = new UsersController(db);

        var result = controller.CreateUser(new CreateUserDto
        {
            Email = "tenant@example.com",
            Password = "password123",
            Role = "Tenant"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserResponseDto>(ok.Value);
        var user = Assert.Single(db.Users);

        Assert.Equal("tenant@example.com", response.Email);
        Assert.Equal("Tenant", response.Role);
        Assert.DoesNotContain("password", user.PasswordHash, StringComparison.OrdinalIgnoreCase);
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", user.PasswordHash));
        Assert.True(user.IsEmailConfirmed);
        Assert.NotNull(user.EmailConfirmedAt);
    }

    [Fact]
    public void CreateUser_returns_conflict_for_duplicate_email()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.Add(new User
        {
            Email = "duplicate@example.com",
            PasswordHash = "hash",
            Role = "Tenant"
        });
        db.SaveChanges();

        var controller = new UsersController(db);

        var result = controller.CreateUser(new CreateUserDto
        {
            Email = "duplicate@example.com",
            Password = "password123",
            Role = "Tenant"
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void GetUsers_returns_safe_responses_and_is_admin_only()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.AddRange(
            new User { Email = "admin@example.com", PasswordHash = "secret", Role = "Admin" },
            new User { Email = "tenant@example.com", PasswordHash = "secret", Role = "Tenant" });
        db.SaveChanges();

        var controller = new UsersController(db);
        var result = controller.GetUsers();

        var ok = Assert.IsType<OkObjectResult>(result);
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponseDto>>(ok.Value);

        Assert.Equal(2, users.Count());
        Assert.All(users, user => Assert.DoesNotContain("secret", user.ToString()));

        var authorize = typeof(UsersController)
            .GetMethod(nameof(UsersController.GetUsers))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin", authorize.Roles);
    }

    [Fact]
    public void CreateUser_is_admin_only()
    {
        var authorize = typeof(UsersController)
            .GetMethod(nameof(UsersController.CreateUser))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin", authorize.Roles);
    }
}
