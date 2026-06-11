using HeliosToolkit.App.Services.Hardware;
using HeliosToolkit.Core.Manifest;
using HeliosToolkit.Core.Versioning;

namespace HeliosToolkit.App.Services.Drivers;

public enum DriverRowState
{
    /// <summary>No version comparison possible — shows installed info and links.</summary>
    InfoOnly,
    UpToDate,
    UpdateAvailable,
    /// <summary>The detect rule matched no device on this machine.</summary>
    NotDetected,
    /// <summary>An online check (NVIDIA) failed.</summary>
    CheckFailed,
}

public sealed record DriverStatusRow(
    DriverComponent Component,
    DriverRowState State,
    string? InstalledVersion,
    string? LatestVersion,
    string? DownloadUrl,
    string? PageUrl,
    string? ReleaseNotesUrl,
    string? MatchedDeviceName,
    string StatusText);

/// <summary>Joins the manifest, the device inventory and NVIDIA's lookup into per-component status rows.</summary>
public sealed class DriverStatusService(NvidiaDriverApiClient nvidia)
{
    public async Task<IReadOnlyList<DriverStatusRow>> BuildAsync(
        DriverManifest manifest,
        IReadOnlyList<PnpDevice> devices,
        SystemSnapshot snapshot,
        CancellationToken ct = default)
    {
        var rows = new List<DriverStatusRow>(manifest.Components.Count);
        foreach (DriverComponent component in manifest.Components)
        {
            ct.ThrowIfCancellationRequested();
            rows.Add(component.Detect.Kind switch
            {
                DetectKind.NvidiaApi => await BuildNvidiaRowAsync(component, snapshot, ct),
                DetectKind.PnpDriverVersion => BuildPnpRow(component, devices),
                _ => new DriverStatusRow(
                    component, DriverRowState.InfoOnly, null, component.LatestVersion,
                    component.DownloadUrl, component.PageUrl, component.ReleaseNotesUrl, null,
                    "Manual check"),
            });
        }

        return rows;
    }

    private async Task<DriverStatusRow> BuildNvidiaRowAsync(
        DriverComponent component, SystemSnapshot snapshot, CancellationToken ct)
    {
        string installed = snapshot.GpuDriverGeForce;
        NvidiaDriverResult? latest = await nvidia.GetLatestAsync(snapshot.GpuName, snapshot.OsBuild, ct);

        if (latest is null)
        {
            return new DriverStatusRow(
                component, DriverRowState.CheckFailed, OrNull(installed), null,
                null, component.PageUrl, component.ReleaseNotesUrl, snapshot.GpuName,
                "NVIDIA lookup unavailable — use the website");
        }

        DriverRowState state = DriverRowState.InfoOnly;
        string status = $"Latest is {latest.Version}";
        if (installed.Length > 0)
        {
            try
            {
                int cmp = NvidiaVersion.CompareGeForce(installed, latest.Version);
                state = cmp < 0 ? DriverRowState.UpdateAvailable : DriverRowState.UpToDate;
                status = cmp < 0
                    ? $"Update available: {installed} → {latest.Version} ({latest.ReleaseDate})"
                    : $"Up to date ({installed})";
            }
            catch (FormatException)
            {
                // keep InfoOnly
            }
        }

        return new DriverStatusRow(
            component, state, OrNull(installed), latest.Version,
            latest.DownloadUrl, component.PageUrl, latest.DetailsUrl ?? component.ReleaseNotesUrl,
            snapshot.GpuName, status);
    }

    private static DriverStatusRow BuildPnpRow(DriverComponent component, IReadOnlyList<PnpDevice> devices)
    {
        PnpDevice? device = MatchDevice(component.Detect, devices);
        if (device is null)
        {
            return new DriverStatusRow(
                component, DriverRowState.NotDetected, null, component.LatestVersion,
                component.DownloadUrl, component.PageUrl, component.ReleaseNotesUrl, null,
                "No matching device found");
        }

        string? installed = device.DriverVersion;
        DriverRowState state = DriverRowState.InfoOnly;
        string status = installed is null ? "Installed (version unknown)" : $"Installed {installed}";

        if (installed is not null
            && DriverVersion.TryParse(installed, out DriverVersion installedVersion)
            && DriverVersion.TryParse(component.LatestVersion, out DriverVersion latestVersion))
        {
            if (installedVersion < latestVersion)
            {
                state = DriverRowState.UpdateAvailable;
                status = $"Update available: {installed} → {component.LatestVersion}";
            }
            else
            {
                state = DriverRowState.UpToDate;
                status = $"Up to date ({installed})";
            }
        }

        return new DriverStatusRow(
            component, state, installed, component.LatestVersion,
            component.DownloadUrl, component.PageUrl, component.ReleaseNotesUrl,
            device.Name, status);
    }

    private static PnpDevice? MatchDevice(DetectSpec spec, IReadOnlyList<PnpDevice> devices)
    {
        IEnumerable<PnpDevice> query = devices;

        if (!string.IsNullOrEmpty(spec.PnpClass))
        {
            query = query.Where(d => d.PnpClass.Equals(spec.PnpClass, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(spec.NameContains))
        {
            query = query.Where(d => d.Name.Contains(spec.NameContains, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(spec.HardwareIdPrefix))
        {
            query = query.Where(d => d.HardwareIds.Any(
                id => id.StartsWith(spec.HardwareIdPrefix, StringComparison.OrdinalIgnoreCase)));
        }

        // Prefer a device that actually reports a driver version.
        return query.OrderByDescending(d => d.DriverVersion is not null).FirstOrDefault();
    }

    private static string? OrNull(string s) => s.Length == 0 ? null : s;
}
