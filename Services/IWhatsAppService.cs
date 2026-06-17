namespace RentTracker.Web.Services;

public interface IWhatsAppService
{
    Task<(bool Success, string? Error)> SendMessageAsync(string phoneNumber, string message);
    Task<(bool Success, string? Error)> TestConnectionAsync(string testPhoneNumber);
}