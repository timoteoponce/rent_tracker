using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Add price to history
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
}
