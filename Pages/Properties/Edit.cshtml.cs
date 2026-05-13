using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class EditModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public EditModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Property Property { get; set; } = new();

    [BindProperty(Name = "UnitInputs")]
    public List<UnitInput> UnitInputs { get; set; } = new();

    public decimal OriginalPrice { get; set; }
    public decimal OriginalWarranty { get; set; }
    public bool OriginalCanBeLeasedByUnits { get; set; }
    public List<Guid> LockedUnitIds { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Property == null)
        {
            return NotFound();
        }

        OriginalPrice = Property.CurrentPrice;
        OriginalWarranty = Property.CurrentWarranty;
        OriginalCanBeLeasedByUnits = Property.CanBeLeasedByUnits;

        // Load existing units into the editable list
        foreach (var unit in Property.Units)
        {
            var hasLeases = await _context.Leases.AnyAsync(l => l.PropertyUnitId == unit.Id);
            if (hasLeases)
            {
                LockedUnitIds.Add(unit.Id);
            }

            UnitInputs.Add(new UnitInput
            {
                Id = unit.Id,
                Name = unit.Name,
                Description = unit.Description,
                Price = unit.Price,
                Warranty = unit.Warranty,
                IsLocked = hasLeases
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        ModelState.Remove("Property.Owner");
        ModelState.Remove("Property.Units");
        ModelState.Remove("Property.Leases");
        ModelState.Remove("Property.PriceHistory");

        if (!ModelState.IsValid)
        {
            await ReloadLockedUnitIds();
            return Page();
        }

        var propertyToUpdate = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (propertyToUpdate == null)
        {
            return NotFound();
        }

        // Track price/warranty changes for history
        var priceChanged = propertyToUpdate.CurrentPrice != Property.CurrentPrice;
        var warrantyChanged = propertyToUpdate.CurrentWarranty != Property.CurrentWarranty;

        if (priceChanged || warrantyChanged)
        {
            var priceHistory = new PropertyPriceHistory
            {
                PropertyId = propertyToUpdate.Id,
                Price = Property.CurrentPrice,
                Warranty = Property.CurrentWarranty,
                ChangedAt = DateTimeOffset.UtcNow,
                ChangeReason = $"Updated by {User.Identity?.Name}"
            };
            _context.PropertyPriceHistory.Add(priceHistory);
        }

        // Update all properties
        propertyToUpdate.Name = Property.Name;
        propertyToUpdate.Location = Property.Location;
        propertyToUpdate.SurfaceSquareMeters = Property.SurfaceSquareMeters;
        propertyToUpdate.NumberOfRooms = Property.NumberOfRooms;
        propertyToUpdate.CurrentPrice = Property.CurrentPrice;
        propertyToUpdate.CurrentWarranty = Property.CurrentWarranty;
        propertyToUpdate.HasBathroom = Property.HasBathroom;
        propertyToUpdate.HasKitchen = Property.HasKitchen;
        propertyToUpdate.HasGarage = Property.HasGarage;
        propertyToUpdate.HasHotWater = Property.HasHotWater;
        propertyToUpdate.HasAirConditioning = Property.HasAirConditioning;
        propertyToUpdate.HasBackyard = Property.HasBackyard;
        propertyToUpdate.HasSecurity = Property.HasSecurity;
        propertyToUpdate.HasDoorbell = Property.HasDoorbell;
        propertyToUpdate.CanBeLeasedByUnits = Property.CanBeLeasedByUnits;
        propertyToUpdate.UpdatedAt = DateTimeOffset.UtcNow;

        // Filter out null entries (can happen when unit rows are removed client-side,
        // creating non-sequential indices that the model binder fills with nulls)
        var validInputs = UnitInputs.Where(u => u != null).ToList();

        // Process units
        var submittedIds = validInputs.Where(u => u.Id != Guid.Empty).Select(u => u.Id).ToHashSet();

        // Delete units that were removed from the form (and have no lease history)
        var unitsToDelete = propertyToUpdate.Units
            .Where(u => !submittedIds.Contains(u.Id))
            .ToList();

        foreach (var unit in unitsToDelete)
        {
            var hasLeases = await _context.Leases.AnyAsync(l => l.PropertyUnitId == unit.Id);
            if (!hasLeases)
            {
                _context.PropertyUnits.Remove(unit);
            }
        }

        // Add new units and update existing ones
        foreach (var unitInput in validInputs)
        {
            if (string.IsNullOrWhiteSpace(unitInput.Name))
                continue;

            if (unitInput.Id == Guid.Empty)
            {
                // New unit
                var unit = new PropertyUnit
                {
                    PropertyId = propertyToUpdate.Id,
                    Name = unitInput.Name,
                    Description = unitInput.Description,
                    Price = unitInput.Price > 0 ? unitInput.Price.Value : propertyToUpdate.CurrentPrice,
                    Warranty = unitInput.Warranty > 0 ? unitInput.Warranty.Value : propertyToUpdate.CurrentWarranty,
                    IsAvailable = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _context.PropertyUnits.Add(unit);
            }
            else
            {
                // Update existing unit (only if not locked by lease)
                var existingUnit = propertyToUpdate.Units.FirstOrDefault(u => u.Id == unitInput.Id);
                if (existingUnit != null && !unitInput.IsLocked)
                {
                    existingUnit.Name = unitInput.Name;
                    existingUnit.Description = unitInput.Description;
                    existingUnit.Price = unitInput.Price ?? existingUnit.Price;
                    existingUnit.Warranty = unitInput.Warranty ?? existingUnit.Warranty;
                }
            }
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("./Details", new { id = propertyToUpdate.Id });
    }

    private async Task ReloadLockedUnitIds()
    {
        foreach (var unit in UnitInputs.Where(u => u != null))
        {
            if (unit!.Id != Guid.Empty)
            {
                var hasLeases = await _context.Leases.AnyAsync(l => l.PropertyUnitId == unit.Id);
                unit.IsLocked = hasLeases;
                if (hasLeases)
                {
                    LockedUnitIds.Add(unit.Id);
                }
            }
        }
    }

    public class UnitInput
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? Warranty { get; set; }
        public bool IsLocked { get; set; }
    }
}
