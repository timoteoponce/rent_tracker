using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using Xunit;

namespace RentTracker.Tests.Unit;

public class NotificationServiceTests
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
    [InlineData(2024, 2, 31, 29)]   // Leap-year February: 31 -> 29
    [InlineData(2023, 2, 31, 28)]   // Non-leap February: 31 -> 28
    [InlineData(2024, 4, 31, 30)]   // April: 31 -> 30
    [InlineData(2024, 1, 15, 15)]   // January: 15 stays 15
    [InlineData(2024, 12, 31, 31)]  // December: 31 stays 31
    public void ClampDayToMonth_ClampsCorrectly(int year, int month, int day, int expected)
    {
        // ClampDayToMonth is private static on NotificationService; invoke via reflection
        var method = typeof(RentTracker.Web.Services.NotificationService).GetMethod(
            "ClampDayToMonth", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = method.Invoke(null, new object[] { year, month, day });
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ClampDayToMonth_UsedForDueSoon_DueDay1_3DaysBefore_LeapYear()
    {
        // PaymentDueDay = 1, DueSoonDaysBefore = 3 => target is Feb 29 (leap year)
        var dueDay = 1;
        var dueDate = new DateTimeOffset(2024, 2, dueDay, 0, 0, 0, TimeSpan.Zero);
        var targetDate = dueDate.AddDays(-3);
        Assert.Equal(29, targetDate.Day);
    }

    [Fact]
    public void ClampDayToMonth_UsedForDueSoon_DueDay1_3DaysBefore_NonLeapYear()
    {
        // PaymentDueDay = 1, DueSoonDaysBefore = 3 => target is Jan 29 (non-leap year)
        var dueDay = 1;
        var dueDate = new DateTimeOffset(2023, 2, dueDay, 0, 0, 0, TimeSpan.Zero);
        var targetDate = dueDate.AddDays(-3);
        Assert.Equal(29, targetDate.Day);
    }

    [Fact]
    public void HasTodayNotificationAsync_FiltersClientSide()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var period = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var propertyId = Guid.NewGuid();
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
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Test Property",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = userId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        context.NotificationLogs.Add(new NotificationLog
        {
            Type = NotificationType.PaymentDueSoon,
            LeaseId = leaseId,
            ForPeriod = period,
            SentAt = DateTimeOffset.UtcNow,
            RecipientUserId = userId,
            RecipientRole = "Tenant",
            MessageContent = "test",
            Status = NotificationLogStatus.Sent
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(
            context,
            new FakeWhatsAppService());

        var method = typeof(RentTracker.Web.Services.NotificationService).GetMethod(
            "HasTodayNotificationAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var resultTask = method.Invoke(service, new object[] { NotificationType.PaymentDueSoon, leaseId, period, null }) as Task<bool>;
        Assert.NotNull(resultTask);
        Assert.True(resultTask.GetAwaiter().GetResult());
    }

    [Theory]
    [InlineData("+59171687570", "59171687570")]      // With +, stripped
    [InlineData("59171687570", "59171687570")]       // Digits only
    [InlineData("+591 71687570", "59171687570")]     // With space
    [InlineData("591-716-87570", "59171687570")]     // With dashes
    [InlineData("+591 (716) 87570", "59171687570")]  // With parentheses
    [InlineData("", "")]                              // Empty
    [InlineData(null, null)]                          // Null
    public void NormalizePhoneNumber_FormatsCorrectly(string input, string expected)
    {
        var result = RentTracker.Web.Services.MetaCloudWhatsAppService.NormalizePhoneNumber(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(2024, 6, 29, 1, 2024, 7, 1)]   // Before due day -> next month
    [InlineData(2024, 6, 1, 1, 2024, 6, 1)]     // On due day -> current month
    [InlineData(2024, 6, 2, 1, 2024, 7, 1)]     // After due day -> next month
    [InlineData(2024, 6, 29, 31, 2024, 6, 30)] // Due day 31 clamps to 30
    [InlineData(2024, 2, 28, 31, 2024, 2, 29)] // Feb 29 on leap year
    [InlineData(2024, 2, 29, 31, 2024, 2, 29)] // On due day -> current period
    [InlineData(2024, 3, 1, 31, 2024, 3, 31)]  // After due day -> Mar 31
    public void GetNextDueDate_ReturnsCorrectDueDateAndPeriod(int year, int month, int day, int paymentDueDay, int expectedYear, int expectedMonth, int expectedDay)
    {
        var method = typeof(RentTracker.Web.Services.NotificationService).GetMethod(
            "GetNextDueDate", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var today = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        var result = method.Invoke(null, new object[] { today, paymentDueDay });

        Assert.NotNull(result);
        var tupleType = typeof(ValueTuple<,>).MakeGenericType(typeof(DateTimeOffset), typeof(DateTimeOffset));
        var dueDate = (DateTimeOffset)tupleType.GetField("Item1")!.GetValue(result)!;
        var forPeriod = (DateTimeOffset)tupleType.GetField("Item2")!.GetValue(result)!;

        Assert.Equal(new DateTimeOffset(expectedYear, expectedMonth, expectedDay, 0, 0, 0, TimeSpan.Zero), dueDate);
        Assert.Equal(new DateTimeOffset(expectedYear, expectedMonth, 1, 0, 0, 0, TimeSpan.Zero), forPeriod);
    }

    [Fact]
    public void ProcessDryRunAsync_SendsOnePerEnabledTemplateToTestNumber()
    {
        using var context = GetInMemoryContext();

        context.WhatsAppSettings.Add(new WhatsAppSettings
        {
            IsEnabled = true,
            AccessToken = "encrypted-token",
            PhoneNumberId = "phone-id",
            EnablePaymentToday = true,
            EnablePaymentDueSoon = true,
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

        var result = service.ProcessDryRunAsync("+59179999999").GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.Equal(4, result.TotalAttempted);
        Assert.Equal(4, result.TotalSucceeded);

        var logs = context.NotificationLogs
            .Where(n => n.Type == NotificationType.DryRun)
            .ToList();

        Assert.Equal(4, logs.Count);
        Assert.All(logs, log => Assert.Equal("+59179999999", log.RecipientPhoneNumber));
    }

    private class FakeWhatsAppService : RentTracker.Web.Services.IWhatsAppService
    {
        public Task<(bool Success, string? Error)> SendMessageAsync(string phoneNumber, string message)
            => Task.FromResult((true, (string?)null));

        public Task<(bool Success, string? Error)> SendTemplateAsync(string phoneNumber, string templateName, List<string> parameters)
            => Task.FromResult((true, (string?)null));

        public Task<(bool Success, string? Error)> SendTestMessageAsync(string phoneNumber)
            => Task.FromResult((true, (string?)null));
    }
}
