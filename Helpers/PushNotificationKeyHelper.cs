using WebPush;

namespace RentTracker.Web.Helpers;

/// <summary>
/// Resolves VAPID keys for Web Push notifications.
/// In development, keys are auto-generated and persisted to disk.
/// In production, keys must be supplied via environment variables or configuration.
/// </summary>
public static class PushNotificationKeyHelper
{
    public static VapidKeys ResolveKeys(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var publicKey = configuration["PushNotifications:PublicKey"];
        var privateKey = configuration["PushNotifications:PrivateKey"];
        var keyPath = Path.Combine(environment.ContentRootPath, "data", "push-vapid.txt");

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            if (File.Exists(keyPath))
            {
                var lines = File.ReadAllLines(keyPath)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains('='))
                    .ToList();

                publicKey = GetValue(lines, "PublicKey") ?? publicKey;
                privateKey = GetValue(lines, "PrivateKey") ?? privateKey;
            }
        }

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            if (environment.IsDevelopment())
            {
                var generated = VapidHelper.GenerateVapidKeys();
                publicKey = generated.PublicKey;
                privateKey = generated.PrivateKey;

                var dataDir = Path.Combine(environment.ContentRootPath, "data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                }

                File.WriteAllLines(keyPath, new[]
                {
                    $"PublicKey={publicKey}",
                    $"PrivateKey={privateKey}"
                });
            }
            else
            {
                throw new InvalidOperationException(
                    "Push notification VAPID keys are missing. " +
                    "Set 'PushNotifications__PublicKey', 'PushNotifications__PrivateKey', and 'PushNotifications__Subject' environment variables, " +
                    "or mount a persistent volume with 'data/push-vapid.txt'.");
            }
        }

        var subject = configuration["PushNotifications:Subject"];
        if (string.IsNullOrWhiteSpace(subject))
        {
            subject = environment.IsDevelopment()
                ? "mailto:renttracker@localhost"
                : throw new InvalidOperationException("PushNotifications:Subject must be configured in production (e.g., mailto:admin@example.com or https://your-domain).");
        }

        return new VapidKeys(publicKey, privateKey, subject);
    }

    private static string? GetValue(List<string> lines, string key)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
        return line?[(line.IndexOf('=') + 1)..];
    }
}

public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);
