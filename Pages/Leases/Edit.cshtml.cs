using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Leases;

[Authorize(Roles = "Administrator,Owner")]
public class EditModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public EditModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Lease Lease { get; set; } = null!;

    public SelectList AvailableUnits { get; set; } = new(Enumerable.Empty<object>());

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

        if (Lease.Status != LeaseStatus.Active)
        {
            return RedirectToPage("./Details", new { id });
        }

        await LoadUnitListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        ModelState.Remove("Lease.Property");
        ModelState.Remove("Lease.Tenant");
        ModelState.Remove("Lease.PropertyUnit");
        ModelState.Remove("Lease.Payments");
        ModelState.Remove("Lease.Status");

        if (!ModelState.IsValid)
        {
            await LoadUnitListAsync();
            return Page();
        }

        var existingLease = await _context.Leases
            .Include(l => l.Property)
            .ThenInclude(p => p.Units)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (existingLease == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        var isTenant = User.IsInRole(UserRoles.Tenant);

        if (!AuthorizationHelper.CanViewLease(existingLease, userId, isAdmin, isTenant))
        {
            return Forbid();
        }

        if (existingLease.Status != LeaseStatus.Active)
        {
            return RedirectToPage("./Details", new { id });
        }

        // If unit assignment changed, handle availability
        if (existingLease.PropertyUnitId != Lease.PropertyUnitId)
        {
            // Mark old unit as available
            if (existingLease.PropertyUnitId.HasValue)
            {
                var oldUnit = await _context.PropertyUnits.FindAsync(existingLease.PropertyUnitId.Value);
                if (oldUnit != null)
                {
                    oldUnit.IsAvailable = true;
                }
            }

            // Mark new unit as unavailable (if leasing a specific unit)
            if (Lease.PropertyUnitId.HasValue)
            {
                var newUnit = await _context.PropertyUnits.FindAsync(Lease.PropertyUnitId.Value);
                if (newUnit == null)
                {
                    ModelState.AddModelError("", "Selected unit not found.");
                    await LoadUnitListAsync();
                    return Page();
                }

                if (!newUnit.IsAvailable)
                {
                    ModelState.AddModelError("", "Selected unit is not available.");
                    await LoadUnitListAsync();
                    return Page();
                }

                // Check if unit already has another active lease
                var unitHasActiveLease = await _context.Leases
                    .AnyAsync(l => l.PropertyUnitId == newUnit.Id && l.Id != id && l.Status == LeaseStatus.Active);

                if (unitHasActiveLease)
                {
                    ModelState.AddModelError("", "Selected unit already has an active lease.");
                    await LoadUnitListAsync();
                    return Page();
                }

                newUnit.IsAvailable = false;
            }
        }

        // Update lease fields
        existingLease.AgreedPrice = Lease.AgreedPrice;
        existingLease.AgreedWarranty = Lease.AgreedWarranty;
        existingLease.StartDate = Lease.StartDate;
        existingLease.EndDate = Lease.EndDate;
        existingLease.PropertyUnitId = Lease.PropertyUnitId;
        existingLease.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        return RedirectToPage("./Details", new { id });
    }

    private async Task LoadUnitListAsync()
    {
        var units = await _context.PropertyUnits
            .Where(u => u.PropertyId == Lease.PropertyId && u.IsAvailable)
            .ToListAsync();

        // If the lease currently has a unit that's now occupied, still include it
        if (Lease.PropertyUnitId.HasValue)
        {
            var currentUnit = await _context.PropertyUnits.FindAsync(Lease.PropertyUnitId.Value);
            if (currentUnit != null && !currentUnit.IsAvailable)
            {
                units.Add(currentUnit);
            }
        }

        AvailableUnits = new SelectList(units.OrderBy(u => u.Name), "Id", "Name", Lease.PropertyUnitId);
    }
}
