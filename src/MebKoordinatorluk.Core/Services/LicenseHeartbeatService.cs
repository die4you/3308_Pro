using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Services;

public class HeartbeatResult
{
    public bool Success { get; set; }
    public bool IsBlocked { get; set; }
    public string? Reason { get; set; }
}

public static class LicenseHeartbeatService
{
    public const string WebhookUrl = "https://script.google.com/macros/s/AKfycby0uiX6VYyd9VOBBVpk60VzG1UP2ysno8jJKd7ZWGosLf0x_Lqe-3ofw-DYchOuupB5Tw/exec";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Program açıldığında arka planda Google E-Tabloya kalp atışı (heartbeat) sinyali gönderir.
    /// Merkezi sunucu lisansı onaylamazsa (kara liste veya kaçak/kayıtsız kullanım), durumu bildirir.
    /// İnternet yoksa veya yavaşsa kullanıcıyı kesinlikle bekletmez ve hata fırlatmaz (offline safe).
    /// </summary>
    public static async Task<HeartbeatResult> SendHeartbeatAsync(
        LicenseInfo license,
        AppSettings settings,
        int studentCount,
        int enterpriseCount)
    {
        try
        {
            var payload = new
            {
                action = "heartbeat",
                hwid = license.MachineCode,
                schoolName = !string.IsNullOrWhiteSpace(settings.SchoolName) ? settings.SchoolName : license.SchoolName,
                cityDistrict = settings.DistrictAndCity,
                licenseStatus = license.Status.ToString(),
                endDate = license.EndDate?.ToString("dd.MM.yyyy") ?? "-",
                daysLeft = license.RemainingDays,
                studentCount = studentCount,
                enterpriseCount = enterpriseCount,
                version = "3308 Pro v2.6"
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(WebhookUrl, content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new HeartbeatResult { Success = false, IsBlocked = false };
            }

            var resBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(resBody))
            {
                return new HeartbeatResult { Success = true, IsBlocked = false };
            }

            using var doc = JsonDocument.Parse(resBody);
            var root = doc.RootElement;

            string status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
            string reason = root.TryGetProperty("reason", out var rProp) ? rProp.GetString() ?? "" : "";

            if (string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "revoked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "pirated", StringComparison.OrdinalIgnoreCase))
            {
                return new HeartbeatResult
                {
                    Success = true,
                    IsBlocked = true,
                    Reason = !string.IsNullOrWhiteSpace(reason) ? reason : "Merkezi kayıtlarla uyuşmayan lisans kullanımı tespit edildi."
                };
            }

            return new HeartbeatResult { Success = true, IsBlocked = false };
        }
        catch
        {
            // Sessiz hata yakalama: İnternet kesintisi durumunda kullanıcıyı asla bekletmez veya engellemez
            return new HeartbeatResult { Success = false, IsBlocked = false };
        }
    }

    /// <summary>
    /// Lisans üretildiğinde Google E-Tablo'nun 'Uretilen_Lisanslar' sayfasına kayıt gönderir.
    /// </summary>
    public static async Task<bool> SendLicenseCreatedAsync(
        string hwid,
        string schoolName,
        string cityDistrict,
        DateTime startDate,
        DateTime endDate,
        string licenseKey,
        string note)
    {
        try
        {
            var payload = new
            {
                action = "license_created",
                hwid = hwid,
                schoolName = schoolName,
                cityDistrict = cityDistrict,
                startDate = startDate.ToString("dd.MM.yyyy"),
                endDate = endDate.ToString("dd.MM.yyyy"),
                licenseKey = licenseKey,
                note = note
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(WebhookUrl, content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kullanıcının İletişim & Destek ekranından gönderdiği mesajı veya lisans talebini arka planda Google Webhook'a iletir.
    /// HWID, okul adı, ilçe/il, sürüm ve lisans durumu arka planda otomatik eklenir.
    /// </summary>
    public static async Task<(bool Success, string Message)> SendSupportMessageAsync(
        string senderName,
        string senderContact,
        string subject,
        string messageText,
        string hwid,
        string schoolName,
        string cityDistrict,
        string licenseStatus)
    {
        try
        {
            var payload = new
            {
                action = "support_message",
                senderName = senderName.Trim(),
                senderContact = senderContact.Trim(),
                subject = subject.Trim(),
                message = messageText.Trim(),
                hwid = hwid,
                schoolName = !string.IsNullOrWhiteSpace(schoolName) ? schoolName : "-",
                cityDistrict = !string.IsNullOrWhiteSpace(cityDistrict) ? cityDistrict : "-",
                licenseStatus = licenseStatus,
                version = "3308 Pro v2.6",
                sentAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(WebhookUrl, content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Destek talebiniz başarıyla iletildi. En kısa sürede geri dönüş sağlanacaktır.");
            }
            return (false, "Sunucuya ulaşılamadı. Lütfen e-posta (3308pro@gmail.com) ile iletişime geçiniz.");
        }
        catch (Exception ex)
        {
            return (false, "İletişim sırasında bir hata oluştu: " + ex.Message);
        }
    }
}
