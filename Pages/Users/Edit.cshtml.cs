using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class EditModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public EditModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public User User { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
            return NotFound();
        }

        User = user;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userToUpdate = await _context.Users.FindAsync(id);
        
        if (userToUpdate == null)
        {
            return NotFound();
        }

        // Check for duplicate full name (excluding current user)
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.FullName == User.FullName && u.Id != id);

        if (existingUser != null)
        {
            ModelState.AddModelError("User.FullName", "A user with this name already exists.");
            return Page();
        }

        // Update user properties
        userToUpdate.FullName = User.FullName;
        userToUpdate.Role = User.Role;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return RedirectToPage("./Index");
    }

    private async Task<bool> UserExists(Guid id)
    {
        return await _context.Users.AnyAsync(e => e.Id == id);
    }
}
