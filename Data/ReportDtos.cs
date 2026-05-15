namespace RentTracker.Web.Data;

public class MonthlyRevenueDto
{
    public int Month { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class PaymentStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PropertyOccupancyDto
{
    public int TotalProperties { get; set; }
    public int OccupiedProperties { get; set; }
    public int AvailableProperties { get; set; }
    public int DisabledProperties { get; set; }
}

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
