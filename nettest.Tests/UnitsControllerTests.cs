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
    public void CreateUnit_returns_conflict_for_duplicate_unit_number()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        db.Units.Add(new Unit { UnitNumber = 101, PropertyId = property.Id });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.CreateUnit(property.Id, new CreateUnitDto { UnitNumber = 101 });

        Assert.IsType<ConflictObjectResult>(result);
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
    public void UpdateUnit_updates_unit_number_for_owned_property()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.UpdateUnit(property.Id, unit.Id, new UpdateUnitDto { UnitNumber = 102 });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UnitResponseDto>(ok.Value);

        Assert.Equal(102, response.UnitNumber);
        Assert.Equal(102, db.Units.Single().UnitNumber);
    }

    [Fact]
    public void UpdateUnit_returns_conflict_for_duplicate_unit_number()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        var otherUnit = new Unit { UnitNumber = 102, PropertyId = property.Id };
        db.Units.AddRange(unit, otherUnit);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.UpdateUnit(property.Id, unit.Id, new UpdateUnitDto { UnitNumber = 102 });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void DeleteUnit_removes_empty_unit()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.DeleteUnit(property.Id, unit.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Units);
    }

    [Fact]
    public void DeleteUnit_returns_conflict_when_unit_has_assigned_user()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.SaveChanges();
        db.Users.Add(new User
        {
            Email = "tenant@example.com",
            PasswordHash = "hash",
            Role = "Tenant",
            UnitId = unit.Id
        });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.DeleteUnit(property.Id, unit.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Units);
    }

    [Fact]
    public void DeleteUnit_returns_conflict_when_unit_has_maintenance_requests()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.Users.Add(new User { Id = 22, Email = "tenant@example.com", PasswordHash = "hash", Role = "Tenant" });
        db.SaveChanges();
        db.MaintenanceRequests.Add(new MaintenanceRequest
        {
            Title = "Leak",
            Description = "Sink leak",
            UnitId = unit.Id,
            CreatedByUserId = 22
        });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.DeleteUnit(property.Id, unit.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Units);
    }

    [Fact]
    public void DeleteUnit_returns_conflict_when_unit_has_invites()
    {
        using var db = TestHelpers.CreateDbContext();
        var property = new Property { Name = "Owned", Address = "1 Unit St", LandlordId = 5 };
        db.Properties.Add(property);
        db.SaveChanges();
        var unit = new Unit { UnitNumber = 101, PropertyId = property.Id };
        db.Units.Add(unit);
        db.SaveChanges();
        db.Invites.Add(new Invite { Code = "ABC123", UnitId = unit.Id });
        db.SaveChanges();

        var controller = new UnitsController(db);
        TestHelpers.SetUser(controller, userId: 5, role: "Landlord");

        var result = controller.DeleteUnit(property.Id, unit.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Units);
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
