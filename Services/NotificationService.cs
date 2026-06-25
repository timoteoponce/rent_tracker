using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public class NotificationService : INotificationService
{
    private readonly RentTrackerDbContext _context;
    private readonly IWhatsAppService _whatsAppService;

    public NotificationService(RentTrackerDbContext context, IWhatsAppService whatsAppService)
    {
        _context = context;
        _whatsAppService = whatsAppService;
    }

    public async Task<WhatsAppSettings?> GetSettingsAsync()
    {
        return await _context.WhatsAppSettings.FirstOrDefaultAsync();
    }

    public async Task SaveSettingsAsync(WhatsAppSettings settings)
    {
        var existing = await _context.WhatsAppSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            _context.WhatsAppSettings.Add(settings);
        }
        else
        {
            existing.IsEnabled = settings.IsEnabled;
            existing.Provider = settings.Provider;
            existing.AccessToken = settings.AccessToken;
            existing.PhoneNumberId = settings.PhoneNumberId;
            existing.BusinessAccountId = settings.BusinessAccountId;
            existing.VerifyToken = settings.VerifyToken;
            existing.EnablePaymentDueSoon = settings.EnablePaymentDueSoon;
            existing.EnablePaymentToday = settings.EnablePaymentToday;
            existing.EnablePaymentOverdue = settings.EnablePaymentOverdue;
            existing.EnableOverdueToTenant = settings.EnableOverdueToTenant;
            existing.EnableOverdueToLender = settings.EnableOverdueToLender;
            existing.DueSoonDaysBefore = settings.DueSoonDaysBefore;
            existing.TimeZoneOffset = settings.TimeZoneOffset;
            existing.TestTemplateName = settings.TestTemplateName;
            existing.PaymentDueSoonTemplateName = settings.PaymentDueSoonTemplateName;
            existing.PaymentTodayTemplateName = settings.PaymentTodayTemplateName;
            existing.PaymentOverdueTemplateName = settings.PaymentOverdueTemplateName;
            existing.EnableIncomingBot = settings.EnableIncomingBot;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task ProcessPaymentDueSoonNotificationsAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings == null || !settings.IsEnabled || !settings.EnablePaymentDueSoon)
            return;

        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var allLeases = await _context.Leases
            .Include(l => l.Property).ThenInclude(p => p!.Owner)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        foreach (var lease in allLeases)
        {
            var dueDay = ClampDayToMonth(currentPeriod.Year, currentPeriod.Month, lease.PaymentDueDay);
            var dueDate = new DateTimeOffset(currentPeriod.Year, currentPeriod.Month, dueDay, 0, 0, 0, TimeSpan.Zero);
            var targetDate = dueDate.AddDays(-settings.DueSoonDaysBefore);

            if (targetDate.Date != today.Date)
                continue;

            if (string.IsNullOrEmpty(lease.Tenant.PhoneNumber))
            {
                await LogSkippedNotification(NotificationType.PaymentDueSoon, lease.Id, currentPeriod, "Tenant", lease.TenantId, "No phone number");
                continue;
            }

            if (await HasReceivedPaymentForPeriodAsync(lease.Id, currentPeriod))
                continue;

            if (await HasTodayNotificationAsync(NotificationType.PaymentDueSoon, lease.Id, currentPeriod))
                continue;

            var daysUntilDue = (dueDate.Date - today.Date).Days;
            var propertyName = lease.Property?.Name ?? "the property";
            var parameters = new List<string>
            {
                lease.AgreedPrice.ToString(),
                propertyName,
                daysUntilDue.ToString(),
                dueDate.ToString("dd MMM yyyy")
            };

            var (success, error) = await _whatsAppService.SendTemplateAsync(lease.Tenant.PhoneNumber, settings.PaymentDueSoonTemplateName, parameters);
            var message = $"Template: {settings.PaymentDueSoonTemplateName}, Params: {string.Join(", ", parameters)}";
            await LogNotification(NotificationType.PaymentDueSoon, lease.Id, currentPeriod, "Tenant", lease.TenantId,
                lease.Tenant.PhoneNumber, message, success ? NotificationLogStatus.Sent : NotificationLogStatus.Failed, error);
        }
    }

    public async Task ProcessPaymentTodayNotificationsAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings == null || !settings.IsEnabled || !settings.EnablePaymentToday)
            return;

        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var allLeases = await _context.Leases
            .Include(l => l.Property).ThenInclude(p => p!.Owner)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        foreach (var lease in allLeases)
        {
            var dueDay = ClampDayToMonth(currentPeriod.Year, currentPeriod.Month, lease.PaymentDueDay);
            if (dueDay != today.Day)
                continue;

            if (string.IsNullOrEmpty(lease.Tenant.PhoneNumber))
            {
                await LogSkippedNotification(NotificationType.PaymentToday, lease.Id, currentPeriod, "Tenant", lease.TenantId, "No phone number");
                continue;
            }

            if (await HasReceivedPaymentForPeriodAsync(lease.Id, currentPeriod))
                continue;

            if (await HasTodayNotificationAsync(NotificationType.PaymentToday, lease.Id, currentPeriod))
                continue;

            var dueDate = new DateTimeOffset(currentPeriod.Year, currentPeriod.Month, dueDay, 0, 0, 0, TimeSpan.Zero);
            var propertyName = lease.Property?.Name ?? "the property";
            var parameters = new List<string>
            {
                lease.AgreedPrice.ToString(),
                propertyName,
                dueDate.ToString("dd MMM yyyy")
            };

            var (success, error) = await _whatsAppService.SendTemplateAsync(lease.Tenant.PhoneNumber, settings.PaymentTodayTemplateName, parameters);
            var message = $"Template: {settings.PaymentTodayTemplateName}, Params: {string.Join(", ", parameters)}";
            await LogNotification(NotificationType.PaymentToday, lease.Id, currentPeriod, "Tenant", lease.TenantId,
                lease.Tenant.PhoneNumber, message, success ? NotificationLogStatus.Sent : NotificationLogStatus.Failed, error);
        }
    }

    public async Task ProcessPaymentOverdueNotificationsAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings == null || !settings.IsEnabled || !settings.EnablePaymentOverdue)
            return;

        var today = DateTimeOffset.UtcNow;
        var currentPeriod = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var allLeases = await _context.Leases
            .Include(l => l.Property).ThenInclude(p => p!.Owner)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        var overdueItems = new List<OverdueItem>();
        foreach (var lease in allLeases)
        {
            var prop = lease.Property;
            if (prop == null || prop.OwnerId == null)
                continue;

            // Check all months from lease start to current month for overdue payments
            var startMonth = new DateTimeOffset(lease.StartDate.Year, lease.StartDate.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var month = startMonth;
            while (month <= currentPeriod)
            {
                var dueDay = ClampDayToMonth(month.Year, month.Month, lease.PaymentDueDay);
                var dueDate = new DateTimeOffset(month.Year, month.Month, dueDay, 0, 0, 0, TimeSpan.Zero);
                
                if (dueDate >= today.Date)
                {
                    // Due date is in the future, skip this month
                    month = month.AddMonths(1);
                    continue;
                }

                if (await HasReceivedPaymentForPeriodAsync(lease.Id, month))
                {
                    // Payment received for this month, skip
                    month = month.AddMonths(1);
                    continue;
                }

                overdueItems.Add(new OverdueItem
                {
                    LeaseId = lease.Id,
                    AgreedPrice = lease.AgreedPrice,
                    PaymentDueDay = dueDay,
                    ForPeriod = month,
                    TenantId = lease.TenantId,
                    Tenant = lease.Tenant,
                    OwnerId = prop.OwnerId.Value,
                    Owner = prop.Owner,
                    PropertyName = prop.Name ?? "the property"
                });

                month = month.AddMonths(1);
            }
        }

        foreach (var ownerGroup in overdueItems.GroupBy(x => x.OwnerId))
        {
            var owner = ownerGroup.First().Owner;
            var items = ownerGroup.ToList();

            if (settings.EnableOverdueToTenant)
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.Tenant.PhoneNumber))
                    {
                        await LogSkippedNotification(NotificationType.PaymentOverdue, item.LeaseId, item.ForPeriod, "Tenant", item.TenantId, "No phone number");
                        continue;
                    }

                    if (await HasTodayNotificationAsync(NotificationType.PaymentOverdue, item.LeaseId, item.ForPeriod, item.TenantId))
                        continue;

                    var dueDate = new DateTimeOffset(item.ForPeriod.Year, item.ForPeriod.Month, item.PaymentDueDay, 0, 0, 0, TimeSpan.Zero);
                    var daysOverdue = (today - dueDate).Days;
                    var parameters = new List<string>
                    {
                        item.AgreedPrice.ToString(),
                        item.PropertyName,
                        dueDate.ToString("dd MMM yyyy"),
                        daysOverdue.ToString()
                    };

                    var (success, error) = await _whatsAppService.SendTemplateAsync(item.Tenant.PhoneNumber, settings.PaymentOverdueTemplateName, parameters);
                    var message = $"Template: {settings.PaymentOverdueTemplateName}, Params: {string.Join(", ", parameters)}";
                    await LogNotification(NotificationType.PaymentOverdue, item.LeaseId, item.ForPeriod, "Tenant", item.TenantId,
                        item.Tenant.PhoneNumber, message, success ? NotificationLogStatus.Sent : NotificationLogStatus.Failed, error);
                }
            }

            if (settings.EnableOverdueToLender && !string.IsNullOrEmpty(owner.PhoneNumber))
            {
                if (await HasTodayOwnerSummaryAsync(owner.Id, currentPeriod))
                    continue;

                var overdueCount = items.Count.ToString();
                var overdueList = string.Join("; ", items.Select(x =>
                    string.Format("{0} owes {1} BOB for {2} ({3:MMM yyyy})",
                        x.Tenant.FullName, x.AgreedPrice, x.PropertyName, x.ForPeriod)));

                var summaryParameters = new List<string>
                {
                    today.ToString("dd MMM yyyy"),
                    overdueCount,
                    overdueList
                };

                var (success, error) = await _whatsAppService.SendTemplateAsync(owner.PhoneNumber, settings.OverdueSummaryTemplateName, summaryParameters);
                var summaryMessage = $"Template: {settings.OverdueSummaryTemplateName}, Params: {string.Join(", ", summaryParameters)}";
                await LogNotification(NotificationType.OverdueSummary, null, currentPeriod, "Owner", owner.Id,
                    owner.PhoneNumber, summaryMessage, success ? NotificationLogStatus.Sent : NotificationLogStatus.Failed, error);
            }
        }
    }

    private static int ClampDayToMonth(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return Math.Min(day, daysInMonth);
    }

    private async Task<bool> HasReceivedPaymentForPeriodAsync(Guid leaseId, DateTimeOffset forPeriod)
    {
        var payments = await _context.Payments
            .Where(p => p.LeaseId == leaseId)
            .ToListAsync();

        return payments.Any(p => p.ForPeriod.Year == forPeriod.Year &&
                                p.ForPeriod.Month == forPeriod.Month &&
                                (p.Status == PaymentStatus.Received || p.Status == PaymentStatus.Partial));
    }

    private async Task<bool> HasTodayNotificationAsync(string type, Guid leaseId, DateTimeOffset forPeriod, Guid? recipientUserId = null)
    {
        var todayDate = DateTimeOffset.UtcNow.Date;
        var candidates = await _context.NotificationLogs
            .Where(n => n.Type == type && n.LeaseId == leaseId)
            .ToListAsync();

        return candidates.Any(n =>
            n.ForPeriod.Year == forPeriod.Year &&
            n.ForPeriod.Month == forPeriod.Month &&
            n.SentAt.Date == todayDate &&
            (!recipientUserId.HasValue || n.RecipientUserId == recipientUserId.Value));
    }

    private async Task<bool> HasTodayOwnerSummaryAsync(Guid ownerId, DateTimeOffset forPeriod)
    {
        var todayDate = DateTimeOffset.UtcNow.Date;
        var candidates = await _context.NotificationLogs
            .Where(n => n.Type == NotificationType.OverdueSummary && n.RecipientUserId == ownerId)
            .ToListAsync();

        return candidates.Any(n =>
            n.ForPeriod.Year == forPeriod.Year &&
            n.ForPeriod.Month == forPeriod.Month &&
            n.SentAt.Date == todayDate);
    }

    private async Task LogSkippedNotification(string type, Guid leaseId, DateTimeOffset forPeriod, string recipientRole, Guid recipientUserId, string reason)
    {
        var log = new NotificationLog
        {
            Type = type,
            LeaseId = leaseId,
            ForPeriod = forPeriod,
            RecipientRole = recipientRole,
            RecipientUserId = recipientUserId,
            RecipientPhoneNumber = null,
            MessageContent = "Skipped: " + reason,
            Status = NotificationLogStatus.Failed,
            ErrorMessage = reason,
            SentAt = DateTimeOffset.UtcNow
        };

        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private async Task LogNotification(string type, Guid? leaseId, DateTimeOffset forPeriod, string recipientRole,
        Guid recipientUserId, string? phoneNumber, string message, string status, string? error)
    {
        var log = new NotificationLog
        {
            Type = type,
            LeaseId = leaseId,
            ForPeriod = forPeriod,
            RecipientRole = recipientRole,
            RecipientUserId = recipientUserId,
            RecipientPhoneNumber = phoneNumber,
            MessageContent = message,
            Status = status,
            ErrorMessage = error,
            SentAt = DateTimeOffset.UtcNow
        };

        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    private class OverdueItem
    {
        public Guid LeaseId { get; set; }
        public decimal AgreedPrice { get; set; }
        public int PaymentDueDay { get; set; }
        public DateTimeOffset ForPeriod { get; set; }
        public Guid TenantId { get; set; }
        public User Tenant { get; set; } = null!;
        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;
        public string PropertyName { get; set; } = string.Empty;
    }
}
