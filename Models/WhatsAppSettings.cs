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

    [StringLength(20)]
    [Phone]
    public string? DryRunPhoneNumber { get; set; }

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

    [Range(-12, 14)]
    public int TimeZoneOffset { get; set; } = -4;

    [StringLength(100)]
    public string TestTemplateName { get; set; } = "renttracker_test";

    [StringLength(100)]
    public string PaymentDueSoonTemplateName { get; set; } = "renttracker_payment_due_soon";

    [StringLength(100)]
    public string PaymentTodayTemplateName { get; set; } = "renttracker_payment_today";

    [StringLength(100)]
    public string PaymentOverdueTemplateName { get; set; } = "renttracker_payment_overdue";

    [StringLength(100)]
    public string OverdueSummaryTemplateName { get; set; } = "renttracker_overdue_summary";

    [StringLength(10)]
    public string TemplateLanguage { get; set; } = "en";

    public bool EnableIncomingBot { get; set; } = false;

    public DateTimeOffset? LastNotificationRunDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}