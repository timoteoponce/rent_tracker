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
            DryRunPhoneNumber = settings.DryRunPhoneNumber ?? "",
            BusinessAccountId = settings.BusinessAccountId ?? "",
            VerifyToken = settings.VerifyToken ?? "",
            EnablePaymentDueSoon = settings.EnablePaymentDueSoon,
            EnablePaymentToday = settings.EnablePaymentToday,
            EnablePaymentOverdue = settings.EnablePaymentOverdue,
            EnableOverdueToTenant = settings.EnableOverdueToTenant,
            EnableOverdueToLender = settings.EnableOverdueToLender,
            DueSoonDaysBefore = settings.DueSoonDaysBefore,
            TimeZoneOffset = settings.TimeZoneOffset,
            TestTemplateName = settings.TestTemplateName,
            PaymentDueSoonTemplateName = settings.PaymentDueSoonTemplateName,
            PaymentTodayTemplateName = settings.PaymentTodayTemplateName,
            PaymentOverdueTemplateName = settings.PaymentOverdueTemplateName,
            OverdueSummaryTemplateName = settings.OverdueSummaryTemplateName,
            TemplateLanguage = settings.TemplateLanguage,
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
        settings.DryRunPhoneNumber = string.IsNullOrWhiteSpace(Input.DryRunPhoneNumber) ? null : Input.DryRunPhoneNumber;
        settings.BusinessAccountId = string.IsNullOrWhiteSpace(Input.BusinessAccountId) ? null : Input.BusinessAccountId;
        settings.VerifyToken = string.IsNullOrWhiteSpace(Input.VerifyToken) ? null : Input.VerifyToken;
        settings.EnablePaymentDueSoon = Input.EnablePaymentDueSoon;
        settings.EnablePaymentToday = Input.EnablePaymentToday;
        settings.EnablePaymentOverdue = Input.EnablePaymentOverdue;
        settings.EnableOverdueToTenant = Input.EnableOverdueToTenant;
        settings.EnableOverdueToLender = Input.EnableOverdueToLender;
        settings.DueSoonDaysBefore = Input.DueSoonDaysBefore;
        settings.TimeZoneOffset = Input.TimeZoneOffset;
        settings.TestTemplateName = Input.TestTemplateName;
        settings.PaymentDueSoonTemplateName = Input.PaymentDueSoonTemplateName;
        settings.PaymentTodayTemplateName = Input.PaymentTodayTemplateName;
        settings.PaymentOverdueTemplateName = Input.PaymentOverdueTemplateName;
        settings.OverdueSummaryTemplateName = Input.OverdueSummaryTemplateName;
        settings.TemplateLanguage = Input.TemplateLanguage;
        settings.EnableIncomingBot = false;

        if (!string.IsNullOrWhiteSpace(Input.AccessToken) && Input.AccessToken != "********")
        {
            settings.AccessToken = _encryption.Encrypt(Input.AccessToken);
        }

        await _notificationService.SaveSettingsAsync(settings);

        TempData["SuccessMessage"] = "WhatsApp settings saved successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync([FromForm] string phoneNumber)
    {
        var settings = await _notificationService.GetSettingsAsync();
        if (settings == null || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new JsonResult(new { success = false, message = "Please provide a test phone number." });
        }

        if (string.IsNullOrWhiteSpace(settings.AccessToken) || string.IsNullOrWhiteSpace(settings.PhoneNumberId))
        {
            return new JsonResult(new { success = false, message = "Please configure the Access Token and Phone Number ID first." });
        }

        var (success, error) = await _whatsAppService.SendTestMessageAsync(phoneNumber);

        var message = success
            ? $"Test message sent successfully to {phoneNumber}!"
            : $"Test message failed: {error}";

        return new JsonResult(new { success, message });
    }

    public async Task<IActionResult> OnPostRunDryRunAsync([FromForm] string? testPhoneNumber)
    {
        var settings = await _notificationService.GetSettingsAsync();
        var phoneNumber = string.IsNullOrWhiteSpace(testPhoneNumber)
            ? settings?.DryRunPhoneNumber
            : testPhoneNumber;

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new JsonResult(new { success = false, message = "Please provide a dry-run phone number." });
        }

        var result = await _notificationService.ProcessDryRunAsync(phoneNumber);

        return new JsonResult(new
        {
            success = result.Success,
            message = result.Message,
            totalAttempted = result.TotalAttempted,
            totalSucceeded = result.TotalSucceeded,
            totalFailed = result.TotalFailed,
            types = result.Types.Select(t => new
            {
                t.Type,
                t.Attempted,
                t.Succeeded,
                t.Failed,
                t.Error
            })
        });
    }

    public class WhatsAppSettingsInput
    {
        public bool IsEnabled { get; set; }
        public string? AccessToken { get; set; } = "";
        public string? PhoneNumberId { get; set; } = "";
        public string? DryRunPhoneNumber { get; set; } = "";
        public string? BusinessAccountId { get; set; } = "";
        public string? VerifyToken { get; set; } = "";
        public bool EnablePaymentDueSoon { get; set; } = true;
        public bool EnablePaymentToday { get; set; } = true;
        public bool EnablePaymentOverdue { get; set; } = true;
        public bool EnableOverdueToTenant { get; set; } = true;
        public bool EnableOverdueToLender { get; set; } = true;
        [Range(1, 30)]
        public int DueSoonDaysBefore { get; set; } = 3;
        [Range(-12, 14)]
        public int TimeZoneOffset { get; set; } = -4;
        public string TestTemplateName { get; set; } = "renttracker_test";
        public string PaymentDueSoonTemplateName { get; set; } = "renttracker_payment_due_soon";
        public string PaymentTodayTemplateName { get; set; } = "renttracker_payment_today";
        public string PaymentOverdueTemplateName { get; set; } = "renttracker_payment_overdue";
        public string OverdueSummaryTemplateName { get; set; } = "renttracker_overdue_summary";
        public string TemplateLanguage { get; set; } = "en";
        public bool EnableIncomingBot { get; set; }
    }
}
