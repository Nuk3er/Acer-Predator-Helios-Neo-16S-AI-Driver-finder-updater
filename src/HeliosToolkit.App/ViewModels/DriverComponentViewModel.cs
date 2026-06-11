using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using IOPath = System.IO.Path;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Drivers;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeliosToolkit.App.ViewModels;

/// <summary>One row in the "Driver updates" list: a manifest component with its status and actions.</summary>
public partial class DriverComponentViewModel : ObservableObject
{
    private readonly DriverStatusRow _row;
    private readonly DownloadService _downloads;
    private readonly ISnackbarService _snackbar;

    public DriverComponentViewModel(DriverStatusRow row, DownloadService downloads, ISnackbarService snackbar)
    {
        _row = row;
        _downloads = downloads;
        _snackbar = snackbar;
    }

    public string Name => _row.Component.Name;
    public string VendorLine => string.Join(" · ", new[] { _row.Component.Vendor, _row.Component.Category }
        .Where(s => !string.IsNullOrWhiteSpace(s)));
    public string StatusText => _row.StatusText;
    public string MatchedDeviceLine => _row.MatchedDeviceName is { Length: > 0 } d ? $"Detected: {d}" : "";
    public string InstallNotes => _row.Component.InstallNotes ?? "";
    public bool HasInstallNotes => InstallNotes.Length > 0;
    public bool HasMatchedDevice => MatchedDeviceLine.Length > 0;

    public bool IsUpdateAvailable => _row.State == DriverRowState.UpdateAvailable;

    public Brush StatusBrush => _row.State switch
    {
        DriverRowState.UpToDate => FindBrush("RiskSafeBrush"),
        DriverRowState.UpdateAvailable => FindBrush("PredatorAccentBrush"),
        DriverRowState.CheckFailed => FindBrush("RiskRiskyBrush"),
        _ => Brushes.Gray,
    };

    public bool HasDownloadUrl => _row.DownloadUrl is not null;
    public bool HasPage => _row.PageUrl is not null;
    public bool HasNotes => _row.ReleaseNotesUrl is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    private bool _isDownloading;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDownloaded))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton))]
    [NotifyPropertyChangedFor(nameof(DownloadedFileLine))]
    private string? _downloadedPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadedFileLine))]
    private string? _downloadedHash;

    public bool HasDownloaded => DownloadedPath is not null;

    public bool ShowDownloadButton => HasDownloadUrl && !IsDownloading && !HasDownloaded;

    public string DownloadedFileLine => DownloadedPath is null
        ? ""
        : $"{IOPath.GetFileName(DownloadedPath)}\nSHA-256: {DownloadedHash}";

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (_row.DownloadUrl is null || IsDownloading)
        {
            return;
        }

        try
        {
            IsDownloading = true;
            Progress = 0;
            var progress = new Progress<double>(p => Progress = p * 100);
            DownloadOutcome outcome = await _downloads.DownloadAsync(_row.DownloadUrl, _row.Component.Id, progress);

            DownloadedPath = outcome.FilePath;
            DownloadedHash = outcome.Sha256Hex;

            if (_row.Component.Sha256 is { Length: > 0 } expected
                && !expected.Equals(outcome.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                _snackbar.Show(
                    "Checksum mismatch!",
                    $"{Name}: the downloaded file does not match the expected SHA-256. Do not install it.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ShieldError24),
                    TimeSpan.FromSeconds(15));
                return;
            }

            _snackbar.Show(
                "Download complete",
                $"{IOPath.GetFileName(outcome.FilePath)} ({outcome.Bytes / 1024.0 / 1024.0:0.0} MB)",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(6));
        }
        catch (Exception e)
        {
            Log.Error(e, "Download failed for {Component}", _row.Component.Id);
            _snackbar.Show(
                "Download failed",
                e.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(10));
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void LaunchInstaller()
    {
        if (DownloadedPath is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(DownloadedPath) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not launch installer {Path}", DownloadedPath);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (DownloadedPath is not null)
        {
            Process.Start("explorer.exe", $"/select,\"{DownloadedPath}\"");
        }
    }

    [RelayCommand]
    private void OpenPage() => OpenUrl(_row.PageUrl);

    [RelayCommand]
    private void OpenNotes() => OpenUrl(_row.ReleaseNotesUrl);

    private static void OpenUrl(string? url)
    {
        if (url is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not open {Url}", url);
        }
    }

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}
