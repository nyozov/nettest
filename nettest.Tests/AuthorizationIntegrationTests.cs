using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using nettest.Data;

namespace nettest.Tests;

public class AuthorizationIntegrationTests
{
    private const string JwtKey = "this-is-a-long-enough-integration-test-signing-key";
    private const string JwtIssuer = "nettest-integration-tests";
    private const string JwtAudience = "nettest-integration-test-client";

    [Theory]
    [InlineData("/api/users")]
    [InlineData("/api/properties")]
    public async Task Admin_with_raw_jwt_claims_can_access_admin_endpoints(string endpoint)
    {
        await using var factory = new NettestWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("Admin"));

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Landlord_with_raw_jwt_claims_can_access_maintenance_queue()
    {
        await using var factory = new NettestWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("Landlord"));

        var response = await client.GetAsync("/api/maintenance-requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CreateToken(string role)
    {
        var claims = new[]
        {
            new Claim("sub", "1"),
            new Claim("email", $"{role.ToLowerInvariant()}@example.com"),
            new Claim("role", role)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class NettestWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                var databaseProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options
                        .UseInMemoryDatabase(Guid.NewGuid().ToString())
                        .UseInternalServiceProvider(databaseProvider));

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.TokenValidationParameters.ValidIssuer = JwtIssuer;
                        options.TokenValidationParameters.ValidAudience = JwtAudience;
                        options.TokenValidationParameters.IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(JwtKey));
                    });
            });
        }
    }
}
