using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;

namespace RentTracker.Web.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var context = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();

                var settings = await context.WhatsAppSettings.FirstOrDefaultAsync(stoppingToken);
                var today = DateTimeOffset.UtcNow.Date;

                // Check if we're within the wake-up window (8:00 - 22:00 local time)
                var timeZoneOffset = TimeSpan.FromHours(settings?.TimeZoneOffset ?? -4);
                var localTime = DateTimeOffset.UtcNow.ToOffset(timeZoneOffset);
                var localHour = localTime.Hour;
                if (localHour < 8 || localHour >= 22)
                {
                    _logger.LogInformation("Local time is {LocalTime} (hour {LocalHour}), outside wake-up window (8:00-22:00). Skipping notification check.", localTime, localHour);
                }
                else if (settings?.LastNotificationRunDate?.Date == today)
                {
                    _logger.LogInformation("Notifications already ran today at {Time}. Skipping.", settings.LastNotificationRunDate);
                }
                else
                {
                    _logger.LogInformation("Running notification check at {Time} (local time: {LocalTime})", DateTimeOffset.UtcNow, localTime);

                    await notificationService.ProcessPaymentDueSoonNotificationsAsync();
                    await notificationService.ProcessPaymentTodayNotificationsAsync();
                    await notificationService.ProcessPaymentOverdueNotificationsAsync();

                    if (settings != null)
                    {
                        settings.LastNotificationRunDate = DateTimeOffset.UtcNow;
                        await context.SaveChangesAsync(stoppingToken);
                    }

                    _logger.LogInformation("Notification check completed at {Time}", DateTimeOffset.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running notification check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("NotificationBackgroundService stopping");
    }
}
