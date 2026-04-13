using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Leases;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Lease> Leases { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.Leases
            .Include(l => l.Property)
            .Include(l => l.PropertyUnit)
            .Include(l => l.Tenant)
            .AsQueryable();

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            query = query.Where(l => l.Status == StatusFilter);
        }

        // Fetch data first, then sort in memory (SQLite DateTimeOffset workaround)
        var leasesList = await query.ToListAsync();
        Leases = leasesList
            .OrderByDescending(l => l.StartDate)
            .ToList();
    }
}
