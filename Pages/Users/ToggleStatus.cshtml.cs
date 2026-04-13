using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class ToggleStatusModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public ToggleStatusModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent deactivating yourself
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (user.Id.ToString() == currentUserId)
        {
            return RedirectToPage("./Index");
        }

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
