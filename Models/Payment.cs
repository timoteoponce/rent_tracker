using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

/// <summary>
/// Payment entity - represents a rental payment record.
/// Updates create new records, old records are kept for history.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Payment amount in Bolivianos (BOB)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code - BOB for now, extensible for USD in future
    /// </summary>
    [StringLength(3)]
    public string Currency { get; set; } = "BOB";

    /// <summary>
    /// The month/year this payment covers
    /// </summary>
    public DateTimeOffset ForPeriod { get; set; }

    /// <summary>
    /// When the payment was actually made
    /// </summary>
    public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(50)]
    public string Status { get; set; } = PaymentStatus.Pending;

    [StringLength(200)]
    public string? Notes { get; set; }

    /// <summary>
    /// If this payment was an update of a previous payment,
    /// this points to the original payment record.
    /// </summary>
    public Guid? PreviousPaymentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Foreign keys
    public Guid LeaseId { get; set; }

    // Navigation properties
    public Lease Lease { get; set; } = null!;
}

public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Received = "Received";
    public const string Partial = "Partial";
    public const string Late = "Late";
}
