using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentTracker.Web.Data;
using RentTracker.Web.Data.Queries;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Reports;

[Authorize(Roles = "Administrator,Owner")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;
    private readonly ISqlQueryService _sqlQueries;

    public IndexModel(RentTrackerDbContext context, ISqlQueryService sqlQueries)
    {
        _context = context;
        _sqlQueries = sqlQueries;
    }

    // Property Stats
    public int TotalProperties { get; set; }
    public int OccupiedProperties { get; set; }
    public int AvailableProperties { get; set; }
    public int DisabledProperties { get; set; }

    // Revenue Stats
    public decimal ThisMonthRevenue { get; set; }
    public int CurrentYear { get; set; }
    public List<string> MonthLabels { get; set; } = new() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    public List<decimal> MonthlyRevenue { get; set; } = new();

    // Payment Status
    public List<string> PaymentStatusLabels { get; set; } = new() { "Pending", "Received", "Partial", "Late" };
    public List<int> PaymentStatusCounts { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        CurrentYear = DateTimeOffset.UtcNow.Year;

        // Property occupancy statistics via SQL (avoids loading full entity graphs)
        var occupancy = await _sqlQueries.GetPropertyOccupancyStatsAsync(userId, isAdmin);
        TotalProperties = occupancy.TotalProperties;
        OccupiedProperties = occupancy.OccupiedProperties;
        AvailableProperties = occupancy.AvailableProperties;
        DisabledProperties = occupancy.DisabledProperties;

        // Monthly revenue via SQL (avoids client-side DateTimeOffset filtering)
        var monthlyRevenueData = await _sqlQueries.GetMonthlyRevenueAsync(CurrentYear, userId, isAdmin, isTenant);
        MonthlyRevenue = new List<decimal>(new decimal[12]);
        foreach (var item in monthlyRevenueData)
        {
            if (item.Month >= 1 && item.Month <= 12)
            {
                MonthlyRevenue[item.Month - 1] = item.TotalRevenue;
            }
        }

        // This month revenue
        var thisMonth = DateTimeOffset.UtcNow.Month;
        ThisMonthRevenue = monthlyRevenueData
            .Where(r => r.Month == thisMonth)
            .Select(r => r.TotalRevenue)
            .FirstOrDefault();

        // Payment status counts via SQL
        var statusCounts = await _sqlQueries.GetPaymentStatusCountsAsync(userId, isAdmin, isTenant);
        var countsByStatus = statusCounts.ToDictionary(s => s.Status, s => s.Count);
        PaymentStatusCounts = new List<int>
        {
            countsByStatus.GetValueOrDefault(PaymentStatus.Pending),
            countsByStatus.GetValueOrDefault(PaymentStatus.Received),
            countsByStatus.GetValueOrDefault(PaymentStatus.Partial),
            countsByStatus.GetValueOrDefault(PaymentStatus.Late)
        };
    }
}
