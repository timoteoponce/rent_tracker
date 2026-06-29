namespace RentTracker.Web.Models;

public class NotificationDryRunResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalAttempted { get; set; }
    public int TotalSucceeded { get; set; }
    public int TotalFailed { get; set; }
    public List<NotificationDryRunTypeResult> Types { get; set; } = new();
}

public class NotificationDryRunTypeResult
{
    public string Type { get; set; } = string.Empty;
    public int Attempted { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public string? Error { get; set; }
}
