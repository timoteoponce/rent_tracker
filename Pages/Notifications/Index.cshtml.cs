using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using RentTracker.Web.Services;

namespace RentTracker.Web.Pages.Notifications;

[Authorize(Roles = "Administrator,Owner")]
public class IndexModel : PageModel
{
    private readonly IPaymentReminderService _reminderService;
    private readonly INotificationService _notificationService;

    public IndexModel(IPaymentReminderService reminderService, INotificationService notificationService)
    {
        _reminderService = reminderService;
        _notificationService = notificationService;
    }

    public List<PaymentReminder> Upcoming { get; set; } = new();
    public List<PaymentReminder> Overdue { get; set; } = new();
    public List<WhatsAppNotificationLogEntry> WhatsAppHistory { get; set; } = new();
    public bool WhatsAppEnabled { get; set; }
    public DateTimeOffset? LastWhatsAppRunAt { get; set; }
    public DateTimeOffset? LastWhatsAppRunAtLocal { get; set; }

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        var reminders = await _reminderService.GetRemindersForUserAsync(userId, isAdmin, isOwner: true);

        Upcoming = reminders.Where(r => r.Type == "Upcoming").ToList();
        Overdue = reminders.Where(r => r.Type == "Overdue").ToList();

        var settings = await _notificationService.GetSettingsAsync();
        WhatsAppEnabled = settings?.IsEnabled ?? false;
        LastWhatsAppRunAt = settings?.LastNotificationRunDate;
        if (LastWhatsAppRunAt.HasValue)
        {
            var timeZoneOffset = TimeSpan.FromHours(settings?.TimeZoneOffset ?? -4);
            LastWhatsAppRunAtLocal = LastWhatsAppRunAt.Value.ToOffset(timeZoneOffset);
        }

        WhatsAppHistory = await _notificationService.GetNotificationHistoryAsync(userId, isAdmin);
    }
}
