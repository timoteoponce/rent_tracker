using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Data;
using RentTracker.Models;
using System.ComponentModel.DataAnnotations;

namespace RentTracker.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }
    public bool ShowDefaultCredentials { get; set; } = true;

    public IActionResult OnGet()
    {
        // Check if any users exist
        var hasUsers = _context.Users.Any();
        ShowDefaultCredentials = !hasUsers || _context.Users.Count() == 1;
        
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(Username, Password, RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByNameAsync(Username);
            
            // Check if password change is required
            if (user?.RequiresPasswordChange ?? false)
            {
                return RedirectToPage("/ChangePassword", new { returnUrl });
            }

            return Redirect(returnUrl);
        }

        ErrorMessage = "Invalid username or password.";
        return Page();
    }
}
