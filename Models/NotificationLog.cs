using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RentTracker.Web.Models;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    public Guid? LeaseId { get; set; }

    public DateTimeOffset ForPeriod { get; set; }

    [StringLength(50)]
    public string RecipientRole { get; set; } = string.Empty;

    public Guid? RecipientUserId { get; set; }

    [StringLength(20)]
    public string? RecipientPhoneNumber { get; set; }

    [StringLength(1000)]
    public string MessageContent { get; set; } = string.Empty;

    [StringLength(20)]
    public string Status { get; set; } = NotificationLogStatus.Sent;

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    public Lease? Lease { get; set; }

    public User? RecipientUser { get; set; }
}

public static class NotificationLogStatus
{
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

public static class NotificationType
{
    public const string PaymentDueSoon = "PaymentDueSoon";
    public const string PaymentToday = "PaymentToday";
    public const string PaymentOverdue = "PaymentOverdue";
    public const string OverdueSummary = "OverdueSummary";
    public const string TestMessage = "TestMessage";
    public const string DryRun = "DryRun";
}
