using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Payments;

[Authorize]
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<Payment> Payments { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant)
            .AsQueryable();

        // For tenants, only show their own payments
        if (User.IsInRole(UserRoles.Tenant))
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userId, out var tenantId))
            {
                query = query.Where(p => p.Lease.TenantId == tenantId);
            }
        }

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            query = query.Where(p => p.Status == StatusFilter);
        }

        // Fetch data first, then sort in memory (SQLite DateTimeOffset workaround)
        var paymentsList = await query.ToListAsync();
        Payments = paymentsList
            .OrderByDescending(p => p.ForPeriod)
            .ToList();
    }
}
