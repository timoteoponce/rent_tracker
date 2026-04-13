using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

/// <summary>
/// PropertyUnit entity - represents a unit within a property (e.g., front unit, back unit)
/// Only used when Property.CanBeLeasedByUnits is true.
/// </summary>
public class PropertyUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty; // e.g., "Front Unit", "Back Unit"

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Warranty { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Foreign keys
    public Guid PropertyId { get; set; }

    // Navigation properties
    public Property Property { get; set; } = null!;
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
}
