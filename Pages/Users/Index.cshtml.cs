using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<User> Users { get; set; } = new();

    [TempData]
    public string? DeleteErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent deleting system users
        if (user.IsSystemUser || user.Username == "admin")
        {
            DeleteErrorMessage = "Cannot delete system users.";
            return RedirectToPage();
        }

        // Prevent deleting users linked to leases
        var hasLeases = await _context.Leases.AnyAsync(l => l.TenantId == id);
        if (hasLeases)
        {
            DeleteErrorMessage = "Cannot delete a tenant who is linked to one or more leases. Close or terminate all leases first.";
            return RedirectToPage();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}
