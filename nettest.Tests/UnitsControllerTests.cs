using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;

namespace nettest.Tests;

public class UnitsControllerTests
{
    [Fact]
    public void CreateUnit_assigns_property_from_route_for_property_owner()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.CreateUnit(property.Id, new CreateUnitDto { UnitNumber = 101 });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UnitResponseDto>(ok.Value);
        var unit = Assert.Single(db.Units);

        Assert.Equal(property.Id, response.PropertyId);
        Assert.Equal(property.Id, unit.PropertyId);
        Assert.Equal(101, response.UnitNumber);
    }

    [Fact]
    public void CreateUnit_returns_not_found_when_landlord_does_not_own_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Other", Address = "2 Unit St", LandlordId = 10 };
        db.Properties.Add(property);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.CreateUnit(property.Id, new CreateUnitDto { UnitNumber = 101 });

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(db.Units);
    }

    [Fact]
    public void CreateUnit_allows_admin_for_any_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Any", Address = "3 Unit St", LandlordId = 10 };
        db.Properties.Add(property);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 99, role: "Admin");

        var result = controller.CreateUnit(property.Id, new CreateUnitDto { UnitNumber = 202 });

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.Units);
    }

    [Fact]
    public void GetUnits_returns_only_units_for_requested_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        var otherProperty = new Property { Name = "Other", Address = "2 Unit St", LandlordId = 5 };
        db.Properties.AddRange(property, otherProperty);
        db.SaveChanges();
        db.Units.AddRange(
            new Unit { UnitNumber = 101, PropertyId = property.Id },
            new Unit { UnitNumber = 201, PropertyId = otherProperty.Id });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.GetUnits(property.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var units = Assert.IsAssignableFrom<IEnumerable<UnitResponseDto>>(ok.Value).ToList();

        var unit = Assert.Single(units);
        Assert.Equal(101, unit.UnitNumber);
    }

    [Fact]
    public void GetUnits_includes_current_users_assigned_to_each_unit()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();

        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.SaveChanges();

        db.Users.AddRange(
            new User
            {
                Email = "tenant@example.com",
                PasswordHash = "hash",
                Role = "Tenant",
                UnitId = unit.Id
            },
            new User
            {
                Email = "roommate@example.com",
                PasswordHash = "hash",
                Role = "tenant",
                UnitId = unit.Id
            });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.GetUnits(property.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var units = Assert.IsAssignableFrom<IEnumerable<UnitResponseDto>>(ok.Value).ToList();
        var response = Assert.Single(units);

        Assert.Equal(
            ["roommate@example.com", "tenant@example.com"],
            response.Tenants.Select(tenant => tenant.Email).ToArray());
    }

    [Fact]
    public void Units_controller_allows_only_admin_and_landlord_roles()
    {
        var authorize = typeof(UnitsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Admin,Landlord", authorize.Roles);
    }
}
