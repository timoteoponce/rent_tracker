using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Leases;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public DetailsModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Lease Lease { get; set; } = null!;
    public List<Payment> Payments { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Lease = await _context.Leases
            .Include(l => l.Property)
            .Include(l => l.PropertyUnit)
            .Include(l => l.Tenant)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (Lease == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        if (!AuthorizationHelper.CanViewLease(Lease, userId, isAdmin, isTenant))
        {
            return Forbid();
        }

        // Get payments for this lease (client-side ordering for DateTimeOffset)
        // Include Lease so CanViewPayment can check property visibility
        var paymentsQuery = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Where(p => p.LeaseId == id)
            .ToListAsync();

        Payments = paymentsQuery
            .Where(p => AuthorizationHelper.CanViewPayment(p, userId, isAdmin, isTenant))
            .OrderByDescending(p => p.ForPeriod)
            .ToList();

        return Page();
    }
}
