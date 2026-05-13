using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class CreateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public CreateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Property Property { get; set; } = new();

    [BindProperty(Name = "UnitInputs")]
    public List<UnitInput> UnitInputs { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove("Property.Owner");
        ModelState.Remove("Property.Units");
        ModelState.Remove("Property.Leases");
        ModelState.Remove("Property.PriceHistory");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var validInputs = UnitInputs.Where(u => u != null).ToList();

        if (Property.CanBeLeasedByUnits && validInputs.Any(u => !string.IsNullOrWhiteSpace(u.Name)))
        {
            foreach (var unitInput in validInputs.Where(u => !string.IsNullOrWhiteSpace(u.Name)))
            {
                var unit = new PropertyUnit
                {
                    PropertyId = Property.Id,
                    Name = unitInput.Name,
                    Description = unitInput.Description,
                    Price = unitInput.Price > 0 ? unitInput.Price.Value : Property.CurrentPrice,
                    Warranty = unitInput.Warranty > 0 ? unitInput.Warranty.Value : Property.CurrentWarranty,
                    IsAvailable = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _context.PropertyUnits.Add(unit);
            }
        }

        var priceHistory = new PropertyPriceHistory
        {
            PropertyId = Property.Id,
            Price = Property.CurrentPrice,
            Warranty = Property.CurrentWarranty,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = "Initial price"
        };

        _context.Properties.Add(Property);
        _context.PropertyPriceHistory.Add(priceHistory);

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    public class UnitInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? Warranty { get; set; }
    }
}
