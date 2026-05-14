using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Data;

/// <summary>
/// EF Core query extension methods for filtering data based on user visibility rules.
/// These methods add SQL-safe WHERE clauses that can be translated by SQLite.
/// </summary>
public static class PropertyQueryExtensions
{
    /// <summary>
    /// Filters a properties query to only include properties visible to the given user.
    /// SQL-safe: uses boolean and nullable GUID comparisons only.
    /// </summary>
    public static IQueryable<Property> VisibleToUser(
        this IQueryable<Property> query,
        Guid? userId,
        bool isAdmin)
    {
        if (isAdmin || !userId.HasValue)
        {
            return query;
        }

        // Public properties OR private properties where LastEditedById matches the user
        return query.Where(p => !p.IsPrivate || p.LastEditedById == userId);
    }

    /// <summary>
    /// Filters a leases query to only include leases visible to the given user.
    /// Must be called AFTER including Property navigation.
    /// SQL-safe for public properties. For private properties, we need to be careful
    /// because SQLite may not handle complex nested comparisons well.
    /// </summary>
    public static IQueryable<Lease> VisibleToUser(
        this IQueryable<Lease> query,
        Guid? userId,
        bool isAdmin,
        bool isTenant)
    {
        if (isAdmin || !userId.HasValue)
        {
            return query;
        }

        // Tenants: always see their own leases
        // Owners: see leases on public properties, or on private properties they last-edited
        if (isTenant)
        {
            // Use .Value to compare Guid (not Guid?) for reliable EF Core translation
            return query.Where(l => l.TenantId == userId.Value);
        }

        // For Owners: public properties are always visible, private only if they are the last editor
        // We use Include(Property) first, then filter. SQLite can handle p.IsPrivate (bool) and p.LastEditedById (nullable guid) comparisons.
        return query.Where(l => !l.Property.IsPrivate || l.Property.LastEditedById == userId);
    }

    /// <summary>
    /// Filters a payments query to only include payments visible to the given user.
    /// Must be called AFTER including Lease.Property navigation.
    /// </summary>
    public static IQueryable<Payment> VisibleToUser(
        this IQueryable<Payment> query,
        Guid? userId,
        bool isAdmin,
        bool isTenant)
    {
        if (isAdmin || !userId.HasValue)
        {
            return query;
        }

        // Tenants: always see payments on their own leases
        if (isTenant)
        {
            // Use .Value to compare Guid (not Guid?) for reliable EF Core translation
            return query.Where(p => p.Lease.TenantId == userId.Value);
        }

        // For Owners: public properties are always visible, private only if they are the last editor
        return query.Where(p => !p.Lease.Property.IsPrivate || p.Lease.Property.LastEditedById == userId);
    }
}
