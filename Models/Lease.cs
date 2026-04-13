using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

/// <summary>
/// Lease entity - represents a rental agreement between an owner and a tenant.
/// </summary>
public class Lease
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Monthly rent amount agreed upon at lease start
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal AgreedPrice { get; set; }

    /// <summary>
    /// Security deposit agreed upon at lease start
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal AgreedWarranty { get; set; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset? EndDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = LeaseStatus.Active;

    /// <summary>
    /// Reason for termination if forcibly terminated
    /// </summary>
    [StringLength(500)]
    public string? TerminationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // Foreign keys
    public Guid PropertyId { get; set; }
    public Guid? PropertyUnitId { get; set; }
    public Guid TenantId { get; set; }

    // Navigation properties
    public Property Property { get; set; } = null!;
    public PropertyUnit? PropertyUnit { get; set; }
    public User Tenant { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public static class LeaseStatus
{
    public const string Active = "Active";
    public const string Closed = "Closed";      // Normal ending
    public const string Terminated = "Terminated";  // Forced ending
}
