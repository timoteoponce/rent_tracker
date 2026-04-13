using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
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
        // Get counts
        TotalProperties = await _context.Properties
            .CountAsync(p => p.IsEnabled);
        
        ActiveLeases = await _context.Leases
            .CountAsync(l => l.Status == LeaseStatus.Active);
        
        TotalTenants = await _context.Users
            .CountAsync(u => u.Role == UserRoles.Tenant && u.IsActive);
        
        PendingPayments = await _context.Payments
            .CountAsync(p => p.Status == PaymentStatus.Pending);

        // Get recent payments with related data
        // NOTE: Using client-side ordering because SQLite doesn't support DateTimeOffset in ORDER BY
        var recentPaymentsQuery = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant)
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
            .ToListAsync();
        
        ActiveLeasesList = activeLeasesQuery
            .OrderBy(l => l.StartDate)
            .ToList();
    }
}
