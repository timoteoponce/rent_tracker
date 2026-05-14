using System.Security.Claims;
using RentTracker.Web.Models;

namespace RentTracker.Web.Helpers;

/// <summary>
/// Central authorization logic for property privacy.
/// All visibility checks for properties, leases, and payments go through here.
/// </summary>
public static class AuthorizationHelper
{
    /// <summary>
    /// Extracts the current user's ID from their claims.
    /// Returns null if parsing fails or user is not authenticated.
    /// </summary>
    public static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdString, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Checks if the user can see a property in the catalog/listing.
    /// Admin: all properties.
    /// Owner: public properties + their own private properties.
    /// Tenant: no catalog access (page-level restriction already handles this).
    /// </summary>
    public static bool CanViewProperty(Property property, Guid? userId, bool isAdmin)
    {
        if (isAdmin)
        {
            return true;
        }

        if (property == null)
        {
            return false;
        }

        // Public properties are visible to all authenticated users
        if (!property.IsPrivate)
        {
            return true;
        }

        // Private properties are only visible to the last editor
        return property.LastEditedById.HasValue && property.LastEditedById == userId;
    }

    /// <summary>
    /// Checks if the user can edit a property.
    /// Same rules as CanViewProperty for now: if you can see it, you can edit it.
    /// Admin: all properties.
    /// Owner: public properties + their own private properties.
    /// </summary>
    public static bool CanEditProperty(Property property, Guid? userId, bool isAdmin)
    {
        return CanViewProperty(property, userId, isAdmin);
    }

    /// <summary>
    /// Checks if the user can toggle the IsPrivate flag on a property.
    /// Admin: always.
    /// Owner: only if they are the last editor AND LastEditedById is set.
    /// If LastEditedById is null, the privacy option is not shown at all.
    /// </summary>
    public static bool CanTogglePrivacy(Property property, Guid? userId, bool isAdmin)
    {
        if (isAdmin)
        {
            return true;
        }

        if (property == null || !property.LastEditedById.HasValue)
        {
            return false;
        }

        return property.LastEditedById == userId;
    }

    /// <summary>
    /// Checks if the user can view a lease.
    /// Admin: all leases.
    /// Tenant: only their own leases.
    /// Owner: all leases on public properties + leases on their own private properties.
    /// </summary>
    public static bool CanViewLease(Lease lease, Guid? userId, bool isAdmin, bool isTenant)
    {
        if (isAdmin)
        {
            return true;
        }

        if (lease == null)
        {
            return false;
        }

        // Tenants can always see their own leases, regardless of property privacy
        if (isTenant && lease.TenantId == userId)
        {
            return true;
        }

        // For non-tenants (Owners), visibility depends on the associated property
        return CanViewProperty(lease.Property, userId, isAdmin);
    }

    /// <summary>
    /// Checks if the user can view a payment.
    /// Admin: all payments.
    /// Tenant: only payments on their own leases.
    /// Owner: all payments on public properties + payments on their own private properties.
    /// </summary>
    public static bool CanViewPayment(Payment payment, Guid? userId, bool isAdmin, bool isTenant)
    {
        if (isAdmin)
        {
            return true;
        }

        if (payment == null || payment.Lease == null)
        {
            return false;
        }

        // Tenants can always see payments on their own leases
        if (isTenant && payment.Lease.TenantId == userId)
        {
            return true;
        }

        // For non-tenants (Owners), visibility depends on the associated property
        return CanViewProperty(payment.Lease.Property, userId, isAdmin);
    }
}
