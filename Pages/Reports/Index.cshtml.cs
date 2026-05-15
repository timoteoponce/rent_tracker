using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
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

    public int TotalProperties { get; set; }
    public int OccupiedProperties { get; set; }
    public int AvailableProperties { get; set; }
    public int DisabledProperties { get; set; }

    public decimal ThisMonthRevenue { get; set; }
    public int CurrentYear { get; set; }
    public List<string> MonthLabels { get; set; } = new() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    public List<decimal> MonthlyRevenue { get; set; } = new();

    public List<string> PaymentStatusLabels { get; set; } = new() { "Pending", "Received", "Partial", "Late" };
    public List<int> PaymentStatusCounts { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        CurrentYear = DateTimeOffset.UtcNow.Year;

        var occupancy = await GetPropertyOccupancyAsync(userId, isAdmin);
        TotalProperties = occupancy.TotalProperties;
        OccupiedProperties = occupancy.OccupiedProperties;
        AvailableProperties = occupancy.AvailableProperties;
        DisabledProperties = occupancy.DisabledProperties;

        var monthlyRevenueData = await GetMonthlyRevenueAsync(CurrentYear, userId, isAdmin, isTenant);
        MonthlyRevenue = new List<decimal>(new decimal[12]);
        foreach (var item in monthlyRevenueData)
        {
            if (item.Month >= 1 && item.Month <= 12)
            {
                MonthlyRevenue[item.Month - 1] = item.TotalRevenue;
            }
        }

        var thisMonth = DateTimeOffset.UtcNow.Month;
        ThisMonthRevenue = monthlyRevenueData
            .Where(r => r.Month == thisMonth)
            .Select(r => r.TotalRevenue)
            .FirstOrDefault();

        var statusCounts = await GetPaymentStatusCountsAsync(userId, isAdmin, isTenant);
        var countsByStatus = statusCounts.ToDictionary(s => s.Status, s => s.Count);
        PaymentStatusCounts = new List<int>
        {
            countsByStatus.GetValueOrDefault(PaymentStatus.Pending),
            countsByStatus.GetValueOrDefault(PaymentStatus.Received),
            countsByStatus.GetValueOrDefault(PaymentStatus.Partial),
            countsByStatus.GetValueOrDefault(PaymentStatus.Late)
        };
    }

    private async Task<PropertyOccupancyDto> GetPropertyOccupancyAsync(Guid? userId, bool isAdmin)
    {
        var propertyWhere = BuildPropertyVisibilityWhere(userId, isAdmin);

        var totalsSql = $@"
            SELECT COUNT(*) AS Total, SUM(CASE WHEN p.IsEnabled = 0 THEN 1 ELSE 0 END) AS Disabled
            FROM Properties p
            WHERE {propertyWhere}";

        var totals = await _context.Database
            .SqlQueryRaw<PropertyTotalRow>(totalsSql)
            .FirstOrDefaultAsync();

        var occupiedSql = $@"
            SELECT COUNT(DISTINCT l.PropertyId) AS Occupied
            FROM Leases l
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE l.Status = 'Active'
                AND {propertyWhere}";

        var occupied = await _context.Database
            .SqlQueryRaw<PropertyOccupiedRow>(occupiedSql)
            .FirstOrDefaultAsync();

        int total = totals?.Total ?? 0;
        int disabled = totals?.Disabled ?? 0;
        int occupiedCount = occupied?.Occupied ?? 0;

        return new PropertyOccupancyDto
        {
            TotalProperties = total,
            OccupiedProperties = occupiedCount,
            AvailableProperties = total - occupiedCount - disabled,
            DisabledProperties = disabled
        };
    }

    private async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int year, Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere(userId, isAdmin, isTenant);

        var sql = $@"
            SELECT CAST(strftime('%m', py.ForPeriod) AS INTEGER) AS Month, SUM(py.Amount) AS TotalRevenue
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE py.Status = 'Received'
                AND strftime('%Y', py.ForPeriod) = '{{0}}'
                AND {visibilityWhere}
            GROUP BY strftime('%m', py.ForPeriod)
            ORDER BY Month";

        return await _context.Database
            .SqlQueryRaw<MonthlyRevenueDto>(sql, year.ToString())
            .ToListAsync();
    }

    private async Task<List<PaymentStatusCountDto>> GetPaymentStatusCountsAsync(Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere(userId, isAdmin, isTenant);

        var sql = $@"
            SELECT py.Status, COUNT(*) AS Count
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE {visibilityWhere}
            GROUP BY py.Status";

        return await _context.Database
            .SqlQueryRaw<PaymentStatusCountDto>(sql)
            .ToListAsync();
    }

    private static string BuildVisibilityWhere(Guid? userId, bool isAdmin, bool isTenant)
    {
        if (isAdmin || !userId.HasValue)
        {
            return "1 = 1";
        }

        var userIdStr = userId.Value.ToString();

        if (isTenant)
        {
            return $"l.TenantId = '{userIdStr}'";
        }

        return $"(p.IsPrivate = 0 OR p.LastEditedById = '{userIdStr}')";
    }

    private static string BuildPropertyVisibilityWhere(Guid? userId, bool isAdmin)
    {
        if (isAdmin || !userId.HasValue)
        {
            return "1 = 1";
        }

        return $"(p.IsPrivate = 0 OR p.LastEditedById = '{userId.Value}')";
    }

    private sealed class PropertyTotalRow
    {
        public int Total { get; set; }
        public int Disabled { get; set; }
    }

    private sealed class PropertyOccupiedRow
    {
        public int Occupied { get; set; }
    }
}
