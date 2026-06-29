using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RentTracker.Web.Models;

public class PushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [Required]
    [StringLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Auth { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedAt { get; set; }

    [ValidateNever]
    public User User { get; set; } = null!;
}

public class PushSubscriptionDto
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
