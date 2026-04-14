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

        // Check for duplicate full name
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.FullName == User.FullName);

        if (existingUser != null)
        {
            ModelState.AddModelError("User.FullName", "A user with this name already exists.");
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
