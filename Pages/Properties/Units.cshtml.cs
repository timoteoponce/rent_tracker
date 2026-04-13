using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class UnitsModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public UnitsModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Property Property { get; set; } = null!;
    public List<PropertyUnit> Units { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties.FindAsync(id);

        if (Property == null)
        {
            return NotFound();
        }

        Units = await _context.PropertyUnits
            .Where(u => u.PropertyId == id)
            .OrderBy(u => u.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAddUnitAsync(Guid propertyId, string unitName, string? unitDescription, decimal unitPrice, decimal unitWarranty)
    {
        if (string.IsNullOrWhiteSpace(unitName) || unitPrice <= 0)
        {
            return RedirectToPage(new { id = propertyId });
        }

        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null || !property.CanBeLeasedByUnits)
        {
            return NotFound();
        }

        // Check if unit name already exists for this property
        var existingUnit = await _context.PropertyUnits
            .FirstOrDefaultAsync(u => u.PropertyId == propertyId && u.Name == unitName);

        if (existingUnit != null)
        {
            // Unit with this name already exists
            return RedirectToPage(new { id = propertyId });
        }

        var unit = new PropertyUnit
        {
            PropertyId = propertyId,
            Name = unitName,
            Description = unitDescription,
            Price = unitPrice,
            Warranty = unitWarranty,
            IsAvailable = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.PropertyUnits.Add(unit);
        await _context.SaveChangesAsync();

        return RedirectToPage(new { id = propertyId });
    }

    public async Task<IActionResult> OnPostDeleteUnitAsync(Guid propertyId, Guid unitId)
    {
        var unit = await _context.PropertyUnits.FindAsync(unitId);
        if (unit == null || unit.PropertyId != propertyId)
        {
            return NotFound();
        }

        // Only delete if unit is available (not occupied)
        if (!unit.IsAvailable)
        {
            return RedirectToPage(new { id = propertyId });
        }

        // Check if unit has any lease history
        var hasLeases = await _context.Leases.AnyAsync(l => l.PropertyUnitId == unitId);
        if (hasLeases)
        {
            // Don't delete if there's lease history
            return RedirectToPage(new { id = propertyId });
        }

        _context.PropertyUnits.Remove(unit);
        await _context.SaveChangesAsync();

        return RedirectToPage(new { id = propertyId });
    }
}
