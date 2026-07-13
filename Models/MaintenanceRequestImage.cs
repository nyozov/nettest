namespace nettest.Models;

public class MaintenanceRequestImage
{
    public int Id { get; set; }

    public string Url { get; set; } = "";
    public string PublicId { get; set; } = "";

    public int MaintenanceRequestId { get; set; }
    public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
