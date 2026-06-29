using Microsoft.AspNetCore.Mvc;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using RentTracker.Web.Services;

namespace RentTracker.Web.ViewComponents;

public class NotificationCountViewComponent : ViewComponent
{
    private readonly IPaymentReminderService _reminderService;

    public NotificationCountViewComponent(IPaymentReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var isAdmin = UserClaimsPrincipal.IsInRole(UserRoles.Administrator);
        var isOwner = UserClaimsPrincipal.IsInRole(UserRoles.Owner);

        if (!isAdmin && !isOwner)
        {
            return Content(string.Empty);
        }

        var userId = AuthorizationHelper.GetCurrentUserId(UserClaimsPrincipal);
        var count = await _reminderService.GetReminderCountAsync(userId, isAdmin, isOwner);

        return View(count);
    }
}
