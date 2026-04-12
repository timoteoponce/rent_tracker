namespace RentTracker.Models;

public class PropertyPriceHistory
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public decimal Price { get; set; }
    public decimal Warranty { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public required string ChangedByUserId { get; set; }
    
    // Navigation properties
    public virtual Property? Property { get; set; }
    public virtual ApplicationUser? ChangedByUser { get; set; }
}
