using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Pages.Leases;

[Authorize(Roles = "Administrator,Owner")]
public class TerminateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public TerminateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Lease Lease { get; set; } = null!;

    [BindProperty]
    [Required(ErrorMessage = "Termination reason is required")]
    [StringLength(500)]
    public string TerminationReason { get; set; } = string.Empty;

    [BindProperty]
    public DateTimeOffset? EndDate { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Lease = await _context.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (Lease == null)
        {
            return NotFound();
        }

        if (Lease.Status != LeaseStatus.Active)
        {
            return RedirectToPage("./Details", new { id });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            Lease = await _context.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenant)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (Lease == null)
            {
                return NotFound();
            }

            return Page();
        }

        Lease = await _context.Leases
            .Include(l => l.Property)
            .Include(l => l.PropertyUnit)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (Lease == null)
        {
            return NotFound();
        }

        if (Lease.Status != LeaseStatus.Active)
        {
            return RedirectToPage("./Details", new { id });
        }

        // Mark lease as terminated
        Lease.Status = LeaseStatus.Terminated;
        Lease.EndDate = EndDate ?? DateTimeOffset.UtcNow;
        Lease.TerminationReason = TerminationReason;
        Lease.UpdatedAt = DateTimeOffset.UtcNow;

        // Mark property/unit as available
        if (Lease.PropertyUnitId.HasValue)
        {
            var unit = await _context.PropertyUnits.FindAsync(Lease.PropertyUnitId.Value);
            if (unit != null)
            {
                unit.IsAvailable = true;
            }
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
