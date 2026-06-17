using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

public class WhatsAppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool IsEnabled { get; set; } = false;

    [StringLength(50)]
    public string Provider { get; set; } = "MetaCloud";

    [StringLength(500)]
    public string? AccessToken { get; set; }

    [StringLength(100)]
    public string? PhoneNumberId { get; set; }

    [StringLength(100)]
    public string? BusinessAccountId { get; set; }

    [StringLength(200)]
    public string? VerifyToken { get; set; }

    public bool EnablePaymentDueSoon { get; set; } = true;

    public bool EnablePaymentToday { get; set; } = true;

    public bool EnablePaymentOverdue { get; set; } = true;

    public bool EnableOverdueToTenant { get; set; } = true;

    public bool EnableOverdueToLender { get; set; } = true;

    [Range(1, 30)]
    public int DueSoonDaysBefore { get; set; } = 3;

    public bool EnableIncomingBot { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}