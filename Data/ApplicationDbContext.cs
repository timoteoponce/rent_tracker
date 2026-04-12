using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentTracker.Models;

namespace RentTracker.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyOwner> PropertyOwners => Set<PropertyOwner>();
    public DbSet<PropertyPriceHistory> PropertyPriceHistory => Set<PropertyPriceHistory>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<RentalPayment> RentalPayments => Set<RentalPayment>();
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Configure PropertyOwner composite key
        builder.Entity<PropertyOwner>(entity =>
        {
            entity.HasKey(e => new { e.PropertyId, e.OwnerId });
            entity.HasOne(e => e.Property)
                  .WithMany(p => p.Owners)
                  .HasForeignKey(e => e.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.OwnedProperties)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure PropertyPriceHistory
        builder.Entity<PropertyPriceHistory>(entity =>
        {
            entity.HasOne(e => e.Property)
                  .WithMany(p => p.PriceHistory)
                  .HasForeignKey(e => e.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChangedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ChangedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure Rental
        builder.Entity<Rental>(entity =>
        {
            entity.HasOne(e => e.Property)
                  .WithMany(p => p.Rentals)
                  .HasForeignKey(e => e.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                  .WithMany(u => u.RentalsAsTenant)
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Status)
                  .HasConversion<string>();
        });
        
        // Configure RentalPayment
        builder.Entity<RentalPayment>(entity =>
        {
            entity.HasOne(e => e.Rental)
                  .WithMany(r => r.Payments)
                  .HasForeignKey(e => e.RentalId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PreviousPayment)
                  .WithMany(p => p.NewerVersions)
                  .HasForeignKey(e => e.PreviousPaymentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure ApplicationUser - FullName unique
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(e => e.FullName).IsUnique();
        });
        
        // Configure decimal precision for monetary values
        builder.Entity<Property>(entity =>
        {
            entity.Property(e => e.CurrentPrice).HasPrecision(18, 2);
            entity.Property(e => e.CurrentWarranty).HasPrecision(18, 2);
            entity.Property(e => e.LocationLatitude).HasPrecision(18, 6);
            entity.Property(e => e.LocationLongitude).HasPrecision(18, 6);
        });
        
        builder.Entity<PropertyPriceHistory>(entity =>
        {
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Warranty).HasPrecision(18, 2);
        });
        
        builder.Entity<Rental>(entity =>
        {
            entity.Property(e => e.MonthlyRent).HasPrecision(18, 2);
            entity.Property(e => e.WarrantyAmount).HasPrecision(18, 2);
        });
        
        builder.Entity<RentalPayment>(entity =>
        {
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });
    }
    
    public async Task ApplyMigrationsAsync()
    {
        await Database.MigrateAsync();
        
        // Apply SQLite pragmas for optimization
        if (Database.IsSqlite())
        {
            await Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;");
            await Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            await Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;");
            await Database.ExecuteSqlRawAsync("PRAGMA cache_size = -64000;"); // 64MB cache
            await Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;");
        }
    }
}
