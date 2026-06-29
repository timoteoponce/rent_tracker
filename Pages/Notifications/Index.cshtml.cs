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

    public IndexModel(IPaymentReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    public List<PaymentReminder> Upcoming { get; set; } = new();
    public List<PaymentReminder> Overdue { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        var reminders = await _reminderService.GetRemindersForUserAsync(userId, isAdmin, isOwner: true);

        Upcoming = reminders.Where(r => r.Type == "Upcoming").ToList();
        Overdue = reminders.Where(r => r.Type == "Overdue").ToList();
    }
}
