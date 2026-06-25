using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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
    private readonly ILogger<MetaCloudWhatsAppService> _logger;
    private const string ApiVersion = "v21.0";

    public MetaCloudWhatsAppService(
        HttpClient httpClient,
        EncryptionHelper encryption,
        RentTrackerDbContext context,
        ILogger<MetaCloudWhatsAppService> logger)
    {
        _httpClient = httpClient;
        _encryption = encryption;
        _context = context;
        _logger = logger;
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

    /// <summary>
    /// Normalizes a phone number for Meta Cloud API.
    /// Meta sample shows numbers without + prefix (e.g., 59171687570).
    /// Strips spaces, dashes, parentheses, and + prefix.
    /// </summary>
    public static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        // Remove all non-digit characters including + (Meta sample shows digits only)
        var digits = phoneNumber.Trim();
        digits = Regex.Replace(digits, "[^0-9]", "");
        
        return digits;
    }

    public async Task<(bool Success, string? Error)> SendMessageAsync(string phoneNumber, string message)
    {
        var (token, phoneNumberId, error) = await GetCredentialsAsync();
        if (token == null || phoneNumberId == null)
        {
            return (false, error ?? "WhatsApp not configured");
        }

        // Normalize phone number (digits only, no + prefix per Meta sample)
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
        {
            return (false, "Phone number is empty after normalization");
        }

        try
        {
            var url = $"https://graph.facebook.com/{ApiVersion}/{phoneNumberId}/messages";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = normalizedPhone,
                type = "text",
                text = new
                {
                    body = message
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            
            // Console output for debugging (works regardless of ILogger config)
            Console.WriteLine($"[WhatsApp] Sending message to {normalizedPhone} via {url}");
            Console.WriteLine($"[WhatsApp] Payload: {jsonPayload}");
            
            _logger.LogInformation("Sending WhatsApp message to {PhoneNumber} via {Url}", normalizedPhone, url);
            _logger.LogDebug("Request payload: {Payload}", jsonPayload);

            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Console output for debugging
            Console.WriteLine($"[WhatsApp] Response status: {(int)response.StatusCode}");
            Console.WriteLine($"[WhatsApp] Response body: {responseBody}");
            
            _logger.LogInformation("WhatsApp API response status: {StatusCode}", (int)response.StatusCode);
            _logger.LogDebug("WhatsApp API response body: {ResponseBody}", responseBody);

            if (response.IsSuccessStatusCode)
            {
                // Meta API returns 200 even for some errors, check the response body
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var errorMessage = errorElement.TryGetProperty("message", out var msgElement)
                        ? msgElement.GetString()
                        : responseBody;
                    _logger.LogError("WhatsApp API returned error: {ErrorMessage}", errorMessage);
                    Console.WriteLine($"[WhatsApp] API error in body: {errorMessage}");
                    return (false, errorMessage);
                }

                _logger.LogInformation("WhatsApp message sent successfully to {PhoneNumber}", normalizedPhone);
                Console.WriteLine($"[WhatsApp] Message sent successfully to {normalizedPhone}");
                return (true, null);
            }
            else
            {
                var errorMessage = responseBody;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.TryGetProperty("message", out var msgElement)
                            ? msgElement.GetString() ?? responseBody
                            : responseBody;
                    }
                }
                catch
                {
                    // If response body isn't valid JSON, use the raw body
                }

                _logger.LogError("WhatsApp API error ({StatusCode}): {ErrorMessage}", (int)response.StatusCode, errorMessage);
                Console.WriteLine($"[WhatsApp] API error ({(int)response.StatusCode}): {errorMessage}");
                return (false, errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WhatsApp message to {PhoneNumber}", normalizedPhone);
            Console.WriteLine($"[WhatsApp] Exception: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> SendTestMessageAsync(string phoneNumber)
    {
        return await SendMessageAsync(phoneNumber, "This is a test message from RentTracker. If you receive this, WhatsApp notifications are configured correctly.");
    }
}