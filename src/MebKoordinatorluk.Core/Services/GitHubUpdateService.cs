using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MebKoordinatorluk.Core.Services;

public class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "2.8.0";
    public string LatestVersion { get; set; } = "";
    public string ReleaseTitle { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string? HtmlUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes <= 0) return "";
            double mb = FileSizeBytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }
    }
}

public class GitHubUpdateService
{
    public const string CurrentVersion = "2.8.0";
    public const string RepoOwner = "die4you";
    public const string RepoName = "3308_Pro";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static GitHubUpdateService()
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "3308-Pro-Updater");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = CurrentVersion
        };

        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Henüz GitHub'da bir Release oluşturulmamış, bu durumda yazılım en güncel sürümdedir
                result.Success = true;
                result.IsUpdateAvailable = false;
                result.LatestVersion = CurrentVersion;
                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.ErrorMessage = $"Sunucu yanıt kodu: {(int)response.StatusCode} ({response.ReasonPhrase})";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string title = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : tagName;
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var htmlElem) ? htmlElem.GetString() ?? "" : "";

            DateTime? pubDate = null;
            if (root.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTime(out var dt))
            {
                pubDate = dt;
            }

            // Asset içinden .exe kurulum dosyasını bul
            string? downloadUrl = null;
            long fileSize = 0;
            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElem.EnumerateArray())
                {
                    var aName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() : null;
                        fileSize = asset.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                        break;
                    }
                }
            }

            // Eğer asset bulunamazsa ana release linkini kullan
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                downloadUrl = htmlUrl;
            }

            var currentVer = ParseVersion(CurrentVersion);
            var remoteVer = ParseVersion(tagName);

            result.Success = true;
            result.LatestVersion = tagName.TrimStart('v', 'V');
            result.ReleaseTitle = string.IsNullOrWhiteSpace(title) ? $"Sürüm {tagName}" : title;
            result.ReleaseNotes = body;
            result.DownloadUrl = downloadUrl;
            result.HtmlUrl = htmlUrl;
            result.FileSizeBytes = fileSize;
            result.PublishedAt = pubDate;
            result.IsUpdateAvailable = remoteVer > currentVer;

            return result;
        }
        catch (HttpRequestException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"İnternet bağlantısı kurulamadı: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Güncelleme kontrolü sırasında hata: {ex.Message}";
            return result;
        }
    }

    public async Task<string> DownloadUpdateAsync(
        string downloadUrl,
        IProgress<(long bytesDownloaded, long totalBytes, int percent)>? progress = null,
        CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"3308_Pro_Guncelleme_{DateTime.Now:yyyyMMddHHmmss}.exe");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
        using var destStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await destStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;

            if (totalBytes > 0)
            {
                int percent = (int)((totalRead * 100) / totalBytes);
                progress?.Report((totalRead, totalBytes, percent));
            }
        }

        return tempFile;
    }

    public static Version ParseVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new Version(0, 0, 0, 0);

        raw = raw.Trim().TrimStart('v', 'V');
        if (Version.TryParse(raw, out var v))
        {
            return new Version(
                Math.Max(0, v.Major),
                Math.Max(0, v.Minor),
                Math.Max(0, v.Build),
                Math.Max(0, v.Revision));
        }

        var match = Regex.Match(raw, @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
        if (match.Success)
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            int revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            return new Version(major, minor, build, revision);
        }

        return new Version(0, 0, 0, 0);
    }
}
