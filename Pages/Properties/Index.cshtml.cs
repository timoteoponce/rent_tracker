using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Properties;

[Authorize(Roles = "Administrator,Owner")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Property> Properties { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = AuthorizationHelper.GetCurrentUserId(User);
        var isAdmin = User.IsInRole(UserRoles.Administrator);

        var query = _context.Properties
            .VisibleToUser(userId, isAdmin)
            .AsQueryable();

        // Fetch first, then sort in memory (SQLite doesn't support some orderings)
        var propertiesList = await query.ToListAsync();
        Properties = propertiesList
            .OrderBy(p => p.Name)
            .ToList();
    }
}
