using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RentTracker.Web.Models;

/// <summary>
/// Immutable snapshot of a <see cref="Payment"/> as it was BEFORE an edit.
/// Payments are edited in place (one row per lease + period); every edit writes
/// one of these so the change history is preserved without duplicating Payment rows.
/// </summary>
public class PaymentAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The payment this snapshot belongs to.</summary>
    public Guid PaymentId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "BOB";

    [StringLength(50)]
    public string Status { get; set; } = PaymentStatus.Pending;

    /// <summary>The month/year the payment covered at the time of this snapshot.</summary>
    public DateTimeOffset ForPeriod { get; set; }

    public DateTimeOffset PaymentDate { get; set; }

    [StringLength(200)]
    public string? Notes { get; set; }

    /// <summary>When the edit that produced this snapshot happened.</summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>User who performed the edit, when known.</summary>
    public Guid? EditedByUserId { get; set; }

    [ValidateNever]
    public Payment Payment { get; set; } = null!;
}
