namespace RentTracker.Models;

public class Property
{
    public int Id { get; set; }
    
    // Location
    public decimal LocationLatitude { get; set; }
    public decimal LocationLongitude { get; set; }
    
    // Physical characteristics
    public int SurfaceSquareMeters { get; set; }
    public int NumberOfRooms { get; set; }
    
    // Facilities
    public bool HasBathroom { get; set; }
    public bool HasKitchen { get; set; }
    public bool HasGarage { get; set; }
    public bool HasHotWater { get; set; }
    public bool HasAC { get; set; }
    public bool HasBackyard { get; set; }
    public bool HasSecurity { get; set; }
    public bool HasDoorbell { get; set; }
    
    // Financial
    public decimal CurrentPrice { get; set; }
    public decimal CurrentWarranty { get; set; }
    
    // Soft delete
    public bool IsEnabled { get; set; } = true;
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<PropertyOwner> Owners { get; set; } = [];
    public virtual ICollection<PropertyPriceHistory> PriceHistory { get; set; } = [];
    public virtual ICollection<Rental> Rentals { get; set; } = [];
    
    // Computed property for current rental
    public Rental? CurrentRental => Rentals.FirstOrDefault(r => r.Status == RentalStatus.Active);
    
    // Helper for Google Maps link
    public string GoogleMapsLink => $"https://www.google.com/maps?q={LocationLatitude},{LocationLongitude}";
}
