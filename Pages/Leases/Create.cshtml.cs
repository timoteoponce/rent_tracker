using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
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

    public async Task OnGetAsync()
    {
        await LoadSelectListsAsync();
        
        // Set default start date to today
        Lease.StartDate = DateTimeOffset.UtcNow;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Remove validation for navigation properties - only FK IDs are submitted from form
        ModelState.Remove("Lease.Property");
        ModelState.Remove("Lease.Tenant");
        
        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        // Check if property has active lease
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

        // Validate based on unit vs whole property
        if (Lease.PropertyUnitId.HasValue)
        {
            // Leasing a specific unit
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

            // Check if unit already has active lease
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
            // Leasing whole property
            if (!property.CanBeLeasedByUnits)
            {
                // Check if property has active lease
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
                // Property can be leased by units - check if any units are leased
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

                // Check if whole property is already leased
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

        var units = property.CanBeLeasedByUnits 
            ? property.Units.Where(u => u.IsAvailable).Select(u => new { id = u.Id, name = u.Name, price = u.Price }).Cast<object>().ToList()
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
        // Get properties that don't have active whole-property leases
        var availableProperties = await _context.Properties
            .Where(p => p.IsEnabled)
            .ToListAsync();

        // Filter out properties with active whole leases client-side
        var propertyList = new List<Property>();
        foreach (var prop in availableProperties)
        {
            if (prop.CanBeLeasedByUnits)
            {
                // Check if any units are available or if whole property can be leased
                var units = await _context.PropertyUnits
                    .Where(u => u.PropertyId == prop.Id && u.IsAvailable)
                    .ToListAsync();
                
                var wholePropertyLeased = await _context.Leases
                    .AnyAsync(l => l.PropertyId == prop.Id && 
                                   l.Status == LeaseStatus.Active && 
                                   l.PropertyUnitId == null);

                if (units.Any() || !wholePropertyLeased)
                {
                    propertyList.Add(prop);
                }
            }
            else
            {
                // Check if property has active lease
                var hasActiveLease = await _context.Leases
                    .AnyAsync(l => l.PropertyId == prop.Id && l.Status == LeaseStatus.Active);
                
                if (!hasActiveLease)
                {
                    propertyList.Add(prop);
                }
            }
        }

        AvailableProperties = new SelectList(propertyList, "Id", "Name");

        // Get tenants (users with Tenant role)
        var tenants = await _context.Users
            .Where(u => u.Role == UserRoles.Tenant && u.IsActive)
            .ToListAsync();
        AvailableTenants = new SelectList(tenants, "Id", "FullName");

        // Units will be loaded via JavaScript based on property selection
        AvailableUnits = new SelectList(Enumerable.Empty<PropertyUnit>(), "Id", "Name");
    }
}
