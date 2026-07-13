using nettest.Models;

namespace nettest.Dtos;

public record UserResponseDto(
    int Id,
    string Email,
    string Role,
    DateTime CreatedAt);

public record PropertyResponseDto(
    int Id,
    string Name,
    string Address,
    int LandlordId,
    UserResponseDto? Landlord,
    DateTime CreatedAt);

public record UnitResponseDto(
    int Id,
    int UnitNumber,
    int PropertyId,
    PropertyResponseDto? Property,
    IReadOnlyList<UserResponseDto> Tenants,
    DateTime CreatedAt);

public record MaintenanceRequestImageResponseDto(
    int Id,
    string Url,
    string PublicId,
    DateTime CreatedAt);

public record MaintenanceRequestResponseDto(
    int Id,
    string Title,
    string Description,
    MaintenanceRequestStatus Status,
    int UnitId,
    int CreatedByUserId,
    UserResponseDto? CreatedByUser,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<MaintenanceRequestImageResponseDto> Images);

public record MaintenanceRequestListItemDto(
    int Id,
    string Title,
    string Description,
    MaintenanceRequestStatus Status,
    int UnitId,
    int UnitNumber,
    int PropertyId,
    string PropertyName,
    int CreatedByUserId,
    UserResponseDto? CreatedByUser,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<MaintenanceRequestImageResponseDto> Images);
