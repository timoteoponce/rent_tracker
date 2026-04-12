namespace RentTracker.Models;

public enum RentalStatus
{
    Active,
    Closed,
    Terminated
}

public class Rental
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public required string TenantId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal WarrantyAmount { get; set; }
    public RentalStatus Status { get; set; } = RentalStatus.Active;
    public string? TerminationReason { get; set; }
    
    // Navigation properties
    public virtual Property? Property { get; set; }
    public virtual ApplicationUser? Tenant { get; set; }
    public virtual ICollection<RentalPayment> Payments { get; set; } = [];
}
