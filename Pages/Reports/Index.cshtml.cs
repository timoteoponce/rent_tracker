using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Reports;

[Authorize(Roles = "Administrator,Owner")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    // Property Stats
    public int TotalProperties { get; set; }
    public int OccupiedProperties { get; set; }
    public int AvailableProperties { get; set; }
    public int DisabledProperties { get; set; }

    // Revenue Stats
    public decimal ThisMonthRevenue { get; set; }
    public int CurrentYear { get; set; }
    public List<string> MonthLabels { get; set; } = new();
    public List<decimal> MonthlyRevenue { get; set; } = new();

    // Payment Status
    public List<string> PaymentStatusLabels { get; set; } = new() { "Pending", "Received", "Partial", "Late" };
    public List<int> PaymentStatusCounts { get; set; } = new();

    public async Task OnGetAsync()
    {
        CurrentYear = DateTimeOffset.UtcNow.Year;

        // Property statistics
        var properties = await _context.Properties.ToListAsync();
        TotalProperties = properties.Count;
        DisabledProperties = properties.Count(p => !p.IsEnabled);

        // Count occupied (has active lease)
        var activeLeases = await _context.Leases
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        // Count unique properties with active leases
        var occupiedPropertyIds = activeLeases.Select(l => l.PropertyId).Distinct().ToList();
        OccupiedProperties = occupiedPropertyIds.Count;
        AvailableProperties = TotalProperties - OccupiedProperties - DisabledProperties;

        // Monthly revenue for current year
        MonthLabels = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        
        // NOTE: Fetch all payments and filter client-side because SQLite doesn't support DateTimeOffset.Year in LINQ
        var allPaymentsList = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Received)
            .ToListAsync();
        
        var payments = allPaymentsList
            .Where(p => p.ForPeriod.Year == CurrentYear)
            .ToList();

        MonthlyRevenue = new List<decimal>();
        for (int month = 1; month <= 12; month++)
        {
            var monthRevenue = payments
                .Where(p => p.ForPeriod.Month == month)
                .Sum(p => p.Amount);
            MonthlyRevenue.Add(monthRevenue);
        }

        // This month revenue
        var thisMonth = DateTimeOffset.UtcNow.Month;
        ThisMonthRevenue = payments
            .Where(p => p.ForPeriod.Month == thisMonth)
            .Sum(p => p.Amount);

        // Payment status counts
        var allPayments = await _context.Payments.ToListAsync();
        PaymentStatusCounts = new List<int>
        {
            allPayments.Count(p => p.Status == PaymentStatus.Pending),
            allPayments.Count(p => p.Status == PaymentStatus.Received),
            allPayments.Count(p => p.Status == PaymentStatus.Partial),
            allPayments.Count(p => p.Status == PaymentStatus.Late)
        };
    }
}
