using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Property> Properties { get; set; } = new();

    [TempData]
    public string? DeleteErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        Properties = await _context.Properties
            .AsNoTracking()
            .VisibleToUser(userId, isAdmin)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property == null)
        {
            return NotFound();
        }

        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        if (!AuthorizationHelper.CanEditProperty(property, userId, isAdmin))
        {
            return Forbid();
        }

        // Prevent deleting properties linked to leases
        var hasLeases = await _context.Leases.AnyAsync(l => l.PropertyId == id);
        if (hasLeases)
        {
            DeleteErrorMessage = "Cannot delete a property that has leases. Close or terminate all leases first.";
            return RedirectToPage();
        }

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}
