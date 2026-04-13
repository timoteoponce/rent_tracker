namespace RentTracker.Web.Models;

/// <summary>
/// PropertyPriceHistory entity - tracks changes in price and warranty over time.
/// </summary>
public class PropertyPriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public decimal Price { get; set; }

    public decimal Warranty { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? ChangeReason { get; set; }

    // Foreign keys
    public Guid PropertyId { get; set; }

    // Navigation properties
    public Property Property { get; set; } = null!;
}
