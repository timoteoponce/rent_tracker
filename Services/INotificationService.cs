using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public interface INotificationService
{
    Task ProcessPaymentDueSoonNotificationsAsync();
    Task ProcessPaymentTodayNotificationsAsync();
    Task ProcessPaymentOverdueNotificationsAsync();
    Task EnsurePendingPaymentsAsync();
    Task<NotificationDryRunResult> ProcessDryRunAsync(string testPhoneNumber);
    Task<WhatsAppSettings?> GetSettingsAsync();
    Task SaveSettingsAsync(WhatsAppSettings settings);
    Task<List<WhatsAppNotificationLogEntry>> GetNotificationHistoryAsync(Guid? userId, bool isAdmin, int limit = 100);
}
