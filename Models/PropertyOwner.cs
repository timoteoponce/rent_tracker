namespace RentTracker.Models;

public class PropertyOwner
{
    public int PropertyId { get; set; }
    public required string OwnerId { get; set; }
    public DateTime Since { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Property? Property { get; set; }
    public virtual ApplicationUser? Owner { get; set; }
}
