using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RentTracker.Web.Pages.Settings;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
