using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using System.Text.Json;

namespace RentTracker.Web.Services;

public class MetaCloudWhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly EncryptionHelper _encryption;
    private readonly RentTrackerDbContext _context;
    private readonly IConfiguration _configuration;
    private const string ApiVersion = "v21.0";

    public MetaCloudWhatsAppService(
        HttpClient httpClient,
        EncryptionHelper encryption,
        RentTrackerDbContext context,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _encryption = encryption;
        _context = context;
        _configuration = configuration;
    }

    private async Task<WhatsAppSettings?> GetSettingsAsync()
    {
        return await _context.WhatsAppSettings.FirstOrDefaultAsync();
    }

    private async Task<(string? Token, string? PhoneNumberId, string? Error)> GetCredentialsAsync()
    {
        var settings = await GetSettingsAsync();
        if (settings == null || string.IsNullOrEmpty(settings.AccessToken) || string.IsNullOrEmpty(settings.PhoneNumberId))
        {
            return (null, null, "WhatsApp settings not configured");
        }

        var decryptedToken = _encryption.Decrypt(settings.AccessToken);
        return (decryptedToken, settings.PhoneNumberId, null);
    }

    public async Task<(bool Success, string? Error)> SendMessageAsync(string phoneNumber, string message)
    {
        var (token, phoneNumberId, error) = await GetCredentialsAsync();
        if (token == null || phoneNumberId == null)
        {
            return (false, error ?? "WhatsApp not configured");
        }

        try
        {
            var url = $"https://graph.facebook.com/{ApiVersion}/{phoneNumberId}/messages";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phoneNumber,
                type = "text",
                text = new
                {
                    body = message
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }
            else
            {
                var errorJson = JsonDocument.Parse(responseBody);
                var errorMessage = errorJson.RootElement.TryGetProperty("error", out var errorObj)
                    ? errorObj.GetProperty("message").GetString()
                    : responseBody;
                return (false, errorMessage);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync(string testPhoneNumber)
    {
        return await SendMessageAsync(testPhoneNumber, "This is a test message from RentTracker. If you receive this, WhatsApp notifications are configured correctly.");
    }
}