using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using RentTracker.Web.Services;
using Xunit;

namespace RentTracker.Tests.Unit;

public class PaymentReminderServiceTests
{
    private RentTrackerDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<RentTrackerDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new RentTrackerDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    [Fact]
    public void GetRemindersForUserAsync_ReturnsOverdueAndUpcoming()
    {
        using var context = GetInMemoryContext();
        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner1",
            Email = "owner1@test.ch",
            FullName = "Owner",
            Role = UserRoles.Owner,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant1",
            Email = "t1@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = currentPeriod.AddMonths(-1),
            PaymentDueDay = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            DueSoonDaysBefore = 5,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        var service = new PaymentReminderService(context);
        var reminders = service.GetRemindersForUserAsync(ownerId, isAdmin: false, isOwner: true).GetAwaiter().GetResult();

        Assert.NotEmpty(reminders);
        Assert.Contains(reminders, r => r.Type == "Overdue");
    }

    [Fact]
    public void GetRemindersForUserAsync_IncludesNextPeriodUpcomingReminder()
    {
        using var context = GetInMemoryContext();
        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner1",
            Email = "owner1@test.ch",
            FullName = "Owner",
            Role = UserRoles.Owner,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant1",
            Email = "t1@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = currentPeriod,
            PaymentDueDay = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Large window so the next due period (current or next month) is always considered upcoming.
        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            DueSoonDaysBefore = 31,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        var service = new PaymentReminderService(context);
        var reminders = service.GetRemindersForUserAsync(ownerId, isAdmin: false, isOwner: true).GetAwaiter().GetResult();

        Assert.Contains(reminders, r => r.Type == "Upcoming");
    }

    [Fact]
    public void GetRemindersForUserAsync_SkipsPaidPeriods()
    {
        using var context = GetInMemoryContext();
        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = ownerId,
            Username = "owner1",
            Email = "owner1@test.ch",
            FullName = "Owner",
            Role = UserRoles.Owner,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant1",
            Email = "t1@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = currentPeriod.AddMonths(-1),
            PaymentDueDay = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.Payments.Add(new Payment
        {
            LeaseId = leaseId,
            Amount = 1000,
            ForPeriod = currentPeriod.AddMonths(-1),
            Status = PaymentStatus.Received,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            DueSoonDaysBefore = 5,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        var service = new PaymentReminderService(context);
        var reminders = service.GetRemindersForUserAsync(ownerId, isAdmin: false, isOwner: true).GetAwaiter().GetResult();

        Assert.DoesNotContain(reminders, r => r.ForPeriod == currentPeriod.AddMonths(-1));
    }
}
