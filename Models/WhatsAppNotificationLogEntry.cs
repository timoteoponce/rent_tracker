namespace RentTracker.Web.Models;

public class WhatsAppNotificationLogEntry
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeDisplayName { get; set; } = string.Empty;
    public string RecipientRole { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? RecipientPhoneNumber { get; set; }
    public string? PropertyName { get; set; }
    public string? TenantName { get; set; }
    public DateTimeOffset ForPeriod { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset SentAtLocal { get; set; }
    public Guid? LeaseId { get; set; }
}