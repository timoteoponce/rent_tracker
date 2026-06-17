using RentTracker.Web.Services;

namespace RentTracker.Web.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

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

                _logger.LogInformation("Running notification check at {Time}", DateTimeOffset.UtcNow);

                await notificationService.ProcessPaymentDueSoonNotificationsAsync();
                await notificationService.ProcessPaymentTodayNotificationsAsync();
                await notificationService.ProcessPaymentOverdueNotificationsAsync();

                _logger.LogInformation("Notification check completed at {Time}", DateTimeOffset.UtcNow);
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
