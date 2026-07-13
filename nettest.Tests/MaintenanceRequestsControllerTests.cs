using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using nettest.Controllers;
using nettest.Dtos;
using nettest.Models;
using nettest.Services;

namespace nettest.Tests;

public class MaintenanceRequestsControllerTests
{
    [Fact]
    public void GetRequests_returns_only_requests_for_landlord_properties()
    {
        using var db = TestHelpers.CreateDbContext();
        var ownedUnit = AddUnit(db, landlordId: 10);
        var otherUnit = AddUnit(db, landlordId: 11);
        AddUser(db, id: 22, role: "Tenant");
        db.MaintenanceRequests.AddRange(
            new MaintenanceRequest
            {
                Title = "Owned request",
                Description = "Visible to the landlord",
                UnitId = ownedUnit.Id,
                CreatedByUserId = 22,
                Status = MaintenanceRequestStatus.Open
            },
            new MaintenanceRequest
            {
                Title = "Other request",
                Description = "Must not be visible",
                UnitId = otherUnit.Id,
                CreatedByUserId = 22,
                Status = MaintenanceRequestStatus.Open
            });
        db.SaveChanges();

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 10, role: "Landlord");

        var result = controller.GetRequests();

        var ok = Assert.IsType<OkObjectResult>(result);
        var requests = Assert.IsAssignableFrom<
            IEnumerable<MaintenanceRequestListItemDto>>(ok.Value).ToList();

