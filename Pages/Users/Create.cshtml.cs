using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class CreateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public CreateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public User User { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Remove PasswordHash validation error - we set it programmatically
        ModelState.Remove("User.PasswordHash");
        
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Generate random values if not provided
        if (string.IsNullOrWhiteSpace(User.Username))
        {
            var random = Guid.NewGuid().ToString("N")[..8];
            User.Username = $"user-{random}";
        }

        if (string.IsNullOrWhiteSpace(User.Email))
        {
            var random = Guid.NewGuid().ToString("N")[..8];
            User.Email = $"user-{random}@fakemail.ch";
        }

        // Check for duplicate username (client-side fetch first due to DateTimeOffset limitations in SQLite)
        var allUsers = await _context.Users.ToListAsync();
        
        if (allUsers.Any(u => u.Username.Equals(User.Username, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("User.Username", "This username is already taken.");
            return Page();
        }

        if (allUsers.Any(u => u.Email.Equals(User.Email, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("User.Email", "This email is already in use.");
            return Page();
        }

        // Prevent creating another user with username 'admin'
        if (User.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("User.Username", "The username 'admin' is reserved for the system administrator.");
            return Page();
        }

        // Set defaults
        User.PasswordHash = Program.HashPassword("password123");
        User.MustChangePassword = true;
        User.IsActive = true;
        User.CreatedAt = DateTimeOffset.UtcNow;
        User.LastLoginAt = null;

        _context.Users.Add(User);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
