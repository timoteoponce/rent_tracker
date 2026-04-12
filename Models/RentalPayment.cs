namespace RentTracker.Models;

public class RentalPayment
{
    public int Id { get; set; }
    public int RentalId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    
    // Versioning for audit trail
    public int Version { get; set; } = 1;
    public int? PreviousPaymentId { get; set; }
    
    // Navigation properties
    public virtual Rental? Rental { get; set; }
    public virtual RentalPayment? PreviousPayment { get; set; }
    public virtual ICollection<RentalPayment> NewerVersions { get; set; } = [];
}
