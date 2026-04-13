using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RentTracker.Web.Pages.Account;

public class ChangePasswordModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public ChangePasswordModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool MustChangePassword { get; set; } = true;

    public class InputModel
    {
        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        // Ensure we have a user ID from temp data
        if (TempData["UserId"] == null)
        {
            return RedirectToPage("./Login");
        }

        // Keep the temp data for the post
        TempData.Keep("UserId");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Ensure we have a user ID
        var userIdString = TempData["UserId"]?.ToString();
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return RedirectToPage("./Login");
        }

        if (!ModelState.IsValid)
        {
            TempData.Keep("UserId");
            return Page();
        }

        // Find the user
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        // Update password
        user.PasswordHash = Program.HashPassword(Input.NewPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();

        // Sign in the user automatically
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        return RedirectToPage("/Index");
    }
}
