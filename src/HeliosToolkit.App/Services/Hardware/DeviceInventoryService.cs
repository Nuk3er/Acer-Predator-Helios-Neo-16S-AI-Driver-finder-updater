using System.Management;
using Serilog;

namespace HeliosToolkit.App.Services.Hardware;

public sealed record PnpDevice
{
    public string Name { get; init; } = "";
    public string PnpClass { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public IReadOnlyList<string> HardwareIds { get; init; } = Array.Empty<string>();
    public int ProblemCode { get; init; }
    public string? DriverVersion { get; init; }
    public DateTime? DriverDate { get; init; }
    public string? InfName { get; init; }

    public bool HasProblem => ProblemCode != 0;

    public string ProblemText => ProblemCode == 0 ? "" : DeviceProblems.Describe(ProblemCode);

    public string DriverVersionText => DriverVersion ?? "—";

    public string DriverDateText => DriverDate?.ToString("yyyy-MM-dd") ?? "";

    public string HardwareIdsText => HardwareIds.Count == 0 ? "(none)" : string.Join(Environment.NewLine, HardwareIds);
}

public static class DeviceProblems
{
    public static string Describe(int code) => code switch
    {
        1 => "Device is not configured correctly (code 1)",
        3 => "Driver is corrupted or the system is low on resources (code 3)",
        9 => "Windows cannot identify this hardware (code 9)",
        10 => "Device cannot start (code 10)",
        12 => "Not enough free resources (code 12)",
        14 => "Restart required to finish setup (code 14)",
        18 => "Drivers need to be reinstalled (code 18)",
        19 => "Registry configuration is damaged (code 19)",
        21 => "Windows is removing the device (code 21)",
        22 => "Device is disabled (code 22)",
        24 => "Device not present or drivers missing (code 24)",
        28 => "Drivers are not installed (code 28)",
        29 => "Device disabled in firmware (code 29)",
        31 => "Device is not working properly (code 31)",
        32 => "Driver service is disabled (code 32)",
        37 => "Driver failed to initialize (code 37)",
        39 => "Driver is corrupted or missing (code 39)",
        43 => "Device reported a problem and was stopped (code 43)",
        _ => $"Device problem (code {code})",
    };
}

/// <summary>
/// Joins Win32_PnPEntity with Win32_PnPSignedDriver into a single inventory.
/// The signed-driver query is slow (seconds), so results are cached for the session.
/// </summary>
public sealed class DeviceInventoryService(WmiQueryService wmi)
{
    private IReadOnlyList<PnpDevice>? _cache;

    public async Task<IReadOnlyList<PnpDevice>> GetDevicesAsync(bool refresh = false)
    {
        if (_cache is not null && !refresh)
        {
            return _cache;
        }

        var entitiesTask = wmi.QueryAsync(
            "SELECT Name, PNPClass, DeviceID, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity");
        var driversTask = wmi.QueryAsync(
            "SELECT DeviceID, DriverVersion, DriverDate, InfName FROM Win32_PnPSignedDriver");

        await Task.WhenAll(entitiesTask, driversTask);

        var driverByDevice = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in driversTask.Result)
        {
            string? id = row.GetString("DeviceID");
            if (id is not null)
            {
                driverByDevice[id] = row;
            }
        }

        var devices = new List<PnpDevice>(entitiesTask.Result.Count);
        foreach (var row in entitiesTask.Result)
        {
            string deviceId = row.GetString("DeviceID") ?? "";
            driverByDevice.TryGetValue(deviceId, out var driver);

            devices.Add(new PnpDevice
            {
                Name = row.GetString("Name") ?? "(unnamed device)",
                PnpClass = row.GetString("PNPClass") ?? "",
                DeviceId = deviceId,
                HardwareIds = row.GetStringArray("HardwareID"),
                ProblemCode = (int)row.GetInt64("ConfigManagerErrorCode"),
                DriverVersion = driver?.GetString("DriverVersion"),
                DriverDate = ParseCimDate(driver?.GetString("DriverDate")),
                InfName = driver?.GetString("InfName"),
            });
        }

        _cache = devices
            .OrderBy(d => d.PnpClass, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cache;
    }

    private static DateTime? ParseCimDate(string? cim)
    {
        if (string.IsNullOrEmpty(cim))
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(cim).Date;
        }
        catch (Exception e)
        {
            Log.Debug(e, "Unparsable CIM date {Date}", cim);
            return null;
        }
    }
}
