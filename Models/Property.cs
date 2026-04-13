using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

/// <summary>
/// Property entity - represents a house, land, room, or apartment.
/// Can be rented as a whole or in units.
/// </summary>
public class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Google Maps link or GPS coordinates
    /// </summary>
    [StringLength(500)]
    public string Location { get; set; } = string.Empty;

    public double? SurfaceSquareMeters { get; set; }

    public int? NumberOfRooms { get; set; }

    // Facilities flags
    public bool HasBathroom { get; set; }
    public bool HasKitchen { get; set; }
    public bool HasGarage { get; set; }
    public bool HasHotWater { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasBackyard { get; set; }
    public bool HasSecurity { get; set; }
    public bool HasDoorbell { get; set; }

    /// <summary>
    /// Monthly rent price in Bolivianos (BOB)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Security deposit/warranty amount in Bolivianos (BOB)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal CurrentWarranty { get; set; }

    /// <summary>
    /// Can be leased as a whole property or divided into units
    /// </summary>
    public bool CanBeLeasedByUnits { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    // Foreign keys
    public Guid? OwnerId { get; set; }

    // Navigation properties
    public User? Owner { get; set; }
    public ICollection<PropertyUnit> Units { get; set; } = new List<PropertyUnit>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<PropertyPriceHistory> PriceHistory { get; set; } = new List<PropertyPriceHistory>();
}
