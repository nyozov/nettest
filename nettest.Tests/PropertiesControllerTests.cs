using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;

namespace nettest.Tests;

public class PropertiesControllerTests
{
    [Fact]
    public void CreateProperty_assigns_landlord_from_authenticated_user()
    {
        using var db = TestHelpers.CreateDbContext();
        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 7, role: "Landlord");

        var result = controller.CreateProperty(new CreatePropertyDto
        {
            Name = "Maple House",
            Address = "123 Maple St"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PropertyResponseDto>(ok.Value);
        var property = Assert.Single(db.Properties);

        Assert.Equal(7, response.LandlordId);
        Assert.Equal(7, property.LandlordId);
    }

    [Fact]
    public void GetProperties_returns_only_landlord_properties()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.AddRange(
            new User { Id = 1, Email = "one@example.com", PasswordHash = "hash", Role = "Landlord" },
            new User { Id = 2, Email = "two@example.com", PasswordHash = "hash", Role = "Landlord" });
        db.Properties.AddRange(
            new Property { Name = "Mine", Address = "1 Mine St", LandlordId = 1 },
            new Property { Name = "Other", Address = "2 Other St", LandlordId = 2 });
        db.SaveChanges();

        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 1, role: "Landlord");

        var result = controller.GetProperties();

        var ok = Assert.IsType<OkObjectResult>(result);
        var properties = Assert.IsAssignableFrom<IEnumerable<PropertyResponseDto>>(ok.Value).ToList();

        var property = Assert.Single(properties);
        Assert.Equal("Mine", property.Name);
    }

    [Fact]
    public void GetProperties_returns_all_properties_for_admin()
    {
        using var db = TestHelpers.CreateDbContext();
        db.Users.AddRange(
            new User { Id = 1, Email = "one@example.com", PasswordHash = "hash", Role = "Landlord" },
            new User { Id = 2, Email = "two@example.com", PasswordHash = "hash", Role = "Landlord" });
        db.Properties.AddRange(
            new Property { Name = "One", Address = "1 Admin St", LandlordId = 1 },
            new Property { Name = "Two", Address = "2 Admin St", LandlordId = 2 });
        db.SaveChanges();

        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 99, role: "Admin");

        var result = controller.GetProperties();

        var ok = Assert.IsType<OkObjectResult>(result);
        var properties = Assert.IsAssignableFrom<IEnumerable<PropertyResponseDto>>(ok.Value);

        Assert.Equal(2, properties.Count());
    }

    [Fact]
    public void UpdateProperty_updates_owned_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Old", Address = "1 Old St", LandlordId = 7 };
        db.Properties.Add(property);
        db.SaveChanges();

        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 7, role: "Landlord");

        var result = controller.UpdateProperty(property.Id, new UpdatePropertyDto
        {
            Name = "New",
            Address = "2 New St"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PropertyResponseDto>(ok.Value);

        Assert.Equal("New", response.Name);
        Assert.Equal("2 New St", db.Properties.Single().Address);
    }

    [Fact]
    public void DeleteProperty_removes_empty_owned_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Empty", Address = "1 Empty St", LandlordId = 7 };
        db.Properties.Add(property);
        db.SaveChanges();

        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 7, role: "Landlord");

        var result = controller.DeleteProperty(property.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Properties);
    }

    [Fact]
    public void DeleteProperty_returns_conflict_when_property_has_units()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Occupied", Address = "1 Unit St", LandlordId = 7 };
        db.Properties.Add(property);
        db.SaveChanges();
        db.Units.Add(new Unit { UnitNumber = 101, PropertyId = property.Id });
        db.SaveChanges();

        var controller = new PropertiesController(db);
        TestHelpers.SetUser(controller, userId: 7, role: "Landlord");

        var result = controller.DeleteProperty(property.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Properties);
    }

    [Fact]
    public void Properties_controller_allows_only_admin_and_landlord_roles()
    {
        var authorize = typeof(PropertiesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin,Landlord", authorize.Roles);
    }
}
