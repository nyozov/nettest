using Microsoft.EntityFrameworkCore;
using nettest.Models;

namespace nettest.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<MaintenanceRequestImage> MaintenanceRequestImages => Set<MaintenanceRequestImage>();

    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<EmailConfirmationCode> EmailConfirmationCodes => Set<EmailConfirmationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invite>()
            .ToTable("Invite");

        // Invite -> Unit (one unit can have many invites over time)
        modelBuilder.Entity<Invite>()
            .HasOne(i => i.Unit)
            .WithMany(u => u.Invites)
            .HasForeignKey(i => i.UnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Invite -> User (who redeemed it)
        modelBuilder.Entity<Invite>()
            .HasOne(i => i.RedeemedByUser)
            .WithMany()
            .HasForeignKey(i => i.RedeemedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // User -> Unit (tenant's current unit)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Unit)
            .WithMany()
            .HasForeignKey(u => u.UnitId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MaintenanceRequestImage>()
            .HasOne(image => image.MaintenanceRequest)
            .WithMany(request => request.Images)
            .HasForeignKey(image => image.MaintenanceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmailConfirmationCode>()
            .HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmailConfirmationCode>()
            .HasIndex(code => code.UserId);

        // Enforce uniqueness on the invite code itself
        modelBuilder.Entity<Invite>()
            .HasIndex(i => i.Code)
            .IsUnique();
    }
}
