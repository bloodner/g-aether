using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace GHelper.WPF.Services
{
    public enum UpdateStatus
    {
        UpToDate,
        UpdateAvailable,
        CheckFailed,
    }

    public class UpdateCheckResult
    {
        public UpdateStatus Status { get; init; }
        public string CurrentVersion { get; init; } = "";
        public string? LatestVersion { get; init; }
        public string? ReleaseUrl { get; init; }
        public string? DownloadUrl { get; init; }
        public long DownloadSize { get; init; }
        public string? ReleaseBody { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class DownloadResult
    {
        public bool Success { get; init; }
        public string? FilePath { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class ReleaseEntry
    {
        public string Version { get; init; } = "";
        public DateTime PublishedAt { get; init; }
        public string Body { get; init; } = "";
        public string Url { get; init; } = "";
        public bool IsPrerelease { get; init; }
    }

    public static class UpdateService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/bloodner/g-aether/releases/latest";
        private const string AllReleasesApiUrl = "https://api.github.com/repos/bloodner/g-aether/releases";

        public static async Task<UpdateCheckResult> CheckAsync()
        {
            string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

            try
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(LatestReleaseApiUrl);
                var release = JsonSerializer.Deserialize<JsonElement>(json);

                string tag = release.GetProperty("tag_name").GetString() ?? "";
                string releaseUrl = release.GetProperty("html_url").GetString() ?? "";
                string body = release.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";

                string? downloadUrl = null;
                long downloadSize = 0;
                if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    for (int i = 0; i < assets.GetArrayLength(); i++)
                    {
                        string assetUrl = assets[i].GetProperty("browser_download_url").GetString() ?? "";
                        if (assetUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = assetUrl;
                            if (assets[i].TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Number)
                                downloadSize = sizeEl.GetInt64();
                            break;
                        }
                    }
                }

                bool isNewer = IsNewerVersion(tag, currentVersion);

                return new UpdateCheckResult
                {
                    Status = isNewer ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate,
                    CurrentVersion = currentVersion,
                    LatestVersion = tag,
                    ReleaseUrl = releaseUrl,
                    DownloadUrl = downloadUrl,
                    DownloadSize = downloadSize,
                    ReleaseBody = body,
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Update check failed: " + ex.Message);
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.CheckFailed,
                    CurrentVersion = currentVersion,
                    ErrorMessage = ex.Message,
                };
            }
        }

        /// <summary>
        /// Fetches the complete release history from GitHub for the changelog view.
        /// </summary>
        public static async Task<List<ReleaseEntry>> GetAllReleasesAsync()
        {
            var results = new List<ReleaseEntry>();
            try
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(AllReleasesApiUrl);
                var releases = JsonSerializer.Deserialize<JsonElement>(json);

                if (releases.ValueKind != JsonValueKind.Array) return results;

                for (int i = 0; i < releases.GetArrayLength(); i++)
                {
                    var rel = releases[i];
                    string tag = rel.GetProperty("tag_name").GetString() ?? "";
                    string body = rel.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
                    string url = rel.GetProperty("html_url").GetString() ?? "";
                    bool prerelease = rel.TryGetProperty("prerelease", out var preEl) && preEl.GetBoolean();
                    DateTime published = DateTime.UtcNow;
                    if (rel.TryGetProperty("published_at", out var pubEl) && pubEl.ValueKind == JsonValueKind.String)
                    {
                        DateTime.TryParse(pubEl.GetString(), out published);
                    }

                    results.Add(new ReleaseEntry
                    {
                        Version = tag,
                        PublishedAt = published,
                        Body = body,
                        Url = url,
                        IsPrerelease = prerelease,
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Changelog fetch failed: " + ex.Message);
            }
            return results;
        }

        /// <summary>
        /// Streams a release asset to <paramref name="destinationPath"/> and reports
        /// progress as a value in [0.0, 1.0]. Verifies final size against
        /// <paramref name="expectedSize"/> when provided (>0).
        /// </summary>
        public static async Task<DownloadResult> DownloadAsync(
            string url,
            string destinationPath,
            long expectedSize,
            IProgress<double>? progress,
            CancellationToken ct)
        {
            try
            {
                // Use a separate client — the check client has a 10s timeout which isn't enough for a multi-MB download.
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                client.DefaultRequestHeaders.Add("User-Agent", "G-Aether");

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long contentLength = response.Content.Headers.ContentLength ?? expectedSize;
                long totalForProgress = contentLength > 0 ? contentLength : expectedSize;

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                using (var input = await response.Content.ReadAsStreamAsync(ct))
                using (var output = File.Create(destinationPath))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    double lastReported = -1.0;
                    while ((read = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                        totalRead += read;
                        if (progress != null && totalForProgress > 0)
                        {
                            double pct = (double)totalRead / totalForProgress;
                            // Report only on meaningful change to avoid UI thrash.
                            if (pct - lastReported >= 0.01 || pct >= 1.0)
                            {
                                progress.Report(Math.Min(pct, 1.0));
                                lastReported = pct;
                            }
                        }
                    }
                }

                if (expectedSize > 0)
                {
                    var actual = new FileInfo(destinationPath).Length;
                    if (actual != expectedSize)
                    {
                        try { File.Delete(destinationPath); } catch { }
                        return new DownloadResult
                        {
                            Success = false,
                            ErrorMessage = $"Size mismatch: expected {expectedSize} bytes, got {actual}",
                        };
                    }
                }

                return new DownloadResult { Success = true, FilePath = destinationPath };
            }
            catch (OperationCanceledException)
            {
                try { File.Delete(destinationPath); } catch { }
                return new DownloadResult { Success = false, ErrorMessage = "Download cancelled" };
            }
            catch (Exception ex)
            {
                try { File.Delete(destinationPath); } catch { }
                Logger.WriteLine("Update download failed: " + ex.Message);
                return new DownloadResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "G-Aether");
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        /// <summary>
        /// Compares release tag against current version. Tags like "v0.1.0-alpha" are
        /// normalized by stripping "v" and any pre-release suffix before the numeric compare.
        /// </summary>
        private static bool IsNewerVersion(string tag, string currentVersion)
        {
            try
            {
                string normalizedTag = NormalizeVersion(tag);
                string normalizedCurrent = NormalizeVersion(currentVersion);

                if (Version.TryParse(normalizedTag, out var tagVer) &&
                    Version.TryParse(normalizedCurrent, out var curVer))
                {
                    return tagVer.CompareTo(curVer) > 0;
                }

                return !string.Equals(normalizedTag, normalizedCurrent, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";

            string v = version.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);

            int dashIdx = v.IndexOf('-');
            if (dashIdx >= 0) v = v.Substring(0, dashIdx);

            return v;
        }
    }
}
