using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Reports;

[Authorize(Roles = "Administrator,Owner")]
public class DetailedModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public DetailedModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public DateTimeOffset? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTimeOffset? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? PropertyId { get; set; }

    public SelectList Properties { get; set; } = new(Enumerable.Empty<object>());

    // Summary Stats
    public decimal TotalRevenue { get; set; }
    public int TotalPayments { get; set; }
    public decimal AveragePayment { get; set; }

    // Breakdown Data
    public List<PropertyBreakdownItem> PropertyBreakdown { get; set; } = new();
    public List<MonthlyBreakdownItem> MonthlyBreakdown { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Set default date range (last 12 months)
        if (!StartDate.HasValue)
        {
            StartDate = DateTimeOffset.UtcNow.AddMonths(-12);
        }
        if (!EndDate.HasValue)
        {
            EndDate = DateTimeOffset.UtcNow;
        }

        // Load properties dropdown
        var properties = await _context.Properties
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync();
        Properties = new SelectList(properties, "Id", "Name");

        // Get filtered payments
        var paymentsQuery = _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Where(p => p.ForPeriod >= StartDate && p.ForPeriod <= EndDate);

        if (PropertyId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Lease.PropertyId == PropertyId.Value);
        }

        var payments = await paymentsQuery.ToListAsync();

        // Calculate summary stats
        TotalPayments = payments.Count;
        TotalRevenue = payments.Sum(p => p.Amount);
        AveragePayment = TotalPayments > 0 ? TotalRevenue / TotalPayments : 0;

        // Property breakdown
        PropertyBreakdown = payments
            .GroupBy(p => p.Lease.Property.Name)
            .Select(g => new PropertyBreakdownItem
            {
                PropertyName = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(p => p.Amount),
                AverageAmount = g.Average(p => p.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        // Monthly breakdown
        MonthlyBreakdown = payments
            .GroupBy(p => new { p.ForPeriod.Year, p.ForPeriod.Month })
            .Select(g => new MonthlyBreakdownItem
            {
                Month = new DateTimeOffset(new DateTime(g.Key.Year, g.Key.Month, 1)),
                PaymentCount = g.Count(),
                TotalRevenue = g.Sum(p => p.Amount),
                PendingCount = g.Count(p => p.Status == PaymentStatus.Pending),
                ReceivedCount = g.Count(p => p.Status == PaymentStatus.Received),
                LateCount = g.Count(p => p.Status == PaymentStatus.Late)
            })
            .OrderBy(x => x.Month)
            .ToList();
    }

    public class PropertyBreakdownItem
    {
        public string PropertyName { get; set; } = string.Empty;
        public int PaymentCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
    }

    public class MonthlyBreakdownItem
    {
        public DateTimeOffset Month { get; set; }
        public int PaymentCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingCount { get; set; }
        public int ReceivedCount { get; set; }
        public int LateCount { get; set; }
    }
}
