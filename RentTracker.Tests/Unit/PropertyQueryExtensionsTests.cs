using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using Xunit;

namespace RentTracker.Tests.Unit;

public class PropertyQueryExtensionsTests
{
    private RentTrackerDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<RentTrackerDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new RentTrackerDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        // Enable foreign key enforcement for in-memory SQLite
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    [Fact]
    public void VisibleToUser_Property_Admin_SeesAll()
    {
        using var context = GetInMemoryContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = ownerId, Username = "owner", Email = "o@test.ch", FullName = "Owner", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = otherId, Username = "other", Email = "o2@test.ch", FullName = "Other", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.Properties.AddRange(
            new Property { Name = "Public", IsPrivate = false, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new Property { Name = "Private Other", IsPrivate = true, LastEditedById = otherId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new Property { Name = "Private Own", IsPrivate = true, LastEditedById = ownerId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var result = context.Properties.VisibleToUser(ownerId, true).ToList();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void VisibleToUser_Property_Owner_SeesPublicAndOwnPrivate()
    {
        using var context = GetInMemoryContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = ownerId, Username = "owner", Email = "o@test.ch", FullName = "Owner", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = otherId, Username = "other", Email = "o2@test.ch", FullName = "Other", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.Properties.AddRange(
            new Property { Name = "Public", IsPrivate = false, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new Property { Name = "Private Other", IsPrivate = true, LastEditedById = otherId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new Property { Name = "Private Own", IsPrivate = true, LastEditedById = ownerId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var result = context.Properties.VisibleToUser(ownerId, false).ToList();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Name == "Private Other");
    }

    [Fact]
    public void VisibleToUser_Property_NullUserId_ReturnsAll()
    {
        using var context = GetInMemoryContext();
        var editorId = Guid.NewGuid();
        context.Users.Add(new User { Id = editorId, Username = "editor", Email = "e@test.ch", FullName = "Editor", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow });
        context.Properties.Add(new Property { Name = "Private", IsPrivate = true, LastEditedById = editorId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow });
        context.SaveChanges();

        var result = context.Properties.VisibleToUser(null, false).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void VisibleToUser_Lease_Tenant_OnlyOwnLeases()
    {
        using var context = GetInMemoryContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = tenantId, Username = "tenant1", Email = "t1@test.ch", FullName = "T1", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = otherTenantId, Username = "tenant2", Email = "t2@test.ch", FullName = "T2", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        var property = new Property { Name = "P", IsPrivate = false, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow };
        context.Properties.Add(property);
        context.SaveChanges();

        context.Leases.AddRange(
            new Lease { PropertyId = property.Id, TenantId = tenantId, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new Lease { PropertyId = property.Id, TenantId = otherTenantId, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var result = context.Leases
            .Include(l => l.Property)
            .VisibleToUser(tenantId, false, true)
            .ToList();

        Assert.Single(result);
        Assert.Equal(tenantId, result[0].TenantId);
    }

    [Fact]
    public void VisibleToUser_Payment_Owner_SeesPublicAndOwnPrivate()
    {
        using var context = GetInMemoryContext();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = ownerId, Username = "owner", Email = "o@test.ch", FullName = "Owner", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = otherId, Username = "other", Email = "o2@test.ch", FullName = "Other", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = tenant1, Username = "t1", Email = "t1@test.ch", FullName = "T1", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = tenant2, Username = "t2", Email = "t2@test.ch", FullName = "T2", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        var publicProperty = new Property { Name = "Public", IsPrivate = false, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow };
        var privateOther = new Property { Name = "Private Other", IsPrivate = true, LastEditedById = otherId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow };
        context.Properties.AddRange(publicProperty, privateOther);
        context.SaveChanges();

        var leasePublic = new Lease { PropertyId = publicProperty.Id, TenantId = tenant1, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow };
        var leasePrivate = new Lease { PropertyId = privateOther.Id, TenantId = tenant2, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow };
        context.Leases.AddRange(leasePublic, leasePrivate);
        context.SaveChanges();

        context.Payments.AddRange(
            new Payment { LeaseId = leasePublic.Id, Amount = 100, Status = PaymentStatus.Received, ForPeriod = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new Payment { LeaseId = leasePrivate.Id, Amount = 200, Status = PaymentStatus.Received, ForPeriod = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var result = context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .VisibleToUser(ownerId, false, false)
            .ToList();

        Assert.Single(result);
        Assert.Equal(100, result[0].Amount);
    }
}
