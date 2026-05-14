namespace RentTracker.Web.Data.Queries.Dtos;

/// <summary>
/// Monthly revenue aggregate for chart/reporting.
/// </summary>
public class MonthlyRevenueDto
{
    public int Month { get; set; }
    public decimal TotalRevenue { get; set; }
}

/// <summary>
/// Payment status count aggregate for chart/reporting.
/// </summary>
public class PaymentStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// Property occupancy statistics for reporting.
/// </summary>
public class PropertyOccupancyDto
{
    public int TotalProperties { get; set; }
    public int OccupiedProperties { get; set; }
    public int AvailableProperties { get; set; }
    public int DisabledProperties { get; set; }
}

/// <summary>
/// Payment detail for the detailed report (raw SQL projection to avoid over-fetching).
/// </summary>
public class PaymentDetailDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ForPeriod { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public Guid? LastEditedById { get; set; }
    public Guid LeaseTenantId { get; set; }
}

/// <summary>
/// Recent payment for the dashboard (raw SQL projection to avoid over-fetching).
/// </summary>
public class RecentPaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ForPeriod { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}
