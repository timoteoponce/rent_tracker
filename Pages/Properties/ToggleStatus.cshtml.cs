using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class ToggleStatusModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public ToggleStatusModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property == null)
        {
            return NotFound();
        }

        // Check if property has active leases before disabling
        if (property.IsEnabled)
        {
            var hasActiveLease = await _context.Leases
                .AnyAsync(l => l.PropertyId == id && l.Status == LeaseStatus.Active);

            if (hasActiveLease)
            {
                // Cannot disable property with active lease
                return RedirectToPage("./Details", new { id });
            }
        }

        property.IsEnabled = !property.IsEnabled;
        property.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
