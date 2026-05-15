using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
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

    public decimal TotalRevenue { get; set; }
    public int TotalPayments { get; set; }
    public decimal AveragePayment { get; set; }

    public List<PropertyBreakdownItem> PropertyBreakdown { get; set; } = new();
    public List<MonthlyBreakdownItem> MonthlyBreakdown { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (!StartDate.HasValue)
        {
            StartDate = DateTimeOffset.UtcNow.AddMonths(-12);
        }
        if (!EndDate.HasValue)
        {
            EndDate = DateTimeOffset.UtcNow;
        }

        var properties = await _context.Properties
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync();
        Properties = new SelectList(properties, "Id", "Name");

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        var visibilityWhere = BuildVisibilityWhere(userId, isAdmin, isTenant);

        var propertyFilter = PropertyId.HasValue
            ? "AND l.PropertyId = '{1}'"
            : "";

        var sql = $@"
            SELECT
                py.Id,
                py.Amount,
                py.Status,
                py.ForPeriod,
                py.CreatedAt,
                p.Id AS PropertyId,
                p.Name AS PropertyName,
                p.IsPrivate,
                p.LastEditedById,
                l.TenantId AS LeaseTenantId
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE py.ForPeriod >= {{0}} AND py.ForPeriod <= {{1}}
                {propertyFilter}
                AND {visibilityWhere}
            ORDER BY py.ForPeriod DESC";

        var parameters = new List<object>
        {
            StartDate.Value.ToString("O"),
            EndDate.Value.ToString("O")
        };

        if (PropertyId.HasValue)
        {
            parameters.Add(PropertyId.Value.ToString());
        }

        var payments = await _context.Database
            .SqlQueryRaw<PaymentDetailDto>(sql, parameters.ToArray())
            .ToListAsync();

        TotalPayments = payments.Count;
        TotalRevenue = payments.Sum(p => p.Amount);
        AveragePayment = TotalPayments > 0 ? TotalRevenue / TotalPayments : 0;

        PropertyBreakdown = payments
            .GroupBy(p => p.PropertyName)
            .Select(g => new PropertyBreakdownItem
            {
                PropertyName = g.Key,
                PaymentCount = g.Count(),
                TotalAmount = g.Sum(p => p.Amount),
                AverageAmount = g.Average(p => p.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        MonthlyBreakdown = payments
            .GroupBy(p => new { p.ForPeriod.Year, p.ForPeriod.Month })
            .Select(g => new MonthlyBreakdownItem
            {
                Month = new DateTimeOffset(new DateTime(g.Key.Year, g.Key.Month, 1)),
                PaymentCount = g.Count(),
                TotalRevenue = g.Sum(p => p.Amount),
                PendingCount = g.Count(p => p.Status == "Pending"),
                ReceivedCount = g.Count(p => p.Status == "Received"),
                LateCount = g.Count(p => p.Status == "Late")
            })
            .OrderBy(x => x.Month)
            .ToList();
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

    public class PropertyBreakdownItem
    {
        public string PropertyName { get; set; } = string.Empty;
        public int PaymentCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; } = 0;
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
