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

        // Check for duplicate username (excluding current user) - client-side check due to SQLite DateTimeOffset limitations
        var allUsers = await _context.Users.ToListAsync();
        
        if (allUsers.Any(u => u.Username.Equals(User.Username, StringComparison.OrdinalIgnoreCase) && u.Id != id))
        {
            ModelState.AddModelError("User.Username", "This username is already taken.");
            return Page();
        }

        if (allUsers.Any(u => u.Email.Equals(User.Email, StringComparison.OrdinalIgnoreCase) && u.Id != id))
        {
            ModelState.AddModelError("User.Email", "This email is already in use.");
            return Page();
        }

        // Update user properties
        userToUpdate.Username = User.Username;
        userToUpdate.Email = User.Email;
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
