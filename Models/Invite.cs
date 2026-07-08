namespace nettest.Models;

public enum InviteStatus
{
    Pending,
    Redeemed,

    Expired,
    Revoked
}

public class Invite
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public InviteStatus Status { get; set; } = InviteStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(60);
    public DateTime? RedeemedAt { get; set; }

    // Allows more than one tenant to redeem the same invite (e.g. couples/roommates)
    public int MaxUses { get; set; } = 1;
    public int UsesCount { get; set; } = 0;

    // Optional: who it was sent to, for tracking/resend
    public string? SentToEmail { get; set; }

    // Set once a tenant redeems it — links the invite to the account it created
    public int? RedeemedByUserId { get; set; }
    public User? RedeemedByUser { get; set; }



}