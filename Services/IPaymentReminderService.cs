using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public interface IPaymentReminderService
{
    Task<List<PaymentReminder>> GetRemindersForUserAsync(Guid? userId, bool isAdmin, bool isOwner);
    Task<int> GetReminderCountAsync(Guid? userId, bool isAdmin, bool isOwner);
}
