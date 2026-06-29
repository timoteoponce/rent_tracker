using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public class PaymentReminderService : IPaymentReminderService
{
    private readonly RentTrackerDbContext _context;

    public PaymentReminderService(RentTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<List<PaymentReminder>> GetRemindersForUserAsync(Guid? userId, bool isAdmin, bool isOwner)
    {
        var settings = await _context.WhatsAppSettings.FirstOrDefaultAsync();
        var daysBefore = settings?.DueSoonDaysBefore ?? 3;

        var today = DateTimeOffset.UtcNow.Date;
        var upcomingCutoff = today.AddDays(daysBefore);
        var currentPeriod = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var leases = await _context.Leases
            .AsNoTracking()
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .VisibleToUser(userId, isAdmin, false)
            .ToListAsync();

        var reminders = new List<PaymentReminder>();

        foreach (var lease in leases)
        {
            var startMonth = new DateTimeOffset(lease.StartDate.Year, lease.StartDate.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var month = startMonth;

            // Include the next upcoming period so reminders appear before the due date arrives.
            while (month <= currentPeriod.AddMonths(1))
            {
                var dueDay = ClampDayToMonth(month.Year, month.Month, lease.PaymentDueDay);
                var dueDate = new DateTimeOffset(month.Year, month.Month, dueDay, 0, 0, 0, TimeSpan.Zero);

                if (await HasReceivedPaymentForPeriodAsync(lease.Id, month))
                {
                    month = month.AddMonths(1);
                    continue;
                }

                if (dueDate < today)
                {
                    reminders.Add(CreateReminder("Overdue", lease, month, dueDate, (today - dueDate).Days));
                }
                else if (dueDate <= upcomingCutoff)
                {
                    reminders.Add(CreateReminder("Upcoming", lease, month, dueDate, (dueDate - today).Days));
                }

                month = month.AddMonths(1);
            }
        }

        return reminders
            .OrderBy(r => r.Type == "Overdue" ? 0 : 1)
            .ThenBy(r => r.DueDate)
            .ToList();
    }

    public async Task<int> GetReminderCountAsync(Guid? userId, bool isAdmin, bool isOwner)
    {
        var reminders = await GetRemindersForUserAsync(userId, isAdmin, isOwner);
        return reminders.Count;
    }

    private static PaymentReminder CreateReminder(string type, Lease lease, DateTimeOffset forPeriod, DateTimeOffset dueDate, int days)
    {
        return new PaymentReminder
        {
            Type = type,
            LeaseId = lease.Id,
            PropertyName = lease.Property?.Name ?? "Unknown Property",
            TenantName = lease.Tenant?.FullName ?? "Unknown Tenant",
            Amount = lease.AgreedPrice,
            ForPeriod = forPeriod,
            DueDate = dueDate,
            Days = days
        };
    }

    private async Task<bool> HasReceivedPaymentForPeriodAsync(Guid leaseId, DateTimeOffset forPeriod)
    {
        var payments = await _context.Payments
            .Where(p => p.LeaseId == leaseId)
            .ToListAsync();

        return payments.Any(p => p.ForPeriod.Year == forPeriod.Year &&
                                p.ForPeriod.Month == forPeriod.Month &&
                                (p.Status == PaymentStatus.Received || p.Status == PaymentStatus.Partial));
    }

    private static int ClampDayToMonth(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return Math.Min(day, daysInMonth);
    }
}
