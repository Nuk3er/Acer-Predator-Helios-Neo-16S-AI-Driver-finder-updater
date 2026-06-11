using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Hardware;
using HeliosToolkit.Core.Manifest;
using Serilog;
using Wpf.Ui;

namespace HeliosToolkit.App.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly ManifestService _manifest;
    private readonly DeviceInventoryService _inventory;
    private readonly SystemInfoService _systemInfo;
    private readonly DriverStatusService _status;
    private readonly DownloadService _downloads;
    private readonly DriverHealthState _health;
    private readonly ISnackbarService _snackbar;

    private string _acerSupportUrl = "https://www.acer.com/us-en/support/product-support/PHN16S-71";
    private bool _scannedOnce;

    public DevicesViewModel(
        ManifestService manifest,
        DeviceInventoryService inventory,
        SystemInfoService systemInfo,
        DriverStatusService status,
        DownloadService downloads,
        DriverHealthState health,
        ISnackbarService snackbar)
    {
        _manifest = manifest;
        _inventory = inventory;
        _systemInfo = systemInfo;
        _status = status;
        _downloads = downloads;
        _health = health;
        _snackbar = snackbar;

        DevicesView = CollectionViewSource.GetDefaultView(AllDevices);
        DevicesView.Filter = FilterDevice;

        _ = ScanAsync();
    }

    public ObservableCollection<DriverComponentViewModel> DriverRows { get; } = new();

    public ObservableCollection<PnpDevice> ProblemDevices { get; } = new();

    public ObservableCollection<PnpDevice> AllDevices { get; } = new();

    public ICollectionView DevicesView { get; }

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanSummary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProblemBannerText))]
    [NotifyPropertyChangedFor(nameof(HasProblemDevices))]
    private int _problemCount;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedHardwareIds))]
    [NotifyPropertyChangedFor(nameof(HasSelectedDevice))]
    private PnpDevice? _selectedDevice;

    public bool HasProblemDevices => ProblemCount > 0;

    public string ProblemBannerText => ProblemCount == 1
        ? "1 device has a driver problem. Fix it first — a broken driver costs more performance than any tweak gains."
        : $"{ProblemCount} devices have driver problems. Fix these first — broken drivers cost more performance than any tweak gains.";

    public bool HasSelectedDevice => SelectedDevice is not null;

    public string SelectedHardwareIds => SelectedDevice?.HardwareIdsText ?? "";

    partial void OnSearchTextChanged(string value) => DevicesView.Refresh();

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        try
        {
            IsScanning = true;
            bool refresh = _scannedOnce;
            _scannedOnce = true;

            Task<SystemSnapshot> snapshotTask = _systemInfo.GetSnapshotAsync(refresh);
            Task<IReadOnlyList<PnpDevice>> devicesTask = _inventory.GetDevicesAsync(refresh);
            Task<DriverManifest> manifestTask = _manifest.GetManifestAsync(forceRefresh: false);

            await Task.WhenAll(snapshotTask, devicesTask, manifestTask);

            SystemSnapshot snapshot = snapshotTask.Result;
            IReadOnlyList<PnpDevice> devices = devicesTask.Result;
            DriverManifest manifest = manifestTask.Result;
            _acerSupportUrl = manifest.AcerSupportUrl ?? _acerSupportUrl;

            IReadOnlyList<DriverStatusRow> rows = await _status.BuildAsync(manifest, devices, snapshot);

            AllDevices.Clear();
            ProblemDevices.Clear();
            foreach (PnpDevice device in devices)
            {
                AllDevices.Add(device);
                if (device.HasProblem)
                {
                    ProblemDevices.Add(device);
                }
            }

            DriverRows.Clear();
            foreach (DriverStatusRow row in rows)
            {
                DriverRows.Add(new DriverComponentViewModel(row, _downloads, _snackbar));
            }

            ProblemCount = ProblemDevices.Count;
            int updates = rows.Count(r => r.State == DriverRowState.UpdateAvailable);
            bool nvidiaOutdated = rows.Any(r =>
                r.Component.Detect.Kind == DetectKind.NvidiaApi && r.State == DriverRowState.UpdateAvailable);
            _health.Update(ProblemCount, updates, nvidiaOutdated);

            ScanSummary =
                $"{devices.Count} devices · {ProblemCount} problem(s) · {updates} update(s) · manifest: {_manifest.LoadedFrom}";
        }
        catch (Exception e)
        {
            Log.Error(e, "Device scan failed");
            ScanSummary = "Scan failed — see logs.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void OpenAcerSupport()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_acerSupportUrl) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not open Acer support page");
        }
    }

    [RelayCommand]
    private void OpenDownloadsFolder()
    {
        try
        {
            Directory.CreateDirectory(DownloadService.DriversFolder);
            Process.Start(new ProcessStartInfo(DownloadService.DriversFolder) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not open downloads folder");
        }
    }

    [RelayCommand]
    private void CopyHardwareIds()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(SelectedDevice.HardwareIdsText);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Clipboard unavailable");
        }
    }

    private bool FilterDevice(object item)
    {
        if (item is not PnpDevice device || string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return device.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || device.PnpClass.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || device.HardwareIds.Any(id => id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    }
}
