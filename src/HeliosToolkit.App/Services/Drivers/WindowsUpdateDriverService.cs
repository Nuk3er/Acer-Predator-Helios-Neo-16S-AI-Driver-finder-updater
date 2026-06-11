using System.Runtime.InteropServices;
using Serilog;

namespace HeliosToolkit.App.Services.Drivers;

/// <summary>A driver update found on Windows Update, optionally matched to a local device.</summary>
public sealed class DriverUpdateCandidate
{
    public required string Title { get; init; }
    public string? HardwareId { get; init; }
    public string? Manufacturer { get; init; }
    public string? DriverClass { get; init; }
    public DateTime? DriverDate { get; init; }
    public string? MatchedDeviceName { get; set; }

    /// <summary>Opaque IUpdate COM object, used when installing.</summary>
    internal object ComUpdate { get; init; } = null!;
}

public sealed record DriverInstallReport(int Succeeded, int Failed, bool RebootRequired, string? Error = null);

/// <summary>
/// Finds and installs missing/updated drivers through the Windows Update Agent COM API
/// (the same machinery "optional driver updates" uses) — this is the legitimate version
/// of what Driver Booster does. Searching and installing are blocking COM calls, so
/// everything runs on background threads.
/// </summary>
public sealed class WindowsUpdateDriverService
{
    public Task<IReadOnlyList<DriverUpdateCandidate>> SearchDriversAsync(CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<DriverUpdateCandidate>>(() =>
        {
            Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType is null)
            {
                throw new InvalidOperationException("Windows Update Agent is not available on this system.");
            }

            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online = true;
            // 2 = ssWindowsUpdate: query Microsoft's servers directly, even if WSUS/policy default differs.
            searcher.ServerSelection = 2;

            Log.Information("Searching Windows Update for drivers…");
            dynamic result = searcher.Search("IsInstalled=0 and Type='Driver'");

            var candidates = new List<DriverUpdateCandidate>();
            int count = result.Updates.Count;
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                dynamic update = result.Updates.Item(i);
                string title = (string)update.Title;
                string? hardwareId = null, manufacturer = null, driverClass = null;
                DateTime? driverDate = null;
                try
                {
                    // IWindowsDriverUpdate members — present on driver updates.
                    hardwareId = (string?)update.DriverHardwareID;
                    manufacturer = (string?)update.DriverManufacturer;
                    driverClass = (string?)update.DriverClass;
                    driverDate = (DateTime?)update.DriverVerDate;
                }
                catch (Exception e) when
                    (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    // Not a driver-typed update; keep the title-only candidate.
                }

                candidates.Add(new DriverUpdateCandidate
                {
                    Title = title,
                    HardwareId = hardwareId,
                    Manufacturer = manufacturer,
                    DriverClass = driverClass,
                    DriverDate = driverDate,
                    ComUpdate = update,
                });
            }

            Log.Information("Windows Update returned {Count} driver update(s)", candidates.Count);
            return candidates;
        }, ct);
    }

    public Task<DriverInstallReport> DownloadAndInstallAsync(
        IReadOnlyList<DriverUpdateCandidate> selected,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                Type sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
                    ?? throw new InvalidOperationException("Windows Update Agent unavailable.");
                dynamic session = Activator.CreateInstance(sessionType)!;

                Type collType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")
                    ?? throw new InvalidOperationException("Windows Update Agent unavailable.");
                dynamic collection = Activator.CreateInstance(collType)!;

                foreach (DriverUpdateCandidate candidate in selected)
                {
                    dynamic update = candidate.ComUpdate;
                    try
                    {
                        if (!(bool)update.EulaAccepted)
                        {
                            update.AcceptEula();
                        }
                    }
                    catch (COMException)
                    {
                    }

                    collection.Add(update);
                }

                if ((int)collection.Count == 0)
                {
                    return new DriverInstallReport(0, 0, false, "Nothing selected.");
                }

                progress?.Report($"Downloading {collection.Count} driver(s)…");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = collection;
                dynamic downloadResult = downloader.Download();
                Log.Information("WU download result: {Code}", (int)downloadResult.ResultCode);

                progress?.Report("Installing…");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = collection;
                dynamic installResult = installer.Install();

                int succeeded = 0, failed = 0;
                for (int i = 0; i < (int)collection.Count; i++)
                {
                    // 2 = orcSucceeded
                    if ((int)installResult.GetUpdateResult(i).ResultCode == 2)
                    {
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                        Log.Warning("Driver install failed: {Title} (result {Code})",
                            (string)collection.Item(i).Title, (int)installResult.GetUpdateResult(i).ResultCode);
                    }
                }

                bool reboot = (bool)installResult.RebootRequired;
                Log.Information("Driver install finished: {Ok} ok, {Bad} failed, reboot={Reboot}",
                    succeeded, failed, reboot);
                return new DriverInstallReport(succeeded, failed, reboot);
            }
            catch (COMException e)
            {
                Log.Error(e, "Windows Update driver install failed");
                string hint = unchecked((uint)e.HResult) == 0x80070422
                    ? "The Windows Update service (wuauserv) is disabled — enable it to install drivers this way."
                    : e.Message;
                return new DriverInstallReport(0, selected.Count, false, hint);
            }
        }, ct);
    }

}
