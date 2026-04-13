using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

/// <summary>
/// User entity - simple user management without ASP.NET Identity complexity.
/// Roles: Administrator, Owner, Tenant
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Property> OwnedProperties { get; set; } = new List<Property>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
}

public static class UserRoles
{
    public const string Administrator = "Administrator";
    public const string Owner = "Owner";
    public const string Tenant = "Tenant";
}
