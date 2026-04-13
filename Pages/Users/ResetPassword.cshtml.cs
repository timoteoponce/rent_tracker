using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class ResetPasswordModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public ResetPasswordModel(RentTrackerDbContext context)
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

        // Reset password to default
        user.PasswordHash = Program.HashPassword("password123");
        user.MustChangePassword = true;
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
