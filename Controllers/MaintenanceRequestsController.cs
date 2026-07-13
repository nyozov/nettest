using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using nettest.Data;
using nettest.Dtos;
using nettest.Models;
using nettest.Services;
using System.Security.Claims;

namespace nettest.Controllers;

[ApiController]
[Authorize]
[Route("api/units/{unitId:int}/requests")]
public class MaintenanceRequestsController(
    AppDbContext db,
    IImageUploader? imageUploader = null) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IImageUploader? _imageUploader = imageUploader;

    [HttpGet("/api/maintenance-requests")]
    [Authorize(Roles = "Admin,Landlord")]
    public IActionResult GetRequests()
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var requests = _db.MaintenanceRequests
            .Include(request => request.Unit)
            .ThenInclude(unit => unit.Property)
            .Include(request => request.CreatedByUser)
            .Include(request => request.Images)
            .Where(request =>
                isAdmin || request.Unit.Property.LandlordId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .ToList()
            .Select(request => new MaintenanceRequestListItemDto(
                request.Id,
                request.Title,
                request.Description,
                request.Status,
                request.UnitId,
                request.Unit.UnitNumber,
                request.Unit.PropertyId,
                request.Unit.Property.Name,
                request.CreatedByUserId,
                request.CreatedByUser == null
                    ? null
                    : new UserResponseDto(
                        request.CreatedByUser.Id,
                        request.CreatedByUser.Email,
                        request.CreatedByUser.Role,
                        request.CreatedByUser.CreatedAt),
                request.CreatedAt,
                request.CompletedAt,
                ToImageResponses(request.Images)))
            .ToList();

        return Ok(requests);
    }

    [HttpPost]
    [Consumes("application/json")]
    public IActionResult CreateRequest(
        int unitId,
        [FromBody] CreateMaintenanceRequestDto dto)
    {
        var accessResult = ValidateCreateRequestAccess(unitId, out var unit, out var userId);
        if (accessResult != null)
            return accessResult;

        return CreateRequestResponse(dto, [], unit!, userId);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(42 * 1024 * 1024)]
    public async Task<IActionResult> CreateRequestWithImages(
        int unitId,
        [FromForm] CreateMaintenanceRequestWithImagesDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.Images.Count > 5)
            return BadRequest("You can upload up to 5 images.");

        var accessResult = ValidateCreateRequestAccess(unitId, out var unit, out var userId);
        if (accessResult != null)
            return accessResult;

        if (dto.Images.Count > 0 && _imageUploader == null)
            throw new InvalidOperationException("Image uploader is not configured.");

        IReadOnlyList<UploadedImage> uploadedImages = dto.Images.Count == 0
            ? []
            : await _imageUploader!.UploadImagesAsync(dto.Images, cancellationToken);

        return CreateRequestResponse(dto, uploadedImages, unit!, userId);
    }

    private IActionResult? ValidateCreateRequestAccess(
        int unitId,
        out Unit? unit,
        out int userId)
    {
        userId = int.Parse(User.FindFirst("sub")!.Value);
        unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
            return NotFound("Unit not found");

        if (User.IsInRole("Landlord") && !CanAccessUnit(unit))
            return NotFound("Unit not found");

        if (User.IsInRole("Tenant"))
        {
            var currentUserId = userId;
            var user = _db.Users.FirstOrDefault(user => user.Id == currentUserId);
            if (user?.UnitId != unitId)
                return NotFound("Unit not found");
        }

        return null;
    }

    private IActionResult CreateRequestResponse(
        CreateMaintenanceRequestDto dto,
        IReadOnlyList<UploadedImage> uploadedImages,
        Unit unit,
        int userId)
    {
        var request = new MaintenanceRequest
        {
            Title = dto.Title,
            Description = dto.Description,
            UnitId = unit.Id,
            CreatedByUserId = userId,
            Status = 0,
            Urgency = 0,
            Images = uploadedImages
                .Select(image => new MaintenanceRequestImage
                {
                    Url = image.Url,
                    PublicId = image.PublicId
                })
                .ToList()
        };

        _db.MaintenanceRequests.Add(request);
        _db.SaveChanges();

        request.Unit = unit;

        return CreatedAtAction(
            nameof(GetRequest),
            new { unitId = unit.Id, id = request.Id },
            ToMaintenanceRequestResponse(request));
    }

    [HttpGet("{id:int}")]
    public IActionResult GetRequest(int unitId, int id)
    {
        var request = _db.MaintenanceRequests
            .Include(r => r.Unit)
            .ThenInclude(u => u.Property)
            .Include(r => r.CreatedByUser)
            .Include(r => r.Images)
            .FirstOrDefault(r => r.UnitId == unitId && r.Id == id);

        if (request == null)
            return NotFound();

        if (!CanAccessRequest(request))
            return NotFound();

        return Ok(ToMaintenanceRequestResponse(request));
    }

    [HttpGet]
    public IActionResult GetRequestsForUnit(int unitId)
    {
        var unit = _db.Units
            .Include(u => u.Property)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
            return NotFound("Unit not found");

        if (User.IsInRole("Landlord") && !CanAccessUnit(unit))
            return NotFound("Unit not found");

        var userId = GetCurrentUserId();
        var isTenant = User.IsInRole("Tenant");

        var requests = _db.MaintenanceRequests
            .Include(r => r.CreatedByUser)
            .Include(r => r.Images)
            .Where(r => r.UnitId == unitId)
            .Where(r => !isTenant || r.CreatedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList()
            .Select(request => ToMaintenanceRequestResponse(request))
            .ToList();

        return Ok(requests);
    }

    private bool CanAccessRequest(MaintenanceRequest request)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (User.IsInRole("Landlord"))
            return CanAccessUnit(request.Unit);

        return request.CreatedByUserId == GetCurrentUserId();
    }

    private bool CanAccessUnit(Unit unit)
    {
        return User.IsInRole("Admin") || unit.Property.LandlordId == GetCurrentUserId();
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue("sub")!);
    }

    private static MaintenanceRequestResponseDto ToMaintenanceRequestResponse(MaintenanceRequest request)
    {
        return new MaintenanceRequestResponseDto(
            request.Id,
            request.Title,
            request.Description,
            request.Status,
            request.UnitId,
            request.CreatedByUserId,
            request.CreatedByUser == null
                ? null
                : new UserResponseDto(
                    request.CreatedByUser.Id,
                    request.CreatedByUser.Email,
                    request.CreatedByUser.Role,
                    request.CreatedByUser.CreatedAt),
            request.CreatedAt,
            request.CompletedAt,
            ToImageResponses(request.Images));
    }

    private static IReadOnlyList<MaintenanceRequestImageResponseDto> ToImageResponses(
        IEnumerable<MaintenanceRequestImage> images)
    {
        return images
            .OrderBy(image => image.Id)
            .Select(image => new MaintenanceRequestImageResponseDto(
                image.Id,
                image.Url,
                image.PublicId,
                image.CreatedAt))
            .ToList();
    }
}
