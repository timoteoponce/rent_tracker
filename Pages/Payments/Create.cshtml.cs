using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Payments;

[Authorize(Roles = "Administrator,Owner")]
public class CreateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public CreateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Payment Payment { get; set; } = new();

    public SelectList ActiveLeases { get; set; } = new(Enumerable.Empty<object>());
    public string? WarningMessage { get; set; }

    public async Task OnGetAsync(Guid? leaseId)
    {
        await LoadSelectListsAsync();

        if (leaseId.HasValue)
        {
            Payment.LeaseId = leaseId.Value;
        }

        // Set defaults
        Payment.PaymentDate = DateTimeOffset.UtcNow;
        Payment.Currency = "BOB";
        Payment.Status = PaymentStatus.Pending;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        // Check for existing payment for same period
        // NOTE: Fetch and filter client-side because SQLite doesn't support DateTimeOffset.Year/Month in LINQ
        var existingPaymentsForLease = await _context.Payments
            .Where(p => p.LeaseId == Payment.LeaseId)
            .ToListAsync();
        
        var existingPayment = existingPaymentsForLease
            .FirstOrDefault(p => p.ForPeriod.Year == Payment.ForPeriod.Year &&
                                p.ForPeriod.Month == Payment.ForPeriod.Month);

        if (existingPayment != null)
        {
            WarningMessage = $"Warning: A payment for {Payment.ForPeriod:MMM yyyy} already exists. You may want to update the existing payment instead.";
            await LoadSelectListsAsync();
            return Page();
        }

        Payment.CreatedAt = DateTimeOffset.UtcNow;
        Payment.PreviousPaymentId = null; // This is a new payment, not an update

        _context.Payments.Add(Payment);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private async Task LoadSelectListsAsync()
    {
        // Get active leases with property and tenant info
        var leases = await _context.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        var leaseList = leases.Select(l => new
        {
            Id = l.Id,
            DisplayText = $"{l.Property.Name} - {l.Tenant.FullName} (Bs. {l.AgreedPrice:N2})"
        }).ToList();

        ActiveLeases = new SelectList(leaseList, "Id", "DisplayText");
    }
}
