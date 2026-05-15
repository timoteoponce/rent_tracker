using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public int TotalProperties { get; set; }
    public int ActiveLeases { get; set; }
    public int TotalTenants { get; set; }
    public int PendingPayments { get; set; }

    public List<RecentPaymentDto> RecentPayments { get; set; } = new();
    public List<DashboardLeaseDto> ActiveLeasesList { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        TotalProperties = await _context.Properties
            .AsNoTracking()
            .VisibleToUser(userId, isAdmin)
            .Where(p => p.IsEnabled)
            .CountAsync();

        ActiveLeases = await _context.Leases
            .AsNoTracking()
            .Where(l => l.Status == LeaseStatus.Active)
            .VisibleToUser(userId, isAdmin, isTenant)
            .CountAsync();

        TotalTenants = await _context.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == UserRoles.Tenant && u.IsActive);

        PendingPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending)
            .VisibleToUser(userId, isAdmin, isTenant)
            .CountAsync();

        RecentPayments = await GetRecentPaymentsAsync(10, userId, isAdmin, isTenant);
        ActiveLeasesList = await GetActiveLeasesAsync(userId, isAdmin, isTenant);
    }

    private async Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(
        int count, Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhereClause(userId, isAdmin, isTenant);

        var sql = $@"
            SELECT
                py.Id,
                py.Amount,
                py.Status,
                py.ForPeriod,
                py.CreatedAt,
                p.Name AS PropertyName,
                u.Username AS TenantName
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            INNER JOIN Users u ON l.TenantId = u.Id
            WHERE {visibilityWhere}
            ORDER BY datetime(py.CreatedAt) DESC
            LIMIT {{0}}";

        return await _context.Database
            .SqlQueryRaw<RecentPaymentDto>(sql, count.ToString())
            .ToListAsync();
    }

    private async Task<List<DashboardLeaseDto>> GetActiveLeasesAsync(
        Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhereClause(userId, isAdmin, isTenant);

        var sql = $@"
            SELECT
                l.Id,
                l.StartDate,
                l.AgreedPrice,
                p.Name AS PropertyName,
                u.FullName AS TenantName
            FROM Leases l
            INNER JOIN Properties p ON l.PropertyId = p.Id
            INNER JOIN Users u ON l.TenantId = u.Id
            WHERE l.Status = 'Active'
                AND {visibilityWhere}
            ORDER BY l.StartDate ASC";

        return await _context.Database
            .SqlQueryRaw<DashboardLeaseDto>(sql)
            .ToListAsync();
    }

    private static string BuildVisibilityWhereClause(Guid? userId, bool isAdmin, bool isTenant)
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
}

public class DashboardLeaseDto
{
    public Guid Id { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public decimal AgreedPrice { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}
