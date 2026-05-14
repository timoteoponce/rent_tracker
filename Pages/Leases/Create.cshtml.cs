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
public class CreateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public CreateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Lease Lease { get; set; } = new();

    public SelectList AvailableProperties { get; set; } = new(Enumerable.Empty<object>());
    public SelectList AvailableUnits { get; set; } = new(Enumerable.Empty<object>());
    public SelectList AvailableTenants { get; set; } = new(Enumerable.Empty<object>());
    public bool ShowUnitSection { get; set; }

    public async Task OnGetAsync(Guid? propertyId, Guid? unitId)
    {
        Lease.StartDate = DateTimeOffset.UtcNow;

        if (unitId.HasValue)
        {
            var unit = await _context.PropertyUnits
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.Id == unitId.Value);
            if (unit != null)
            {
                // Guard: verify user can view this property
                var userId = AuthorizationHelper.GetCurrentUserId(User);
                var isAdmin = User.IsInRole(UserRoles.Administrator);
                if (AuthorizationHelper.CanViewProperty(unit.Property, userId, isAdmin))
                {
                    Lease.PropertyId = unit.PropertyId;
                    Lease.PropertyUnitId = unitId.Value;
                    Lease.AgreedPrice = unit.Price;
                    Lease.AgreedWarranty = unit.Warranty;
                }
            }
        }
        else if (propertyId.HasValue)
        {
            var unitless = await _context.Properties.FindAsync(propertyId.Value);
            if (unitless != null)
            {
                var userId = AuthorizationHelper.GetCurrentUserId(User);
                var isAdmin = User.IsInRole(UserRoles.Administrator);
                if (AuthorizationHelper.CanViewProperty(unitless, userId, isAdmin))
                {
                    Lease.PropertyId = propertyId.Value;
                    Lease.AgreedPrice = unitless.CurrentPrice;
                    Lease.AgreedWarranty = unitless.CurrentWarranty;
                }
            }
        }

        await LoadSelectListsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Lease.Property");
        ModelState.Remove("Lease.Tenant");
        ModelState.Remove("Lease.PropertyUnit");

        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        var property = await _context.Properties
            .Include(p => p.Leases)
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == Lease.PropertyId);

        if (property == null)
        {
            ModelState.AddModelError("", "Property not found.");
            await LoadSelectListsAsync();
            return Page();
        }

        // Guard: verify user can view this property before creating a lease
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        if (!AuthorizationHelper.CanViewProperty(property, userId, isAdmin))
        {
            return Forbid();
        }

        // Validate based on unit vs whole property
        if (Lease.PropertyUnitId.HasValue)
        {
            var unit = property.Units.FirstOrDefault(u => u.Id == Lease.PropertyUnitId.Value);
            if (unit == null)
            {
                ModelState.AddModelError("", "Selected unit not found.");
                await LoadSelectListsAsync();
                return Page();
            }

            if (!unit.IsAvailable)
            {
                ModelState.AddModelError("", "Selected unit is not available.");
                await LoadSelectListsAsync();
                return Page();
            }

            var unitHasActiveLease = await _context.Leases
                .AnyAsync(l => l.PropertyUnitId == unit.Id && l.Status == LeaseStatus.Active);

            if (unitHasActiveLease)
            {
                ModelState.AddModelError("", "Selected unit already has an active lease.");
                await LoadSelectListsAsync();
                return Page();
            }

            unit.IsAvailable = false;
        }
        else
        {
            if (!property.CanBeLeasedByUnits)
            {
                var propertyHasActiveLease = property.Leases.Any(l => l.Status == LeaseStatus.Active);
                if (propertyHasActiveLease)
                {
                    ModelState.AddModelError("", "Property already has an active lease.");
                    await LoadSelectListsAsync();
                    return Page();
                }
            }
            else
            {
                var anyUnitLeased = await _context.Leases
                    .AnyAsync(l => l.PropertyId == property.Id && 
                                   l.Status == LeaseStatus.Active && 
                                   l.PropertyUnitId != null);

                if (anyUnitLeased)
                {
                    ModelState.AddModelError("", "Cannot lease whole property when units are already leased.");
                    await LoadSelectListsAsync();
                    return Page();
                }

                var wholePropertyLeased = await _context.Leases
                    .AnyAsync(l => l.PropertyId == property.Id && 
                                   l.Status == LeaseStatus.Active && 
                                   l.PropertyUnitId == null);

                if (wholePropertyLeased)
                {
                    ModelState.AddModelError("", "Property already has an active lease.");
                    await LoadSelectListsAsync();
                    return Page();
                }
            }
        }

        Lease.Status = LeaseStatus.Active;
        Lease.CreatedAt = DateTimeOffset.UtcNow;

        _context.Leases.Add(Lease);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnGetUnitsAsync(Guid propertyId)
    {
        var property = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == propertyId);

        if (property == null)
        {
            return new JsonResult(new { 
                canBeLeasedByUnits = false, 
                units = new List<object>(),
                defaultPrice = (decimal)0,
                defaultWarranty = (decimal)0
            });
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);
        if (!AuthorizationHelper.CanViewProperty(property, userId, isAdmin))
        {
            return Forbid();
        }

        var units = property.CanBeLeasedByUnits 
            ? property.Units.Select(u => new { 
                id = u.Id, 
                name = u.Name, 
                price = u.Price, 
                isAvailable = u.IsAvailable 
              }).Cast<object>().ToList()
            : new List<object>();

        return new JsonResult(new { 
            canBeLeasedByUnits = property.CanBeLeasedByUnits, 
            units,
            defaultPrice = property.CurrentPrice,
            defaultWarranty = property.CurrentWarranty
        });
    }

    private async Task LoadSelectListsAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        // Get visible properties for the dropdown
        var properties = await _context.Properties
            .VisibleToUser(userId, isAdmin)
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync();
        AvailableProperties = new SelectList(properties, "Id", "Name");

        // Get units for the selected property (if any)
        if (Lease.PropertyId != Guid.Empty)
        {
            var units = await _context.PropertyUnits
                .Where(u => u.PropertyId == Lease.PropertyId && u.IsAvailable)
                .OrderBy(u => u.Name)
                .ToListAsync();

            // If a unit is already selected, include it even if not available
            if (Lease.PropertyUnitId.HasValue)
            {
                var selectedUnit = await _context.PropertyUnits.FindAsync(Lease.PropertyUnitId.Value);
                if (selectedUnit != null && !units.Any(u => u.Id == selectedUnit.Id))
                {
                    units.Add(selectedUnit);
                }
            }

            AvailableUnits = new SelectList(units, "Id", "Name");
            ShowUnitSection = units.Any();
        }

        // Get active tenants
        var tenants = await _context.Users
            .Where(u => u.Role == UserRoles.Tenant && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
        AvailableTenants = new SelectList(tenants, "Id", "FullName");
    }
}
