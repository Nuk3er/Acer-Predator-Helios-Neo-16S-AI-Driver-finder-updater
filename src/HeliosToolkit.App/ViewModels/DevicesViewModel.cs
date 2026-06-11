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

    private readonly WindowsUpdateDriverService _windowsUpdate;

    public DevicesViewModel(
        ManifestService manifest,
        DeviceInventoryService inventory,
        SystemInfoService systemInfo,
        DriverStatusService status,
        DownloadService downloads,
        DriverHealthState health,
        ISnackbarService snackbar,
        WindowsUpdateDriverService windowsUpdate)
    {
        _manifest = manifest;
        _inventory = inventory;
        _systemInfo = systemInfo;
        _status = status;
        _downloads = downloads;
        _health = health;
        _snackbar = snackbar;
        _windowsUpdate = windowsUpdate;

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

    // ───────────── Driver fixes via Windows Update (Driver Booster-style) ─────────────

    public ObservableCollection<DriverUpdateViewModel> FoundDriverUpdates { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFoundUpdates))]
    private bool _hasSearchedWu;

    [ObservableProperty]
    private bool _isWuBusy;

    [ObservableProperty]
    private string _wuStatus =
        "Windows Update can find drivers for problem devices and install them silently — " +
        "or open the Microsoft Update Catalog per device to download manually.";

    public bool HasFoundUpdates => HasSearchedWu && FoundDriverUpdates.Count > 0;

    [RelayCommand]
    private async Task FindDriversAsync()
    {
        if (IsWuBusy)
        {
            return;
        }

        try
        {
            IsWuBusy = true;
            WuStatus = "Searching Windows Update for drivers… this can take a minute.";
            IReadOnlyList<DriverUpdateCandidate> candidates = await _windowsUpdate.SearchDriversAsync();

            int matched = 0;
            FoundDriverUpdates.Clear();
            foreach (DriverUpdateCandidate candidate in candidates)
            {
                if (candidate.HardwareId is { Length: > 0 } hardwareId)
                {
                    PnpDevice? device = ProblemDevices.FirstOrDefault(d =>
                        d.HardwareIds.Any(id =>
                            id.Equals(hardwareId, StringComparison.OrdinalIgnoreCase) ||
                            id.StartsWith(hardwareId, StringComparison.OrdinalIgnoreCase) ||
                            hardwareId.StartsWith(id, StringComparison.OrdinalIgnoreCase)));
                    if (device is not null)
                    {
                        candidate.MatchedDeviceName = device.Name;
                        matched++;
                    }
                }

                FoundDriverUpdates.Add(new DriverUpdateViewModel(candidate));
            }

            HasSearchedWu = true;
            OnPropertyChanged(nameof(HasFoundUpdates));
            WuStatus = candidates.Count == 0
                ? "Windows Update has no driver updates for this machine. For the remaining problem devices, " +
                  "use the per-device Catalog button or Acer's support page."
                : $"{candidates.Count} driver update(s) found, {matched} matching problem device(s). " +
                  "Untick anything you don't want, then install.";
        }
        catch (Exception e)
        {
            Log.Error(e, "Windows Update driver search failed");
            WuStatus = unchecked((uint)e.HResult) == 0x80070422
                ? "The Windows Update service (wuauserv) is disabled — your debloat likely turned it off. " +
                  "Re-enable it (services.msc → Windows Update → Manual) and try again."
                : $"Driver search failed: {e.Message}";
        }
        finally
        {
            IsWuBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallSelectedDriversAsync()
    {
        var selected = FoundDriverUpdates.Where(u => u.IsSelected).Select(u => u.Candidate).ToList();
        if (selected.Count == 0 || IsWuBusy)
        {
            return;
        }

        try
        {
            IsWuBusy = true;
            var progress = new Progress<string>(s => WuStatus = s);
            DriverInstallReport report = await _windowsUpdate.DownloadAndInstallAsync(selected, progress);

            if (report.Error is not null && report.Succeeded == 0)
            {
                WuStatus = report.Error;
                _snackbar.Show("Driver install failed", report.Error,
                    Wpf.Ui.Controls.ControlAppearance.Danger,
                    new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(10));
                return;
            }

            WuStatus = $"{report.Succeeded} driver(s) installed, {report.Failed} failed."
                + (report.RebootRequired ? " Reboot to finish." : "");
            _snackbar.Show(
                "Drivers installed",
                WuStatus,
                report.Failed == 0
                    ? Wpf.Ui.Controls.ControlAppearance.Success
                    : Wpf.Ui.Controls.ControlAppearance.Caution,
                new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(8));

            FoundDriverUpdates.Clear();
            HasSearchedWu = false;
            OnPropertyChanged(nameof(HasFoundUpdates));
            await ScanAsync(); // refresh the problem list
        }
        finally
        {
            IsWuBusy = false;
        }
    }

    [RelayCommand]
    private void OpenCatalog(PnpDevice? device)
    {
        string? hardwareId = device?.HardwareIds.FirstOrDefault();
        if (hardwareId is null)
        {
            return;
        }

        try
        {
            string url = "https://www.catalog.update.microsoft.com/Search.aspx?q=" + Uri.EscapeDataString(hardwareId);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open catalog");
        }
    }

    [RelayCommand]
    private async Task InstallInfFromFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder containing an extracted driver (.inf)",
            InitialDirectory = DownloadService.DriversFolder,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsWuBusy = true;
            WuStatus = "Installing driver(s) from folder via pnputil…";
            var result = await Services.System.ProcessRunner.RunAsync(
                "pnputil", $"/add-driver \"{dialog.FolderName}\\*.inf\" /subdirs /install");

            WuStatus = result.Success
                ? "Driver package(s) installed. Rescanning…"
                : $"pnputil reported a problem (exit {result.ExitCode}) — see logs.";
            Log.Information("pnputil output: {Out}", result.Combined);
            await ScanAsync();
        }
        finally
        {
            IsWuBusy = false;
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
