namespace nettest.Models;

public class Property
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Address { get; set; } = "";
    public int LandlordId { get; set; }

    public User Landlord { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;



}