using RentTracker.Web.Data.Queries.Dtos;

namespace RentTracker.Web.Data.Queries;

/// <summary>
/// SQL-agnostic query service for performance-critical aggregations and reports.
/// Implementations are provider-specific (SQLite, PostgreSQL, etc.).
/// PageModels depend on this interface, not on any specific provider.
/// </summary>
public interface ISqlQueryService
{
    /// <summary>
    /// Gets monthly revenue totals for a given year.
    /// Returns 12 rows (one per month), even if revenue is zero.
    /// </summary>
    Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int year, Guid? userId, bool isAdmin, bool isTenant);

    /// <summary>
    /// Gets payment status counts across all visible payments.
    /// </summary>
    Task<List<PaymentStatusCountDto>> GetPaymentStatusCountsAsync(Guid? userId, bool isAdmin, bool isTenant);

    /// <summary>
    /// Gets property occupancy statistics for the dashboard.
    /// </summary>
    Task<PropertyOccupancyDto> GetPropertyOccupancyStatsAsync(Guid? userId, bool isAdmin);

    /// <summary>
    /// Gets payments within a date range, with visibility filtering applied in SQL.
    /// Returns a flat DTO projection suitable for grouping in memory.
    /// </summary>
    Task<List<PaymentDetailDto>> GetPaymentsInDateRangeAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? propertyId,
        Guid? userId,
        bool isAdmin,
        bool isTenant);

    /// <summary>
    /// Gets the most recent payments for the dashboard, ordered by creation date.
    /// </summary>
    Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(int count, Guid? userId, bool isAdmin, bool isTenant);
}
