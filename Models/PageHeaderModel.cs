namespace RentTracker.Web.Models;

public class PageHeaderModel
{
    public string Title { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string? ActionRouteId { get; set; }
}
