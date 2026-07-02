using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using nettest.Data;

namespace nettest.Tests;

internal static class TestHelpers
{
    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    public static void SetUser(ControllerBase controller, int userId, string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("role", role),
                new Claim("email", $"user{userId}@example.com")
            ],
            authenticationType: "TestAuth",
            nameType: "sub",
            roleType: "role");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
