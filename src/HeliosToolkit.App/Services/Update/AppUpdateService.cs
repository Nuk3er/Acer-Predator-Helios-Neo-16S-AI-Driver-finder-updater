using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HeliosToolkit.Core.Update;
using Serilog;

namespace HeliosToolkit.App.Services.Update;

public sealed record AppUpdateCheck(bool UpdateAvailable, string? LatestTag, string CurrentVersion, string? ReleaseUrl);

/// <summary>Checks the GitHub Releases API for a newer tag. Never auto-installs.</summary>
public sealed class AppUpdateService(HttpClient http)
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/nuk3er/acer-predator-helios-neo-16s-ai-driver-finder-updater/releases/latest";

    public string CurrentVersion =>
        typeof(AppUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public async Task<AppUpdateCheck> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            GitHubRelease? release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: ct);
            bool newer = ReleaseVersion.IsNewer(release?.TagName, CurrentVersion);
            return new AppUpdateCheck(newer, release?.TagName, CurrentVersion, release?.HtmlUrl);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Log.Warning(e, "Update check failed");
            return new AppUpdateCheck(false, null, CurrentVersion, null);
        }
    }

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