        var request = Assert.Single(requests);
        Assert.Equal("Owned request", request.Title);
        Assert.Equal(ownedUnit.UnitNumber, request.UnitNumber);
        Assert.Equal($"Property {10}", request.PropertyName);
    }

    [Fact]
    public void CreateRequest_assigns_unit_and_current_user()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        AddUser(db, id: 22, role: "Tenant", unitId: unit.Id);

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 22, role: "Tenant");

        var result = controller.CreateRequest(unit.Id, new CreateMaintenanceRequestDto
        {
            Title = "Leak",
            Description = "Kitchen sink leak"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<MaintenanceRequestResponseDto>(created.Value);
        var request = Assert.Single(db.MaintenanceRequests);

        Assert.Equal(unit.Id, response.UnitId);
        Assert.Equal(22, response.CreatedByUserId);
        Assert.Equal(unit.Id, request.UnitId);
        Assert.Equal(22, request.CreatedByUserId);
    }

    [Fact]
    public async Task CreateRequestWithImages_uploads_and_stores_image_urls()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        AddUser(db, id: 22, role: "Tenant", unitId: unit.Id);
        var uploader = new FakeImageUploader([
            new UploadedImage(
                "https://res.cloudinary.com/demo/image/upload/request.jpg",
                "nestops/maintenance/request")
        ]);

        var controller = new MaintenanceRequestsController(db, uploader);
        TestHelpers.SetUser(controller, userId: 22, role: "Tenant");

        var result = await controller.CreateRequestWithImages(
            unit.Id,
            new CreateMaintenanceRequestWithImagesDto
            {
                Title = "Leak",
                Description = "Kitchen sink leak",
                Images = [CreateImageFile()]
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<MaintenanceRequestResponseDto>(created.Value);
        var image = Assert.Single(response.Images);

        Assert.Equal("https://res.cloudinary.com/demo/image/upload/request.jpg", image.Url);
        Assert.Equal("nestops/maintenance/request", image.PublicId);
        Assert.Single(db.MaintenanceRequestImages);
    }

    [Fact]
    public void CreateRequest_denies_tenant_for_unit_they_do_not_live_in()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        var otherUnit = AddUnit(db, landlordId: 11);
        AddUser(db, id: 22, role: "Tenant", unitId: otherUnit.Id);

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 22, role: "Tenant");

        var result = controller.CreateRequest(unit.Id, new CreateMaintenanceRequestDto
        {
            Title = "Leak",
            Description = "Kitchen sink leak"
        });

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(db.MaintenanceRequests);
    }

    [Fact]
    public void CreateRequest_denies_landlord_for_unit_they_do_not_own()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 11, role: "Landlord");

        var result = controller.CreateRequest(unit.Id, new CreateMaintenanceRequestDto
        {
            Title = "Leak",
            Description = "Kitchen sink leak"
        });

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(db.MaintenanceRequests);
    }

    [Fact]
    public void GetRequestsForUnit_returns_only_current_tenant_requests()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        AddUser(db, id: 22, role: "Tenant");
        AddUser(db, id: 23, role: "Tenant");
        db.MaintenanceRequests.AddRange(
            new MaintenanceRequest
            {
                Title = "Mine",
                Description = "My request",
                UnitId = unit.Id,
                CreatedByUserId = 22,
                Status = MaintenanceRequestStatus.Open
            },
            new MaintenanceRequest
            {
                Title = "Other",
                Description = "Other tenant request",
                UnitId = unit.Id,
                CreatedByUserId = 23,
                Status = MaintenanceRequestStatus.Open
            });
        db.SaveChanges();

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 22, role: "Tenant");

        var result = controller.GetRequestsForUnit(unit.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var requests = Assert.IsAssignableFrom<IEnumerable<MaintenanceRequestResponseDto>>(ok.Value).ToList();

        var request = Assert.Single(requests);
        Assert.Equal("Mine", request.Title);
    }

    [Fact]
    public void GetRequestsForUnit_returns_all_requests_for_owning_landlord()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        AddUser(db, id: 22, role: "Tenant");
        AddUser(db, id: 23, role: "Tenant");
        db.MaintenanceRequests.AddRange(
            new MaintenanceRequest
            {
                Title = "One",
                Description = "First",
                UnitId = unit.Id,
                CreatedByUserId = 22,
                Status = MaintenanceRequestStatus.Open
            },
            new MaintenanceRequest
            {
                Title = "Two",
                Description = "Second",
                UnitId = unit.Id,
                CreatedByUserId = 23,
                Status = MaintenanceRequestStatus.Open
            });
        db.SaveChanges();

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 10, role: "Landlord");

        var result = controller.GetRequestsForUnit(unit.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var requests = Assert.IsAssignableFrom<IEnumerable<MaintenanceRequestResponseDto>>(ok.Value);

        Assert.Equal(2, requests.Count());
    }

    [Fact]
    public void GetRequest_returns_not_found_when_tenant_requests_someone_elses_request()
    {
        using var db = TestHelpers.CreateDbContext();
        var unit = AddUnit(db, landlordId: 10);
        AddUser(db, id: 23, role: "Tenant");
        var request = new MaintenanceRequest
        {
            Title = "Other",
            Description = "Other tenant request",
            UnitId = unit.Id,
            CreatedByUserId = 23,
            Status = MaintenanceRequestStatus.Open
        };
        db.MaintenanceRequests.Add(request);
        db.SaveChanges();

        var controller = new MaintenanceRequestsController(db);
        TestHelpers.SetUser(controller, userId: 22, role: "Tenant");

        var result = controller.GetRequest(unit.Id, request.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    private static Unit AddUnit(nettest.Data.AppDbContext db, int landlordId)
    {
        AddUser(db, landlordId, "Landlord");

        var property = new Property
        {
            Name = $"Property {landlordId}",
            Address = $"{landlordId} Test St",
            LandlordId = landlordId
        };
        db.Properties.Add(property);
        db.SaveChanges();

        var unit = new Unit
        {
            UnitNumber = 100 + landlordId,
            PropertyId = property.Id
        };
        db.Units.Add(unit);
        db.SaveChanges();

        return unit;
    }

    private static void AddUser(nettest.Data.AppDbContext db, int id, string role, int? unitId = null)
    {
        if (db.Users.Any(user => user.Id == id))
            return;

        db.Users.Add(new User
        {
            Id = id,
            Email = $"user{id}@example.com",
            PasswordHash = "hash",
            Role = role,
            UnitId = unitId
        });
        db.SaveChanges();
    }

    private static IFormFile CreateImageFile()
    {
        var bytes = Encoding.UTF8.GetBytes("fake image");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "images", "leak.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private sealed class FakeImageUploader(
        IReadOnlyList<UploadedImage> uploadedImages) : IImageUploader
    {
        public Task<IReadOnlyList<UploadedImage>> UploadImagesAsync(
            IReadOnlyList<IFormFile> images,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(uploadedImages);
        }
    }
}
