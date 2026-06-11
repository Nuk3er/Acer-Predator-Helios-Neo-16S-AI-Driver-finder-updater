using HeliosToolkit.App.Services.System;
using HeliosToolkit.Core.Versioning;
using Serilog;

namespace HeliosToolkit.App.Services.Hardware;

public sealed record DiskSnapshot(string Name, double SizeGb, bool IsNvme);

public sealed record SystemSnapshot
{
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string SystemFamily { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public string BiosVersion { get; init; } = "";

    public string CpuName { get; init; } = "";
    public int CpuCores { get; init; }
    public int CpuThreads { get; init; }

    public string GpuName { get; init; } = "";
    public string GpuDriverWmi { get; init; } = "";
    public string GpuDriverGeForce { get; init; } = "";
    public string GpuPnpDeviceId { get; init; } = "";
    public string IgpuName { get; init; } = "";

    public double RamGb { get; init; }
    public int RamSpeed { get; init; }

    public IReadOnlyList<DiskSnapshot> Disks { get; init; } = Array.Empty<DiskSnapshot>();

    public string OsCaption { get; init; } = "";
    public int OsBuild { get; init; }
    public string OsDisplayVersion { get; init; } = "";

    public int DisplayWidth { get; init; }
    public int DisplayHeight { get; init; }
    public int DisplayRefreshHz { get; init; }

    public bool OnAcPower { get; init; }
    public int BatteryPercent { get; init; }

    /// <summary>True when this machine is the laptop the toolkit is built for.</summary>
    public bool IsTargetModel => Model.Contains("PHN16S-71", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Collects the dashboard system summary from WMI and Win32.</summary>
public sealed class SystemInfoService(WmiQueryService wmi)
{
    public const string TargetModel = "PHN16S-71";

    private SystemSnapshot? _cached;

    public async Task<SystemSnapshot> GetSnapshotAsync(bool refresh = false)
    {
        if (_cached is not null && !refresh)
        {
            return _cached;
        }

        var system = wmi.QueryAsync("SELECT Manufacturer, Model, SystemFamily FROM Win32_ComputerSystem");
        var product = wmi.QueryAsync("SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct");
        var bios = wmi.QueryAsync("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
        var cpu = wmi.QueryAsync("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        var gpus = wmi.QueryAsync("SELECT Name, DriverVersion, PNPDeviceID FROM Win32_VideoController");
        var ram = wmi.QueryAsync("SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
        var os = wmi.QueryAsync("SELECT Caption, BuildNumber FROM Win32_OperatingSystem");
        var disks = QueryDisksSafeAsync();

        try
        {
            await Task.WhenAll(system, product, bios, cpu, gpus, ram, os, disks);
        }
        catch (Exception e)
        {
            // Individual failures are tolerated below; log the first one for diagnosis.
            Log.Warning(e, "One or more WMI queries failed while building the system snapshot");
        }

        var systemRow = FirstOrEmpty(system);
        var cpuRow = FirstOrEmpty(cpu);
        var osRow = FirstOrEmpty(os);

        string gpuName = "", gpuDriverWmi = "", gpuGeForce = "", gpuPnp = "", igpuName = "";
        foreach (var row in RowsOrEmpty(gpus))
        {
            string name = row.GetString("Name") ?? "";
            if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                gpuName = name;
                gpuDriverWmi = row.GetString("DriverVersion") ?? "";
                gpuPnp = row.GetString("PNPDeviceID") ?? "";
                if (NvidiaVersion.TryFromWmiVersion(gpuDriverWmi, out string geforce))
                {
                    gpuGeForce = geforce;
                }
            }
            else if (name.Length > 0 && igpuName.Length == 0)
            {
                igpuName = name;
            }
        }

        long totalRamBytes = 0;
        int ramSpeed = 0;
        foreach (var row in RowsOrEmpty(ram))
        {
            totalRamBytes += row.GetInt64("Capacity");
            ramSpeed = Math.Max(ramSpeed, (int)row.GetInt64("ConfiguredClockSpeed"));
            if (ramSpeed == 0)
            {
                ramSpeed = (int)row.GetInt64("Speed");
            }
        }

        int build = (int)FirstOrEmpty(os).GetInt64("BuildNumber");
        (int width, int height, int hz) = DisplayInfo.GetPrimaryDisplayMode();

        _cached = new SystemSnapshot
        {
            Manufacturer = systemRow.GetString("Manufacturer") ?? "",
            Model = systemRow.GetString("Model") ?? "",
            SystemFamily = systemRow.GetString("SystemFamily") ?? "",
            SerialNumber = FirstOrEmpty(product).GetString("IdentifyingNumber") ?? "",
            BiosVersion = FirstOrEmpty(bios).GetString("SMBIOSBIOSVersion") ?? "",
            CpuName = cpuRow.GetString("Name") ?? "",
            CpuCores = (int)cpuRow.GetInt64("NumberOfCores"),
            CpuThreads = (int)cpuRow.GetInt64("NumberOfLogicalProcessors"),
            GpuName = gpuName,
            GpuDriverWmi = gpuDriverWmi,
            GpuDriverGeForce = gpuGeForce,
            GpuPnpDeviceId = gpuPnp,
            IgpuName = igpuName,
            RamGb = Math.Round(totalRamBytes / 1024.0 / 1024.0 / 1024.0, 0),
            RamSpeed = ramSpeed,
            Disks = disks.IsCompletedSuccessfully ? disks.Result : Array.Empty<DiskSnapshot>(),
            OsCaption = osRow.GetString("Caption") ?? "",
            OsBuild = build,
            OsDisplayVersion = BuildToDisplayVersion(build),
            DisplayWidth = width,
            DisplayHeight = height,
            DisplayRefreshHz = hz,
            OnAcPower = PowerStatus.IsOnAcPower(),
            BatteryPercent = PowerStatus.BatteryPercent(),
        };

        return _cached;
    }

    private async Task<IReadOnlyList<DiskSnapshot>> QueryDisksSafeAsync()
    {
        try
        {
            var rows = await wmi.QueryAsync(
                "SELECT FriendlyName, Size, BusType FROM MSFT_PhysicalDisk",
                @"root\Microsoft\Windows\Storage");

            return rows
                .Select(r => new DiskSnapshot(
                    r.GetString("FriendlyName") ?? "Disk",
                    Math.Round(r.GetInt64("Size") / 1000.0 / 1000.0 / 1000.0, 0),
                    r.GetInt64("BusType") == 17))
                .ToList();
        }
        catch (Exception e)
        {
            Log.Warning(e, "MSFT_PhysicalDisk query failed");
            return Array.Empty<DiskSnapshot>();
        }
    }

    private static string BuildToDisplayVersion(int build) => build switch
    {
        >= 26200 => "25H2",
        >= 26100 => "24H2",
        >= 22631 => "23H2",
        >= 22621 => "22H2",
        >= 22000 => "21H2",
        > 0 => "Windows 10",
        _ => "",
    };

    private static Dictionary<string, object?> FirstOrEmpty(Task<List<Dictionary<string, object?>>> task) =>
        task.IsCompletedSuccessfully && task.Result.Count > 0 ? task.Result[0] : new Dictionary<string, object?>();

    private static List<Dictionary<string, object?>> RowsOrEmpty(Task<List<Dictionary<string, object?>>> task) =>
        task.IsCompletedSuccessfully ? task.Result : new List<Dictionary<string, object?>>();
}
