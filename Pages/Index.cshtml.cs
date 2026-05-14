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
    
    public List<Payment> RecentPayments { get; set; } = new();
    public List<Lease> ActiveLeasesList { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        // Get counts - filtered by visibility
        var visibleProperties = await _context.Properties
            .VisibleToUser(userId, isAdmin)
            .Where(p => p.IsEnabled)
            .ToListAsync();
        TotalProperties = visibleProperties.Count;
        
        var visibleActiveLeases = await _context.Leases
            .Include(l => l.Property)
            .Where(l => l.Status == LeaseStatus.Active)
            .VisibleToUser(userId, isAdmin, isTenant)
            .ToListAsync();
        ActiveLeases = visibleActiveLeases.Count;
        
        TotalTenants = await _context.Users
            .CountAsync(u => u.Role == UserRoles.Tenant && u.IsActive);
        
        var visiblePendingPayments = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Where(p => p.Status == PaymentStatus.Pending)
            .VisibleToUser(userId, isAdmin, isTenant)
            .ToListAsync();
        PendingPayments = visiblePendingPayments.Count;

        // Get recent payments with related data
        // NOTE: Using client-side ordering because SQLite doesn't support DateTimeOffset in ORDER BY
        var recentPaymentsQuery = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant)
            .VisibleToUser(userId, isAdmin, isTenant)
            .Take(50)  // Take more than needed, then sort in memory
            .ToListAsync();
        
        RecentPayments = recentPaymentsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .ToList();

        // Get active leases with related data
        // NOTE: Using client-side ordering because SQLite doesn't support DateTimeOffset in ORDER BY
        var activeLeasesQuery = await _context.Leases
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
