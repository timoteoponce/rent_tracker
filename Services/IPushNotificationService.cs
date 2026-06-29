using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public interface IPushNotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, string? url = null, CancellationToken ct = default);
    Task SendNotificationToAdminOwnersAsync(string title, string message, string? url = null, CancellationToken ct = default);
    Task<bool> SubscribeAsync(Guid userId, PushSubscriptionDto subscription);
    Task UnsubscribeAsync(Guid userId, string endpoint);
    string? GetPublicKey();
}
