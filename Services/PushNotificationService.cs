using System.Net;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using WebPush;

namespace RentTracker.Web.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly RentTrackerDbContext _context;
    private readonly VapidKeys _vapidKeys;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        RentTrackerDbContext context,
        VapidKeys vapidKeys,
        ILogger<PushNotificationService> logger)
    {
        _context = context;
        _vapidKeys = vapidKeys;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, string? url = null, CancellationToken ct = default)
    {
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (!subscriptions.Any())
        {
            return;
        }

        var payload = BuildPayload(title, message, url);

        foreach (Models.PushSubscription subscription in subscriptions)
        {
            await SendToSubscriptionAsync(subscription, payload, ct);
        }
    }

    public async Task SendNotificationToAdminOwnersAsync(string title, string message, string? url = null, CancellationToken ct = default)
    {
        var adminOwnerIds = await _context.Users
            .Where(u => u.IsActive && (u.Role == UserRoles.Administrator || u.Role == UserRoles.Owner))
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in adminOwnerIds)
        {
            await SendNotificationAsync(userId, title, message, url, ct);
        }
    }

    public async Task<bool> SubscribeAsync(Guid userId, PushSubscriptionDto subscription)
    {
        if (string.IsNullOrWhiteSpace(subscription.Endpoint) ||
            string.IsNullOrWhiteSpace(subscription.P256dh) ||
            string.IsNullOrWhiteSpace(subscription.Auth))
        {
            return false;
        }

        var existing = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == subscription.Endpoint);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.P256dh = subscription.P256dh;
            existing.Auth = subscription.Auth;
            existing.LastUsedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _context.PushSubscriptions.Add(new Models.PushSubscription
            {
                UserId = userId,
                Endpoint = subscription.Endpoint,
                P256dh = subscription.P256dh,
                Auth = subscription.Auth,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint)
    {
        var existing = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId);

        if (existing != null)
        {
            _context.PushSubscriptions.Remove(existing);
            await _context.SaveChangesAsync();
        }
    }

    public string? GetPublicKey() => _vapidKeys.PublicKey;

    private async Task SendToSubscriptionAsync(Models.PushSubscription subscription, string payload, CancellationToken ct)
    {
        var webPushSub = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        using var client = new WebPushClient();

        try
        {
            await client.SendNotificationAsync(
                webPushSub,
                payload,
                new VapidDetails(_vapidKeys.Subject, _vapidKeys.PublicKey, _vapidKeys.PrivateKey),
                ct);

            subscription.LastUsedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
        catch (WebPushException ex)
        {
            _logger.LogWarning(ex, "Failed to send push notification to {Endpoint}. Status: {StatusCode}", subscription.Endpoint, ex.StatusCode);

            // Subscription is no longer valid — remove it
            if (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _context.PushSubscriptions.Remove(subscription);
                await _context.SaveChangesAsync(ct);
            }
        }
    }

    private static string BuildPayload(string title, string message, string? url)
    {
        var payload = new { title, message, url, timestamp = DateTimeOffset.UtcNow };
        return System.Text.Json.JsonSerializer.Serialize(payload);
    }
}
