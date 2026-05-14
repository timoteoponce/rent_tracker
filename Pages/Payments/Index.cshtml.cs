using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Payments;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Payment> Payments { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [TempData]
    public string? DeleteErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.PropertyUnit)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant)
            .AsQueryable();

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            query = query.Where(p => p.Status == StatusFilter);
        }

        // Apply visibility filtering
        if (isTenant && userId.HasValue)
        {
            // Tenants: only payments on their own leases
            // Use explicit Guid (not Guid?) for reliable EF Core translation
            query = query.Where(p => p.Lease.TenantId == userId.Value);
        }
        else if (!isAdmin)
        {
            // Owners: all payments on public properties + their own private properties
            query = query.Where(p => !p.Lease.Property.IsPrivate || p.Lease.Property.LastEditedById == userId);
        }

        // Fetch data first, then sort in memory (SQLite DateTimeOffset workaround)
        var paymentsList = await query.ToListAsync();
        Payments = paymentsList
            .OrderByDescending(p => p.ForPeriod)
            .ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        if (!AuthorizationHelper.CanViewPayment(payment, userId, isAdmin, isTenant))
        {
            return Forbid();
        }

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}
