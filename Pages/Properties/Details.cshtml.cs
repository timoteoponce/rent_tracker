using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public DetailsModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Property Property { get; set; } = null!;
    public List<Lease> LeaseHistory { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Property = await _context.Properties
            .Include(p => p.Units)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Property == null)
        {
            return NotFound();
        }

        // Get lease history - client side sort for DateTimeOffset
        var leasesList = await _context.Leases
            .Include(l => l.Tenant)
            .Where(l => l.PropertyId == id)
            .ToListAsync();

        LeaseHistory = leasesList
            .OrderByDescending(l => l.StartDate)
            .ToList();

        return Page();
    }
}
