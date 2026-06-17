using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public interface INotificationService
{
    Task ProcessPaymentDueSoonNotificationsAsync();
    Task ProcessPaymentTodayNotificationsAsync();
    Task ProcessPaymentOverdueNotificationsAsync();
    Task<WhatsAppSettings?> GetSettingsAsync();
    Task SaveSettingsAsync(WhatsAppSettings settings);
    Task<(bool Success, string? Error)> SendTestMessageAsync(string phoneNumber);
}