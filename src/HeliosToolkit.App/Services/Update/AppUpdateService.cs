using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Windows;
using HeliosToolkit.Core.Update;
using Serilog;

namespace HeliosToolkit.App.Services.Update;

public sealed record AppUpdateCheck(
    bool UpdateAvailable,
    string? LatestTag,
    string CurrentVersion,
    string? ReleaseUrl,
    string? ExeDownloadUrl,
    bool NoReleasesYet = false);

/// <summary>
/// Checks GitHub Releases for a newer tag and can download + self-swap the EXE.
/// The swap uses a tiny cmd script that waits for this process to exit, replaces
/// the file, and relaunches — the standard single-file self-update dance.
/// </summary>
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

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The repo simply has no published releases yet — not an error.
                return new AppUpdateCheck(false, null, CurrentVersion, null, null, NoReleasesYet: true);
            }

            response.EnsureSuccessStatusCode();

            GitHubRelease? release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: ct);
            bool newer = ReleaseVersion.IsNewer(release?.TagName, CurrentVersion);
            string? exeUrl = release?.Assets?
                .FirstOrDefault(a => a.Name?.Equals("HeliosToolkit.exe", StringComparison.OrdinalIgnoreCase) == true)
                ?.BrowserDownloadUrl;

            return new AppUpdateCheck(newer, release?.TagName, CurrentVersion, release?.HtmlUrl, exeUrl);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Log.Warning(e, "Update check failed");
            return new AppUpdateCheck(false, null, CurrentVersion, null, null);
        }
    }

    /// <summary>Downloads the new EXE, stages a swap script, launches it and exits the app.</summary>
    public async Task DownloadAndInstallAsync(string exeDownloadUrl, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the running EXE path.");
        string staged = Path.Combine(AppPaths.Cache, "HeliosToolkit.update.exe");

        using (HttpResponseMessage response =
            await http.GetAsync(exeDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            await using Stream source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = new FileStream(staged, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
            byte[] buffer = new byte[1 << 16];
            long written = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                written += read;
                if (total is > 0)
                {
                    progress?.Report((double)written / total.Value);
                }
            }
        }

        string script = Path.Combine(AppPaths.Cache, "helios-update.cmd");
        string pid = Environment.ProcessId.ToString();
        await File.WriteAllTextAsync(script, $"""
            @echo off
            title Helios Neo Toolkit update
            echo Waiting for Helios Neo Toolkit to close...
            :wait
            tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            copy /y "{staged}" "{currentExe}" >nul || (echo Update failed - the file may be locked. & pause & exit /b 1)
            del "{staged}" >nul 2>&1
            start "" "{currentExe}"
            del "%~f0"
            """, ct);

        Log.Information("Update staged; handing off to swap script and exiting");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        });

        Application.Current.Shutdown();
    }

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; init; }
    }

    private sealed record GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
