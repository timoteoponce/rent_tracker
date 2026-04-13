using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RentTracker.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public LoginModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool MustChangePassword { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? mustChangePassword = null)
    {
        MustChangePassword = mustChangePassword == "true";
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Find user by full name
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.FullName == Input.FullName && u.IsActive);

        if (user == null)
        {
            ErrorMessage = "Invalid login attempt.";
            return Page();
        }

        // Verify password
        if (!Program.VerifyPassword(Input.Password, user.PasswordHash))
        {
            ErrorMessage = "Invalid login attempt.";
            return Page();
        }

        // Check if user must change password
        if (user.MustChangePassword)
        {
            // Store user ID in temp data and redirect to change password page
            TempData["UserId"] = user.Id.ToString();
            return RedirectToPage("./ChangePassword");
        }

        // Create authentication claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            ExpiresUtc = Input.RememberMe 
                ? DateTimeOffset.UtcNow.AddDays(30) 
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Update last login time
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        return LocalRedirect(returnUrl);
    }
}
