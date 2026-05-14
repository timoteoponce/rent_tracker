using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Data.Queries;
using RentTracker.Web.Data.Queries.Dtos;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;
    private readonly ISqlQueryService _sqlQueries;

    public IndexModel(RentTrackerDbContext context, ISqlQueryService sqlQueries)
    {
        _context = context;
        _sqlQueries = sqlQueries;
    }

    public int TotalProperties { get; set; }
    public int ActiveLeases { get; set; }
    public int TotalTenants { get; set; }
    public int PendingPayments { get; set; }
    
    public List<RecentPaymentDto> RecentPayments { get; set; } = new();
    public List<Lease> ActiveLeasesList { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        // Get counts - filtered by visibility
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

        // Get recent payments via SQL (avoids client-side sorting and over-fetching)
        RecentPayments = await _sqlQueries.GetRecentPaymentsAsync(10, userId, isAdmin, isTenant);

        // Get active leases with related data
        // NOTE: Fetch then sort client-side (SQLite DateTimeOffset workaround)
        var activeLeasesQuery = await _context.Leases
            .AsNoTracking()
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .VisibleToUser(userId, isAdmin, isTenant)
            .ToListAsync();
        
        ActiveLeasesList = activeLeasesQuery
            .OrderBy(l => l.StartDate)
            .ToList();
    }
}
