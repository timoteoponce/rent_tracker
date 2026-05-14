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
        // Remove PasswordHash validation error - it is not submitted from the form
        ModelState.Remove("User.PasswordHash");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userToUpdate = await _context.Users.FindAsync(id);
        
        if (userToUpdate == null)
        {
            return NotFound();
        }

        // Prevent changing 'admin' username
        if (userToUpdate.Username == "admin" && User.Username != "admin")
        {
            ModelState.AddModelError("User.Username", "The username 'admin' cannot be changed.");
            return Page();
        }

        // Prevent assigning 'admin' username to another user
        if (User.Username == "admin" && userToUpdate.Username != "admin")
        {
            ModelState.AddModelError("User.Username", "The username 'admin' is reserved.");
            return Page();
        }

        // Check for duplicate username and email (database-side for performance)
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == User.Username.ToLower() && u.Id != id))
        {
            ModelState.AddModelError("User.Username", "This username is already taken.");
            return Page();
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == User.Email.ToLower() && u.Id != id))
        {
            ModelState.AddModelError("User.Email", "This email is already in use.");
            return Page();
        }

        // Bind form values directly to the tracked entity (prevents silent data loss when new properties are added)
        if (!await TryUpdateModelAsync(
            userToUpdate,
            "User",
            u => u.Username,
            u => u.Email,
            u => u.FullName,
            u => u.Role))
        {
            User = userToUpdate;
            return Page();
        }

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
