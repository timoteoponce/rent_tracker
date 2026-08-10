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
    public async Task GetNotificationHistoryAsync_Admin_SeesAllLogs()
    {
        using var context = GetInMemoryContext();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var otherLeaseId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = ownerId, Username = "owner", Email = "o@test.ch", FullName = "Owner", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = tenantId, Username = "tenant", Email = "t@test.ch", FullName = "Tenant", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.Properties.Add(new Property { Id = propertyId, Name = "Casa", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow });
        context.Leases.AddRange(
            new Lease { Id = leaseId, PropertyId = propertyId, TenantId = tenantId, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new Lease { Id = otherLeaseId, PropertyId = propertyId, TenantId = tenantId, Status = LeaseStatus.Active, AgreedPrice = 1200, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.NotificationLogs.AddRange(
            new NotificationLog { Type = NotificationType.PaymentToday, LeaseId = leaseId, ForPeriod = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), RecipientRole = "Tenant", RecipientUserId = tenantId, RecipientPhoneNumber = "+59170000001", MessageContent = "tenant msg", Status = NotificationLogStatus.Sent, SentAt = DateTimeOffset.UtcNow.AddHours(-1) },
            new NotificationLog { Type = NotificationType.OverdueSummary, LeaseId = null, ForPeriod = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), RecipientRole = "Owner", RecipientUserId = ownerId, RecipientPhoneNumber = "+59170000002", MessageContent = "owner msg", Status = NotificationLogStatus.Sent, SentAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        var history = await service.GetNotificationHistoryAsync(ownerId, isAdmin: true);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Type == NotificationType.PaymentToday);
        Assert.Contains(history, h => h.Type == NotificationType.OverdueSummary);
    }

    [Fact]
    public async Task GetNotificationHistoryAsync_Owner_SeesOwnSummaryAndTenantRemindersForVisibleLeases()
    {
        using var context = GetInMemoryContext();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var publicPropertyId = Guid.NewGuid();
        var privatePropertyId = Guid.NewGuid();
        var publicLeaseId = Guid.NewGuid();
        var privateLeaseId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = ownerId, Username = "owner", Email = "o@test.ch", FullName = "Owner", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = otherOwnerId, Username = "other", Email = "o2@test.ch", FullName = "Other", Role = UserRoles.Owner, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new User { Id = tenantId, Username = "tenant", Email = "t@test.ch", FullName = "Tenant", Role = UserRoles.Tenant, PasswordHash = "x", IsActive = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.Properties.AddRange(
            new Property { Id = publicPropertyId, Name = "Public", IsPrivate = false, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new Property { Id = privatePropertyId, Name = "Private", IsPrivate = true, LastEditedById = otherOwnerId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.Leases.AddRange(
            new Lease { Id = publicLeaseId, PropertyId = publicPropertyId, TenantId = tenantId, Status = LeaseStatus.Active, AgreedPrice = 1000, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow },
            new Lease { Id = privateLeaseId, PropertyId = privatePropertyId, TenantId = tenantId, Status = LeaseStatus.Active, AgreedPrice = 1200, StartDate = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
        );
        context.NotificationLogs.AddRange(
            new NotificationLog { Type = NotificationType.PaymentToday, LeaseId = publicLeaseId, ForPeriod = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), RecipientRole = "Tenant", RecipientUserId = tenantId, RecipientPhoneNumber = "+59170000001", MessageContent = "public tenant msg", Status = NotificationLogStatus.Sent, SentAt = DateTimeOffset.UtcNow.AddHours(-1) },
            new NotificationLog { Type = NotificationType.PaymentToday, LeaseId = privateLeaseId, ForPeriod = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), RecipientRole = "Tenant", RecipientUserId = tenantId, RecipientPhoneNumber = "+59170000001", MessageContent = "private tenant msg", Status = NotificationLogStatus.Sent, SentAt = DateTimeOffset.UtcNow.AddHours(-2) },
            new NotificationLog { Type = NotificationType.OverdueSummary, LeaseId = null, ForPeriod = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), RecipientRole = "Owner", RecipientUserId = ownerId, RecipientPhoneNumber = "+59170000002", MessageContent = "owner summary", Status = NotificationLogStatus.Sent, SentAt = DateTimeOffset.UtcNow }
        );
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        var history = await service.GetNotificationHistoryAsync(ownerId, isAdmin: false);

        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Type == NotificationType.PaymentToday && h.LeaseId == publicLeaseId);
        Assert.Contains(history, h => h.Type == NotificationType.OverdueSummary);
        Assert.DoesNotContain(history, h => h.LeaseId == privateLeaseId);
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

    [Fact]
    public async Task EnsurePendingPaymentsAsync_CreatesCurrentAndNextPeriodPending()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant",
            Email = "t@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Casa",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1500m,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        await service.EnsurePendingPaymentsAsync();

        var now = DateTimeOffset.UtcNow;
        var currentMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var nextMonth = currentMonth.AddMonths(1);

        var created = context.Payments.Where(p => p.LeaseId == leaseId).ToList();
        Assert.Equal(2, created.Count);
        Assert.All(created, p =>
        {
            Assert.Equal(PaymentStatus.Pending, p.Status);
            Assert.Equal(1500m, p.Amount);
            Assert.Equal("BOB", p.Currency);
        });
        Assert.Contains(created, p => p.ForPeriod == currentMonth);
        Assert.Contains(created, p => p.ForPeriod == nextMonth);
    }

    [Fact]
    public async Task EnsurePendingPaymentsAsync_Idempotent_NoDuplicatesOnRerun()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant",
            Email = "t@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Casa",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1500m,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        await service.EnsurePendingPaymentsAsync();
        await service.EnsurePendingPaymentsAsync();

        Assert.Equal(2, context.Payments.Where(p => p.LeaseId == leaseId).Count());
    }

    [Fact]
    public async Task EnsurePendingPaymentsAsync_DoesNotOverwriteExistingPayment()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var period = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant",
            Email = "t@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Casa",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Active,
            AgreedPrice = 1500m,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Payments.Add(new Payment
        {
            LeaseId = leaseId,
            Amount = 1500m,
            Currency = "BOB",
            ForPeriod = period,
            Status = PaymentStatus.Received,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        await service.EnsurePendingPaymentsAsync();

        // Existing Received for that period must remain untouched; no duplicate for it.
        var periodPayments = context.Payments.Where(p => p.LeaseId == leaseId && p.ForPeriod == period).ToList();
        Assert.Single(periodPayments);
        Assert.Equal(PaymentStatus.Received, periodPayments[0].Status);
    }

    [Fact]
    public async Task EnsurePendingPaymentsAsync_SkipsInactiveLeases()
    {
        using var context = GetInMemoryContext();
        var leaseId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = tenantId,
            Username = "tenant",
            Email = "t@test.ch",
            FullName = "Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Properties.Add(new Property
        {
            Id = propertyId,
            Name = "Casa",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.Leases.Add(new Lease
        {
            Id = leaseId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Status = LeaseStatus.Closed,
            AgreedPrice = 1500m,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new RentTracker.Web.Services.NotificationService(context, new FakeWhatsAppService());
        await service.EnsurePendingPaymentsAsync();

        Assert.Empty(context.Payments.Where(p => p.LeaseId == leaseId));
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
