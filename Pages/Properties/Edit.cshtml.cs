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

    public decimal OriginalPrice { get; set; }
    public decimal OriginalWarranty { get; set; }
    public bool OriginalCanBeLeasedByUnits { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties.FindAsync(id);

        if (Property == null)
        {
            return NotFound();
        }

        OriginalPrice = Property.CurrentPrice;
        OriginalWarranty = Property.CurrentWarranty;
        OriginalCanBeLeasedByUnits = Property.CanBeLeasedByUnits;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var propertyToUpdate = await _context.Properties.FindAsync(id);

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

        await _context.SaveChangesAsync();

        return RedirectToPage("./Details", new { id = propertyToUpdate.Id });
    }
}
