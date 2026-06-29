namespace RentTracker.Web.Models;

public class PaymentReminder
{
    public string Type { get; set; } = string.Empty;
    public Guid LeaseId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset ForPeriod { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public int Days { get; set; }
}
