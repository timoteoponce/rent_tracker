using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public DetailsModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Property Property { get; set; } = null!;
    public List<Lease> LeaseHistory { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties
            .AsNoTracking()
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Property == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        if (!AuthorizationHelper.CanViewProperty(Property, userId, isAdmin))
        {
            return Forbid();
        }

        // Get lease history - client side sort for DateTimeOffset
        var leasesList = await _context.Leases
            .AsNoTracking()
            .Include(l => l.Tenant)
            .Include(l => l.PropertyUnit)
            .Where(l => l.PropertyId == id)
            .ToListAsync();

        // Filter leases by visibility rules
        var isTenant = User.IsInRole(UserRoles.Tenant);
        LeaseHistory = leasesList
            .Where(l => AuthorizationHelper.CanViewLease(l, userId, isAdmin, isTenant))
            .OrderByDescending(l => l.StartDate)
            .ToList();

        return Page();
    }
}
