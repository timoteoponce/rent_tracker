using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Models;

namespace RentTracker.Web.Data;

/// <summary>
/// Entity Framework database context for RentTracker.
/// Configures SQLite with optimizations for single-developer maintenance.
/// </summary>
public class RentTrackerDbContext : DbContext
{
    public RentTrackerDbContext(DbContextOptions<RentTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyUnit> PropertyUnits => Set<PropertyUnit>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PropertyPriceHistory> PropertyPriceHistory => Set<PropertyPriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.FullName).IsUnique();
            entity.Property(u => u.Role).HasMaxLength(100);
            entity.Property(u => u.FullName).HasMaxLength(100);
        });

        // Property configuration
        modelBuilder.Entity<Property>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Location).HasMaxLength(500);
            entity.Property(p => p.CurrentPrice).HasPrecision(18, 2);
            entity.Property(p => p.CurrentWarranty).HasPrecision(18, 2);
            
            entity.HasOne(p => p.Owner)
                .WithMany(u => u.OwnedProperties)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PropertyUnit configuration
        modelBuilder.Entity<PropertyUnit>(entity =>
        {
            entity.Property(u => u.Name).HasMaxLength(100);
            entity.Property(u => u.Price).HasPrecision(18, 2);
            entity.Property(u => u.Warranty).HasPrecision(18, 2);
            
            entity.HasOne(u => u.Property)
                .WithMany(p => p.Units)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Lease configuration
        modelBuilder.Entity<Lease>(entity =>
        {
            entity.Property(l => l.Status).HasMaxLength(50);
            entity.Property(l => l.AgreedPrice).HasPrecision(18, 2);
            entity.Property(l => l.AgreedWarranty).HasPrecision(18, 2);
            entity.Property(l => l.TerminationReason).HasMaxLength(500);
            
            entity.HasOne(l => l.Property)
                .WithMany(p => p.Leases)
                .HasForeignKey(l => l.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(l => l.PropertyUnit)
                .WithMany(u => u.Leases)
                .HasForeignKey(l => l.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(l => l.Tenant)
                .WithMany(u => u.Leases)
                .HasForeignKey(l => l.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(3);
            entity.Property(p => p.Status).HasMaxLength(50);
            entity.Property(p => p.Notes).HasMaxLength(200);
            
            entity.HasOne(p => p.Lease)
                .WithMany(l => l.Payments)
                .HasForeignKey(p => p.LeaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PropertyPriceHistory configuration
        modelBuilder.Entity<PropertyPriceHistory>(entity =>
        {
            entity.Property(h => h.Price).HasPrecision(18, 2);
            entity.Property(h => h.Warranty).HasPrecision(18, 2);
            
            entity.HasOne(h => h.Property)
                .WithMany(p => p.PriceHistory)
                .HasForeignKey(h => h.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
