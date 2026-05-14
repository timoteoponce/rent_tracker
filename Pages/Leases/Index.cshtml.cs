using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Leases;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Lease> Leases { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [TempData]
    public string? DeleteErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        var query = _context.Leases
            .AsNoTracking()
            .Include(l => l.Property)
            .Include(l => l.PropertyUnit)
            .Include(l => l.Tenant)
            .AsQueryable();

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            query = query.Where(l => l.Status == StatusFilter);
        }

        // Apply visibility filtering
        if (isTenant && userId.HasValue)
        {
            // Tenants: only their own leases
            query = query.Where(l => l.TenantId == userId.Value);
        }
        else if (!isAdmin)
        {
            // Owners: all leases on public properties + their own private properties
            query = query.Where(l => !l.Property.IsPrivate || l.Property.LastEditedById == userId);
        }

        // Fetch data first, then sort in memory (SQLite DateTimeOffset workaround)
        var leasesList = await query.ToListAsync();
        Leases = leasesList
            .OrderByDescending(l => l.StartDate)
            .ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var lease = await _context.Leases
            .Include(l => l.Property)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lease == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        if (!AuthorizationHelper.CanViewLease(lease, userId, isAdmin, isTenant))
        {
            return Forbid();
        }

        // Prevent deleting leases linked to payments
        var hasPayments = await _context.Payments.AnyAsync(p => p.LeaseId == id);
        if (hasPayments)
        {
            DeleteErrorMessage = "Cannot delete a lease that has payment records. Deleting it would cascade-delete all payments.";
            return RedirectToPage();
        }

        _context.Leases.Remove(lease);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}
