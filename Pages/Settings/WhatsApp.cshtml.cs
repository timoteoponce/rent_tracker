using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using RentTracker.Web.Services;

namespace RentTracker.Web.Pages.Settings;

[Authorize(Roles = "Administrator")]
public class WhatsAppModel : PageModel
{
    private readonly INotificationService _notificationService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly EncryptionHelper _encryption;

    public WhatsAppModel(
        INotificationService notificationService,
        IWhatsAppService whatsAppService,
        EncryptionHelper encryption)
    {
        _notificationService = notificationService;
        _whatsAppService = whatsAppService;
        _encryption = encryption;
    }

    [BindProperty]
    public WhatsAppSettingsInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }

    public async Task OnGetAsync()
    {
        var settings = await _notificationService.GetSettingsAsync();
        if (settings == null)
        {
            settings = new WhatsAppSettings();
            await _notificationService.SaveSettingsAsync(settings);
        }

        Input = new WhatsAppSettingsInput
        {
            IsEnabled = settings.IsEnabled,
            AccessToken = string.IsNullOrEmpty(settings.AccessToken) ? "" : "********",
            PhoneNumberId = settings.PhoneNumberId ?? "",
            BusinessAccountId = settings.BusinessAccountId ?? "",
            VerifyToken = settings.VerifyToken ?? "",
            EnablePaymentDueSoon = settings.EnablePaymentDueSoon,
            EnablePaymentToday = settings.EnablePaymentToday,
            EnablePaymentOverdue = settings.EnablePaymentOverdue,
            EnableOverdueToTenant = settings.EnableOverdueToTenant,
            EnableOverdueToLender = settings.EnableOverdueToLender,
            DueSoonDaysBefore = settings.DueSoonDaysBefore,
            TimeZoneOffset = settings.TimeZoneOffset,
            EnableIncomingBot = false
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var settings = await _notificationService.GetSettingsAsync();
        if (settings == null)
        {
            settings = new WhatsAppSettings();
        }

        settings.IsEnabled = Input.IsEnabled;
        settings.PhoneNumberId = string.IsNullOrWhiteSpace(Input.PhoneNumberId) ? null : Input.PhoneNumberId;
        settings.BusinessAccountId = string.IsNullOrWhiteSpace(Input.BusinessAccountId) ? null : Input.BusinessAccountId;
        settings.VerifyToken = string.IsNullOrWhiteSpace(Input.VerifyToken) ? null : Input.VerifyToken;
        settings.EnablePaymentDueSoon = Input.EnablePaymentDueSoon;
        settings.EnablePaymentToday = Input.EnablePaymentToday;
        settings.EnablePaymentOverdue = Input.EnablePaymentOverdue;
        settings.EnableOverdueToTenant = Input.EnableOverdueToTenant;
        settings.EnableOverdueToLender = Input.EnableOverdueToLender;
        settings.DueSoonDaysBefore = Input.DueSoonDaysBefore;
        settings.TimeZoneOffset = Input.TimeZoneOffset;
        settings.EnableIncomingBot = false;

        if (!string.IsNullOrWhiteSpace(Input.AccessToken) && Input.AccessToken != "********")
        {
            settings.AccessToken = _encryption.Encrypt(Input.AccessToken);
        }

        await _notificationService.SaveSettingsAsync(settings);

        TempData["SuccessMessage"] = "WhatsApp settings saved successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        var settings = await _notificationService.GetSettingsAsync();
        if (settings == null || string.IsNullOrWhiteSpace(Input.TestPhoneNumber))
        {
            TestSuccess = false;
            TestResult = "Please save the settings first and provide a test phone number.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(settings.AccessToken) || string.IsNullOrWhiteSpace(settings.PhoneNumberId))
        {
            TestSuccess = false;
            TestResult = "Please configure the Access Token and Phone Number ID first.";
            return Page();
        }

        var (success, error) = await _whatsAppService.TestConnectionAsync(Input.TestPhoneNumber);

        TestSuccess = success;
        TestResult = success
            ? $"Test message sent successfully to {Input.TestPhoneNumber}!"
            : $"Test message failed: {error}";

        return Page();
    }

    public class WhatsAppSettingsInput
    {
        public bool IsEnabled { get; set; }
        public string AccessToken { get; set; } = "";
        public string PhoneNumberId { get; set; } = "";
        public string BusinessAccountId { get; set; } = "";
        public string VerifyToken { get; set; } = "";
        public bool EnablePaymentDueSoon { get; set; } = true;
        public bool EnablePaymentToday { get; set; } = true;
        public bool EnablePaymentOverdue { get; set; } = true;
        public bool EnableOverdueToTenant { get; set; } = true;
        public bool EnableOverdueToLender { get; set; } = true;
        [Range(1, 30)]
        public int DueSoonDaysBefore { get; set; } = 3;
        [Range(-12, 14)]
        public int TimeZoneOffset { get; set; } = -4;
        public bool EnableIncomingBot { get; set; }
        public string TestPhoneNumber { get; set; } = "";
    }
}
