using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data.Queries.Dtos;
using RentTracker.Web.Models;

namespace RentTracker.Web.Data.Queries;

/// <summary>
/// SQLite-specific implementation of ISqlQueryService.
/// Uses EF Core's SqlQueryRaw for provider-specific SQL.
/// If the database provider is ever switched (e.g., to PostgreSQL),
/// replace this class with a PostgresQueryService that implements the same interface.
/// </summary>
public class SqliteQueryService : ISqlQueryService
{
    private readonly RentTrackerDbContext _context;

    public SqliteQueryService(RentTrackerDbContext context)
    {
        _context = context;
    }

    public Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int year, Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere("p", "l", userId, isAdmin, isTenant);

        // SQLite strftime returns string month; we cast to int in the query
        var sql = $@"
            SELECT CAST(strftime('%m', py.ForPeriod) AS INTEGER) AS Month, SUM(py.Amount) AS TotalRevenue
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE py.Status = 'Received'
                AND strftime('%Y', py.ForPeriod) = {{0}}
                AND {visibilityWhere}
            GROUP BY strftime('%m', py.ForPeriod)
            ORDER BY Month";

        return _context.Database.SqlQueryRaw<MonthlyRevenueDto>(sql, year.ToString()).ToListAsync();
    }

    public Task<List<PaymentStatusCountDto>> GetPaymentStatusCountsAsync(Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere("p", "l", userId, isAdmin, isTenant);

        var sql = $@"
            SELECT py.Status, COUNT(*) AS Count
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE {visibilityWhere}
            GROUP BY py.Status";

        return _context.Database.SqlQueryRaw<PaymentStatusCountDto>(sql).ToListAsync();
    }

    public async Task<PropertyOccupancyDto> GetPropertyOccupancyStatsAsync(Guid? userId, bool isAdmin)
    {
        var propertyVisibilityWhere = BuildPropertyVisibilityWhere("p", userId, isAdmin);

        // Total and disabled
        var totalsSql = $@"
            SELECT COUNT(*) AS Total, SUM(CASE WHEN p.IsEnabled = 0 THEN 1 ELSE 0 END) AS Disabled
            FROM Properties p
            WHERE {propertyVisibilityWhere}";

        var totals = await _context.Database.SqlQueryRaw<PropertyTotalRow>(totalsSql).FirstOrDefaultAsync();

        // Occupied (properties with at least one active lease)
        var occupiedSql = $@"
            SELECT COUNT(DISTINCT l.PropertyId) AS Occupied
            FROM Leases l
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE l.Status = 'Active'
                AND {propertyVisibilityWhere}";

        var occupied = await _context.Database.SqlQueryRaw<PropertyOccupiedRow>(occupiedSql).FirstOrDefaultAsync();

        int total = totals?.Total ?? 0;
        int disabled = totals?.Disabled ?? 0;
        int occupiedCount = occupied?.Occupied ?? 0;

        return new PropertyOccupancyDto
        {
            TotalProperties = total,
            OccupiedProperties = occupiedCount,
            AvailableProperties = total - occupiedCount - disabled,
            DisabledProperties = disabled
        };
    }

    public Task<List<PaymentDetailDto>> GetPaymentsInDateRangeAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? propertyId,
        Guid? userId,
        bool isAdmin,
        bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere("p", "l", userId, isAdmin, isTenant);

        var propertyFilter = propertyId.HasValue
            ? "AND l.PropertyId = {1}"
            : "";

        // SQLite compares ISO-8601 datetime strings lexicographically
        var sql = $@"
            SELECT
                py.Id,
                py.Amount,
                py.Status,
                py.ForPeriod,
                py.CreatedAt,
                p.Id AS PropertyId,
                p.Name AS PropertyName,
                p.IsPrivate,
                p.LastEditedById,
                l.TenantId AS LeaseTenantId
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            WHERE py.ForPeriod >= {{0}} AND py.ForPeriod <= {{1}}
                {propertyFilter}
                AND {visibilityWhere}
            ORDER BY py.ForPeriod DESC";

        var parameters = new List<object>
        {
            startDate.ToString("O"),
            endDate.ToString("O")
        };

        if (propertyId.HasValue)
        {
            parameters.Add(propertyId.Value.ToString());
        }

        return _context.Database.SqlQueryRaw<PaymentDetailDto>(sql, parameters.ToArray()).ToListAsync();
    }

    public Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(int count, Guid? userId, bool isAdmin, bool isTenant)
    {
        var visibilityWhere = BuildVisibilityWhere("p", "l", userId, isAdmin, isTenant);

        var sql = $@"
            SELECT
                py.Id,
                py.Amount,
                py.Status,
                py.ForPeriod,
                py.CreatedAt,
                p.Name AS PropertyName,
                u.Username AS TenantName
            FROM Payments py
            INNER JOIN Leases l ON py.LeaseId = l.Id
            INNER JOIN Properties p ON l.PropertyId = p.Id
            INNER JOIN Users u ON l.TenantId = u.Id
            WHERE {visibilityWhere}
            ORDER BY datetime(py.CreatedAt) DESC
            LIMIT {{0}}";

        return _context.Database.SqlQueryRaw<RecentPaymentDto>(sql, count.ToString()).ToListAsync();
    }

    // ---- Helper methods for visibility SQL ----

    private static string BuildVisibilityWhere(string propertyAlias, string leaseAlias, Guid? userId, bool isAdmin, bool isTenant)
    {
        if (isAdmin || !userId.HasValue)
        {
            return "1 = 1";
        }

        var userIdStr = userId.Value.ToString();

        if (isTenant)
        {
            return $"{leaseAlias}.TenantId = '{userIdStr}'";
        }

        // Owner: public properties OR private properties where they are the last editor
        return $"({propertyAlias}.IsPrivate = 0 OR {propertyAlias}.LastEditedById = '{userIdStr}')";
    }

    private static string BuildPropertyVisibilityWhere(string propertyAlias, Guid? userId, bool isAdmin)
    {
        if (isAdmin || !userId.HasValue)
        {
            return "1 = 1";
        }

        var userIdStr = userId.Value.ToString();
        return $"({propertyAlias}.IsPrivate = 0 OR {propertyAlias}.LastEditedById = '{userIdStr}')";
    }

    // Internal DTOs for raw SQL row shapes
    private sealed class PropertyTotalRow
    {
        public int Total { get; set; }
        public int Disabled { get; set; }
    }

    private sealed class PropertyOccupiedRow
    {
        public int Occupied { get; set; }
    }
}
