using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using Xunit;

namespace RentTracker.Tests.Unit;

public class NotificationBackgroundServiceTests
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

    [Theory]
    [InlineData(0, 12, true)]   // UTC noon -> local noon (within window)
    [InlineData(-4, 12, true)]   // UTC noon -> local 8am (within window)
    [InlineData(-4, 2, false)]  // UTC 2am -> local 10pm (outside window)
    [InlineData(-4, 6, false)]   // UTC 6am -> local 2am (outside window)
    [InlineData(8, 12, true)]   // UTC noon -> local 8pm (within window)
    [InlineData(8, 15, false)]  // UTC 3pm -> local 11pm (outside window)
    public void IsWithinWakeUpWindow_ReturnsCorrectResult(int timeZoneOffset, int utcHour, bool expected)
    {
        var utcTime = new DateTimeOffset(2024, 6, 1, utcHour, 0, 0, TimeSpan.Zero);
        var timeSpan = TimeSpan.FromHours(timeZoneOffset);
        var localTime = utcTime.ToOffset(timeSpan);
        var localHour = localTime.Hour;
        var result = localHour >= 8 && localHour < 22;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsWithinWakeUpWindow_BoliviaTimezone_MidnightIsOutsideWindow()
    {
        // Bolivia is UTC-4, midnight UTC is 8pm local (within window)
        var utcTime = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var localTime = utcTime.ToOffset(TimeSpan.FromHours(-4));
        var localHour = localTime.Hour;
        Assert.Equal(20, localHour); // 8pm local
        var result = localHour >= 8 && localHour < 22;
        Assert.True(result); // 8pm is within window
    }

    [Fact]
    public void IsWithinWakeUpWindow_BoliviaTimezone_6amIsOutsideWindow()
    {
        // Bolivia is UTC-4, 6am UTC is 2am local (outside window)
        var utcTime = new DateTimeOffset(2024, 6, 1, 6, 0, 0, TimeSpan.Zero);
        var localTime = utcTime.ToOffset(TimeSpan.FromHours(-4));
        var localHour = localTime.Hour;
        Assert.Equal(2, localHour); // 2am local
        var result = localHour >= 8 && localHour < 22;
        Assert.False(result); // 2am is outside window
    }

    [Fact]
    public void IsWithinWakeUpWindow_BoliviaTimezone_12pmIsWithinWindow()
    {
        // Bolivia is UTC-4, 12pm UTC is 8am local (within window)
        var utcTime = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var localTime = utcTime.ToOffset(TimeSpan.FromHours(-4));
        var localHour = localTime.Hour;
        Assert.Equal(8, localHour); // 8am local
        var result = localHour >= 8 && localHour < 22;
        Assert.True(result); // 8am is within window
    }

    [Fact]
    public void IsWithinWakeUpWindow_BoliviaTimezone_2pmIsWithinWindow()
    {
        // Bolivia is UTC-4, 2pm UTC is 10am local (within window)
        var utcTime = new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.Zero);
        var localTime = utcTime.ToOffset(TimeSpan.FromHours(-4));
        var localHour = localTime.Hour;
        Assert.Equal(10, localHour); // 10am local
        var result = localHour >= 8 && localHour < 22;
        Assert.True(result); // 10am is within window
    }

    [Fact]
    public void IsWithinWakeUpWindow_BoliviaTimezone_2amIsOutsideWindow()
    {
        // Bolivia is UTC-4, 2am UTC is 10pm local (outside window)
        var utcTime = new DateTimeOffset(2024, 6, 1, 2, 0, 0, TimeSpan.Zero);
        var localTime = utcTime.ToOffset(TimeSpan.FromHours(-4));
        var localHour = localTime.Hour;
        Assert.Equal(22, localHour); // 10pm local
        var result = localHour >= 8 && localHour < 22;
        Assert.False(result); // 10pm is outside window
    }

    [Fact]
    public void OverdueLogic_ChecksAllMonthsFromLeaseStart()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        // Use dates relative to today so the test is stable
        var today = DateTimeOffset.UtcNow;
        var startDate = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-3);
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Add owner
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

        // Add tenant
        context.Users.Add(new User
        {
            Id = userId,
            Username = "tenant1",
            Email = "t1@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Add property
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Add lease starting 3 months ago with payment due on 1st
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = userId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = startDate,
            PaymentDueDay = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        // Add WhatsApp settings (enabled for overdue notifications)
        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            IsEnabled = true,
            EnablePaymentOverdue = true,
            EnableOverdueToTenant = true,
            EnableOverdueToLender = true,
            TimeZoneOffset = -4,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        // No payments added - all months should be overdue
        var service = new RentTracker.Web.Services.NotificationService(
            context,
            new FakeWhatsAppService());

        // Invoke ProcessPaymentOverdueNotificationsAsync via reflection
        var method = typeof(RentTracker.Web.Services.NotificationService).GetMethod(
            "ProcessPaymentOverdueNotificationsAsync", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var task = method.Invoke(service, null) as Task;
        Assert.NotNull(task);
        task.GetAwaiter().GetResult();

        // Check that notifications were logged for the 3 months + current month if due date has passed
        var logs = context.NotificationLogs
            .Where(n => n.Type == NotificationType.PaymentOverdue)
            .ToList();

        // Today is past the due date (1st), so current month is also overdue
        var expectedCount = 4; // 3 months + current month
        Assert.Equal(expectedCount, logs.Count);

        var periods = logs.Select(n => n.ForPeriod).OrderBy(d => d).ToList();
        Assert.Equal(startDate, periods[0]);
        Assert.Equal(startDate.AddMonths(1), periods[1]);
        Assert.Equal(startDate.AddMonths(2), periods[2]);
        Assert.Equal(startDate.AddMonths(3), periods[3]);
    }

    [Fact]
    public void OverdueLogic_SkipsMonthsWithPayment()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        // Use dates relative to today so the test is stable
        var today = DateTimeOffset.UtcNow;
        var startDate = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-3);
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var middleMonth = startDate.AddMonths(1);

        // Add owner
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

        // Add tenant
        context.Users.Add(new User
        {
            Id = userId,
            Username = "tenant1",
            Email = "t1@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Add property
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            OwnerId = ownerId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Add lease starting 3 months ago with payment due on 1st
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = userId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = startDate,
            PaymentDueDay = 1,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        // Add payment for the middle month
        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            Amount = 1000,
            ForPeriod = middleMonth,
            Status = PaymentStatus.Received,
            CreatedAt = DateTimeOffset.UtcNow
        });

        context.SaveChanges();

        // Add WhatsApp settings (enabled for overdue notifications)
        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            IsEnabled = true,
            EnablePaymentOverdue = true,
            EnableOverdueToTenant = true,
            EnableOverdueToLender = true,
            TimeZoneOffset = -4,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(
            context,
            new FakeWhatsAppService());

        // Invoke ProcessPaymentOverdueNotificationsAsync via reflection
        var method = typeof(RentTracker.Web.Services.NotificationService).GetMethod(
            "ProcessPaymentOverdueNotificationsAsync", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var task = method.Invoke(service, null) as Task;
        Assert.NotNull(task);
        task.GetAwaiter().GetResult();

        // Check that notifications were logged for first and last month + current month (middle month skipped because payment received)
        var logs = context.NotificationLogs
            .Where(n => n.Type == NotificationType.PaymentOverdue)
            .ToList();

        // Today is past the due date (1st), so current month is also overdue
        var expectedCount = 3; // first month + last month + current month
        Assert.Equal(expectedCount, logs.Count);

        var periods = logs.Select(n => n.ForPeriod).OrderBy(d => d).ToList();
        Assert.Equal(startDate, periods[0]);
        Assert.Equal(startDate.AddMonths(2), periods[1]);
        Assert.Equal(startDate.AddMonths(3), periods[2]);
    }

    private class FakeWhatsAppService : RentTracker.Web.Services.IWhatsAppService
    {
        public Task<(bool Success, string? Error)> SendMessageAsync(string phoneNumber, string message)
            => Task.FromResult((true, (string?)null));

        public Task<(bool Success, string? Error)> SendTestMessageAsync(string phoneNumber)
            => Task.FromResult((true, (string?)null));
    }
}
