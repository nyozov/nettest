namespace nettest.Models;

public class Unit
{
    public int Id {get; set;}
    public int UnitNumber {get; set;}

    public int PropertyId {get; set;}

    public Property Property {get; set;} = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MaintenanceRequest> MaintenanceRequests { get; set; } = [];

}