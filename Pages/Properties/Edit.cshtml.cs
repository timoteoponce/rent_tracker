using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
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

    /// <summary>
    /// Whether the current user is allowed to see and toggle the IsPrivate checkbox.
    /// </summary>
    public bool CanTogglePrivacyFlag { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Property == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        if (!AuthorizationHelper.CanEditProperty(Property, userId, isAdmin))
        {
            return Forbid();
        }

        CanTogglePrivacyFlag = AuthorizationHelper.CanTogglePrivacy(Property, userId, isAdmin);

        OriginalPrice = Property.CurrentPrice;
        OriginalWarranty = Property.CurrentWarranty;
        OriginalCanBeLeasedByUnits = Property.CanBeLeasedByUnits;

        // Batch lease existence check to avoid N+1 queries
        var unitIds = Property.Units.Select(u => u.Id).ToList();
        var leasedUnitIds = await _context.Leases
            .Where(l => unitIds.Contains(l.PropertyUnitId.Value))
            .Select(l => l.PropertyUnitId.Value)
            .Distinct()
            .ToListAsync();

        // Load existing units into the editable list
        foreach (var unit in Property.Units)
        {
            var hasLeases = leasedUnitIds.Contains(unit.Id);
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
        // Remove validation for navigation properties - only FK IDs are submitted from form
        // Use prefix matching because complex navigation properties generate nested ModelState keys
        // Also remove UnitInputs because empty rows may be submitted, and code validates manually.
        var keysToRemove = ModelState.Keys
            .Where(k => k.StartsWith("Property.Owner") ||
                        k.StartsWith("Property.LastEditedBy") ||
                        k.StartsWith("Property.Units") ||
                        k.StartsWith("Property.Leases") ||
                        k.StartsWith("Property.PriceHistory") ||
                        k.StartsWith("UnitInputs"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ModelState.Remove(key);
        }

        // Load the tracked entity directly for model binding
        var propertyToUpdate = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (propertyToUpdate == null)
        {
            return NotFound();
        }

        // Bind form values directly to the tracked entity (prevents silent data loss when new properties are added)
        if (!await TryUpdateModelAsync(propertyToUpdate, "Property"))
        {
            Property = propertyToUpdate;
            await ReloadLockedUnitIds(propertyToUpdate.Units);
            return Page();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        if (!AuthorizationHelper.CanEditProperty(propertyToUpdate, userId, isAdmin))
        {
            return Forbid();
        }

        // If the IsPrivate flag changed, verify the user is allowed to toggle it
        if (!AuthorizationHelper.CanTogglePrivacy(propertyToUpdate, userId, isAdmin))
        {
            return Forbid();
        }

        // Track price/warranty changes for history
        var priceChanged = OriginalPrice != propertyToUpdate.CurrentPrice;
        var warrantyChanged = OriginalWarranty != propertyToUpdate.CurrentWarranty;

        // Update the last editor so they retain control over the privacy flag
        if (userId.HasValue)
        {
            propertyToUpdate.LastEditedById = userId.Value;
        }
        propertyToUpdate.UpdatedAt = DateTimeOffset.UtcNow;

        // Wrap multi-operation update in a transaction for consistency
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (priceChanged || warrantyChanged)
            {
                var priceHistory = new PropertyPriceHistory
                {
                    PropertyId = propertyToUpdate.Id,
                    Price = propertyToUpdate.CurrentPrice,
                    Warranty = propertyToUpdate.CurrentWarranty,
                    ChangedAt = DateTimeOffset.UtcNow,
                    ChangeReason = $"Updated by {User.Identity?.Name}"
                };
                _context.PropertyPriceHistory.Add(priceHistory);
            }

            // Filter out null entries (can happen when unit rows are removed client-side)
            var validInputs = UnitInputs.Where(u => u != null).ToList();

            // Process units
            var submittedIds = validInputs.Where(u => u.Id != Guid.Empty).Select(u => u.Id).ToHashSet();

            // Batch lease existence check to avoid N+1 queries
            var unitIds = propertyToUpdate.Units.Select(u => u.Id).ToList();
            var leasedUnitIds = await _context.Leases
                .Where(l => unitIds.Contains(l.PropertyUnitId.Value))
                .Select(l => l.PropertyUnitId.Value)
                .Distinct()
                .ToListAsync();

            // Delete units that were removed from the form (and have no lease history)
            var unitsToDelete = propertyToUpdate.Units
                .Where(u => !submittedIds.Contains(u.Id))
                .ToList();

            foreach (var unit in unitsToDelete)
            {
                if (!leasedUnitIds.Contains(unit.Id))
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
                    if (existingUnit != null && !leasedUnitIds.Contains(unitInput.Id))
                    {
                        existingUnit.Name = unitInput.Name;
                        existingUnit.Description = unitInput.Description;
                        existingUnit.Price = unitInput.Price ?? existingUnit.Price;
                        existingUnit.Warranty = unitInput.Warranty ?? existingUnit.Warranty;
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToPage("./Details", new { id = propertyToUpdate.Id });
    }

    private async Task ReloadLockedUnitIds(ICollection<PropertyUnit> units)
    {
        // Batch lease existence check to avoid N+1 queries
        var unitIds = units.Select(u => u.Id).ToList();
        var leasedUnitIds = await _context.Leases
            .Where(l => unitIds.Contains(l.PropertyUnitId.Value))
            .Select(l => l.PropertyUnitId.Value)
            .Distinct()
            .ToListAsync();

        foreach (var unit in units)
        {
            var isLocked = leasedUnitIds.Contains(unit.Id);
            var input = UnitInputs.FirstOrDefault(u => u?.Id == unit.Id);
            if (input != null)
            {
                input.IsLocked = isLocked;
            }
            if (isLocked)
            {
                LockedUnitIds.Add(unit.Id);
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
