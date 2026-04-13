using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Payments;

[Authorize(Roles = "Administrator,Owner")]
public class EditModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public EditModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Payment Payment { get; set; } = new();

    [BindProperty]
    public Guid OriginalPaymentId { get; set; }

    public Payment OriginalPayment { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        OriginalPayment = await _context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (OriginalPayment == null)
        {
            return NotFound();
        }

        OriginalPaymentId = OriginalPayment.Id;

        // Pre-fill new payment with original values
        Payment = new Payment
        {
            LeaseId = OriginalPayment.LeaseId,
            Amount = OriginalPayment.Amount,
            Currency = OriginalPayment.Currency,
            ForPeriod = OriginalPayment.ForPeriod,
            Status = OriginalPayment.Status,
            PaymentDate = DateTimeOffset.UtcNow, // Default to today for updates
            Notes = ""
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            // Reload original payment for display
            OriginalPayment = await _context.Payments
                .Include(p => p.Lease)
                .ThenInclude(l => l.Property)
                .Include(p => p.Lease)
                .ThenInclude(l => l.Tenant)
                .FirstOrDefaultAsync(p => p.Id == OriginalPaymentId);

            if (OriginalPayment == null)
            {
                return NotFound();
            }

            return Page();
        }

        // Get original payment
        var originalPayment = await _context.Payments.FindAsync(OriginalPaymentId);
        if (originalPayment == null)
        {
            return NotFound();
        }

        // Create new payment record
        Payment.CreatedAt = DateTimeOffset.UtcNow;
        Payment.PreviousPaymentId = OriginalPaymentId;
        // ForPeriod stays the same as original
        Payment.ForPeriod = originalPayment.ForPeriod;
        Payment.LeaseId = originalPayment.LeaseId;

        _context.Payments.Add(Payment);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
