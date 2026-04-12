using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentTracker.Models;
using System.ComponentModel.DataAnnotations;

namespace RentTracker.Pages;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    [DataType(DataType.Password)]
    [Required]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    [DataType(DataType.Password)]
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    [DataType(DataType.Password)]
    [Required]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match.")]
    public string ConfirmPassword { get; set; } = "";

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ViewData["ReturnUrl"] = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var changePasswordResult = await _userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            ErrorMessage = string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
            return Page();
        }

        // Clear the requires password change flag
        user.RequiresPasswordChange = false;
        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);
        SuccessMessage = "Your password has been changed successfully.";
        
        // Redirect after showing success
        return Redirect(returnUrl);
    }
}
