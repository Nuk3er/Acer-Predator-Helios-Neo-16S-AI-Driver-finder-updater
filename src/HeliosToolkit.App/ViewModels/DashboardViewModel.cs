using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Hardware;
using HeliosToolkit.App.Views.Pages;
using Serilog;
using Wpf.Ui;

namespace HeliosToolkit.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SystemInfoService _systemInfo;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private SystemSnapshot? _snapshot;

    public DriverHealthState Health { get; }

    public BoostViewModel Boost { get; }

    public DashboardViewModel(
        SystemInfoService systemInfo, INavigationService navigation, DriverHealthState health, BoostViewModel boost)
    {
        _systemInfo = systemInfo;
        _navigation = navigation;
        Health = health;
        Boost = boost;
        _ = LoadAsync(refresh: false);
    }

    // ----- derived display strings -----

    public string ModelLine => Join(Snapshot?.Manufacturer, Snapshot?.Model);
    public string FamilyLine => Snapshot?.SystemFamily ?? "";
    public string SerialLine => Snapshot?.SerialNumber is { Length: > 0 } s ? $"S/N {s}" : "";
    public string BiosLine => Snapshot?.BiosVersion is { Length: > 0 } b ? $"BIOS {b}" : "";
    public string OsLine => Snapshot is { } x && x.OsCaption.Length > 0
        ? $"{x.OsCaption} {x.OsDisplayVersion}".Trim() + (x.OsBuild > 0 ? $" (build {x.OsBuild})" : "")
        : "";

    public string CpuLine => Snapshot?.CpuName ?? "";
    public string CpuCoresLine => Snapshot is { CpuCores: > 0 } x ? $"{x.CpuCores} cores / {x.CpuThreads} threads" : "";

    public string GpuLine => Snapshot is { } x && x.GpuName.Length > 0 ? x.GpuName : "No NVIDIA GPU detected";
    public string GpuDriverLine => Snapshot is { } x && x.GpuDriverGeForce.Length > 0
        ? $"GeForce driver {x.GpuDriverGeForce}"
        : Snapshot?.GpuDriverWmi is { Length: > 0 } v ? $"Driver {v}" : "";
    public string IgpuLine => Snapshot?.IgpuName is { Length: > 0 } i ? $"iGPU: {i}" : "";

    public string RamLine => Snapshot is { RamGb: > 0 } x
        ? $"{x.RamGb:0} GB" + (x.RamSpeed > 0 ? $" @ {x.RamSpeed} MT/s" : "")
        : "";

    public string DisksLine => Snapshot is { } x && x.Disks.Count > 0
        ? string.Join(Environment.NewLine, x.Disks.Select(d => $"{d.Name} — {d.SizeGb:0} GB{(d.IsNvme ? " (NVMe)" : "")}"))
        : "";

    public string DisplayLine => Snapshot is { DisplayWidth: > 0 } x
        ? $"{x.DisplayWidth} × {x.DisplayHeight} @ {x.DisplayRefreshHz} Hz"
        : "";

    public string PowerLine => Snapshot is { } x
        ? (x.OnAcPower ? "Plugged in" : "On battery") + (x.BatteryPercent >= 0 ? $" — battery {x.BatteryPercent}%" : "")
        : "";

    public bool ShowModelWarning => Snapshot is { } x && x.Model.Length > 0 && !x.IsTargetModel;
    public bool ShowModelVerified => Snapshot is { IsTargetModel: true };

    public string ModelWarningText => Snapshot is { } x
        ? $"This machine reports model \"{x.Model}\". The toolkit's curated driver data targets the {SystemInfoService.TargetModel}; " +
          "everything still works, but \"latest driver\" info may not apply to this hardware."
        : "";

    partial void OnSnapshotChanged(SystemSnapshot? value)
    {
        OnPropertyChanged(string.Empty); // refresh every derived property
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(refresh: true);

    [RelayCommand]
    private void OpenDevices() => _navigation.Navigate(typeof(DevicesPage));

    [RelayCommand]
    private void OpenNvidia() => _navigation.Navigate(typeof(NvidiaPage));

    [RelayCommand]
    private void OpenTweaks() => _navigation.Navigate(typeof(WindowsTweaksPage));

    private async Task LoadAsync(bool refresh)
    {
        try
        {
            IsLoading = true;
            Snapshot = await _systemInfo.GetSnapshotAsync(refresh);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load system snapshot");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string Join(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
