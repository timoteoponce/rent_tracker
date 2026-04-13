using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Users;

[Authorize(Roles = "Administrator")]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<User> Users { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Fetch users and sort in memory (SQLite DateTimeOffset workaround)
        var usersList = await _context.Users.ToListAsync();
        Users = usersList
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToList();
    }
}
