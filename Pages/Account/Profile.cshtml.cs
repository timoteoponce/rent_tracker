using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RentTracker.Web.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public ProfileModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public ProfileInputModel Input { get; set; } = new();

    public User CurrentUser { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public class ProfileInputModel
    {
        [Required]
        [StringLength(50)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("./Login");
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
        {
            return NotFound();
        }

        CurrentUser = user;
        Input = new ProfileInputModel
        {
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("./Login");
        }

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
        {
            return NotFound();
        }

        CurrentUser = user;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Prevent changing 'admin' username
        if (user.Username == "admin" && Input.Username != "admin")
        {
            ModelState.AddModelError("Input.Username", "The username 'admin' cannot be changed.");
            return Page();
        }

        // Prevent non-admin from using 'admin' username
        if (Input.Username == "admin" && user.Username != "admin")
        {
            ModelState.AddModelError("Input.Username", "The username 'admin' is reserved.");
            return Page();
        }

        // Check for duplicate username (excluding current user) - client-side check due to SQLite limitations
        var allUsers = await _context.Users.ToListAsync();
        
        if (allUsers.Any(u => u.Username.Equals(Input.Username, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
        {
            ModelState.AddModelError("Input.Username", "This username is already taken.");
            return Page();
        }

        if (allUsers.Any(u => u.Email.Equals(Input.Email, StringComparison.OrdinalIgnoreCase) && u.Id != user.Id))
        {
            ModelState.AddModelError("Input.Email", "This email is already in use.");
            return Page();
        }

        // Update user
        user.Username = Input.Username;
        user.Email = Input.Email;
        user.FullName = Input.FullName;

        await _context.SaveChangesAsync();

        // Refresh the authentication cookie to update the username claim
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        SuccessMessage = "Profile updated successfully.";
        return Page();
    }
}
