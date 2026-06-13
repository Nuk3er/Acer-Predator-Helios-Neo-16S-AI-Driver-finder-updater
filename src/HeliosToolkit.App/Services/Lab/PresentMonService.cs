using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.Core.Lab;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

/// <summary>
/// Captures FPS/frame-time data with Intel PresentMon (downloaded on demand, MIT).
/// The console exe and CLI/CSV format are pinned to a verified version.
/// </summary>
public sealed class PresentMonService(DownloadService downloads)
{
    private const string Version = "2.4.1";
    private const string DownloadUrl =
        "https://github.com/GameTechDev/PresentMon/releases/download/v2.4.1/PresentMon-2.4.1-x64.exe";
    private const string ExpectedSha256 = "d74183e7ae630f72cd3690be0373ecbfdc6cbb86578148aab8fa2a7166068f34";
    private const string SessionName = "HeliosBench";

    private string ExePath => Path.Combine(AppPaths.Tools, $"PresentMon-{Version}-x64.exe");

    public bool IsInstalled => File.Exists(ExePath) && VerifyHash(ExePath);

    /// <summary>Downloads PresentMon if missing; throws on hash mismatch.</summary>
    public async Task EnsureInstalledAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (IsInstalled)
        {
            return;
        }

        Log.Information("Downloading PresentMon {Version}", Version);
        DownloadOutcome outcome = await downloads.DownloadToAsync(DownloadUrl, AppPaths.Tools, progress, ct);

        if (!outcome.Sha256Hex.Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(outcome.FilePath);
            throw new InvalidOperationException(
                "Downloaded PresentMon did not match the expected checksum and was deleted. " +
                "Download it yourself from github.com/GameTechDev/PresentMon and place it in the tools folder.");
        }

        // DownloadToAsync names by URL; ensure the canonical name exists.
        if (!File.Exists(ExePath))
        {
            File.Move(outcome.FilePath, ExePath, overwrite: true);
        }
    }

    /// <summary>Runs a timed capture of one process; returns parsed frame stats.</summary>
    public async Task<(BenchStats Stats, IReadOnlyList<double> FrameTimes, string CsvPath)> CaptureAsync(
        string processName, int seconds, CancellationToken ct = default)
    {
        await EnsureInstalledAsync(ct: ct);

        string runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string csvPath = Path.Combine(AppPaths.BenchCsv, $"{runId}.csv");

        string args =
            $"--process_name \"{processName}\" --output_file \"{csvPath}\" " +
            $"--timed {seconds} --terminate_after_timed --stop_existing_session " +
            $"--session_name {SessionName} --no_console_stats";

        Log.Information("PresentMon capture: {Args}", args);
        ProcessResult result = await ProcessRunner.RunAsync(ExePath, args, ct);
        if (!File.Exists(csvPath))
        {
            throw new InvalidOperationException(
                $"PresentMon produced no data. Is '{processName}' running and presenting frames? " +
                $"(exit {result.ExitCode}) {result.StdErr}");
        }

        string csv = await File.ReadAllTextAsync(csvPath, ct);
        List<double> frameTimes = FrameStats.ParseFrameTimesCsv(csv);

        // Drop the first ~1 s of frames (load shock) when we have enough data.
        if (frameTimes.Count > 200)
        {
            double cumulative = 0;
            int skip = 0;
            while (skip < frameTimes.Count && cumulative < 1000.0)
            {
                cumulative += frameTimes[skip];
                skip++;
            }

            frameTimes = frameTimes.Skip(skip).ToList();
        }

        return (FrameStats.FromFrameTimes(frameTimes), frameTimes, csvPath);
    }

    /// <summary>Stops a capture early by terminating the named session.</summary>
    public Task StopAsync(CancellationToken ct = default) =>
        ProcessRunner.RunAsync(ExePath, $"--terminate_existing_session --session_name {SessionName}", ct);

    /// <summary>Processes that have a visible window — the bench game-picker source.</summary>
    public static IReadOnlyList<(string ProcessName, string Title)> RunningWindowedProcesses()
    {
        var list = new List<(string, string)>();
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                {
                    list.Add(($"{p.ProcessName}.exe", $"{p.MainWindowTitle}  ({p.ProcessName}.exe)"));
                }
            }
            catch (Exception)
            {
                // access denied to a protected process — skip
            }
            finally
            {
                p.Dispose();
            }
        }

        return list.DistinctBy(x => x.Item1).OrderBy(x => x.Item2).ToList();
    }

    private static bool VerifyHash(string path)
    {
        try
        {
            using FileStream fs = File.OpenRead(path);
            byte[] hash = SHA256.HashData(fs);
            return Convert.ToHexString(hash).Equals(ExpectedSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
