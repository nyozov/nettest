namespace nettest.Models;

public enum MaintenanceRequestStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

public class MaintenanceRequest
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public MaintenanceRequestStatus Status { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}