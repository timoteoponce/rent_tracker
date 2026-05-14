using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;

namespace RentTracker.Web.Data;

/// <summary>
/// Centralized EF Core query compositions for common data access patterns.
/// Ensures Include chains and visibility filters are applied consistently across PageModels.
/// </summary>
public static class RentTrackerQueries
{
    /// <summary>
    /// Payments query with full Lease, Property, PropertyUnit, and Tenant navigation loaded.
    /// Use this for list/detail pages that need to display payment context.
    /// </summary>
    public static IQueryable<Payment> PaymentsWithLeaseInfo(this RentTrackerDbContext context)
        => context.Payments
            .Include(p => p.Lease)
            .ThenInclude(l => l.Property)
            .Include(p => p.Lease)
            .ThenInclude(l => l.PropertyUnit)
            .Include(p => p.Lease)
            .ThenInclude(l => l.Tenant);

    /// <summary>
    /// Leases query with Property, PropertyUnit, and Tenant navigation loaded.
    /// Use this for list/detail pages that need to display lease context.
    /// </summary>
    public static IQueryable<Lease> LeasesWithPropertyInfo(this RentTrackerDbContext context)
        => context.Leases
            .Include(l => l.Property)
            .Include(l => l.PropertyUnit)
            .Include(l => l.Tenant);

    /// <summary>
    /// Properties query with units included. Use this for edit/detail pages that manage units.
    /// </summary>
    public static IQueryable<Property> PropertiesWithUnits(this RentTrackerDbContext context)
        => context.Properties
            .Include(p => p.Units);
}
