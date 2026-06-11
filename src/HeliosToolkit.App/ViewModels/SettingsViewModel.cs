using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Update;
using Serilog;

namespace HeliosToolkit.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string RepoUrl =
        "https://github.com/nuk3er/acer-predator-helios-neo-16s-ai-driver-finder-updater";

    private readonly AppUpdateService _updates;
    private readonly ManifestService _manifest;

    public SettingsViewModel(AppUpdateService updates, ManifestService manifest)
    {
        _updates = updates;
        _manifest = manifest;
    }

    public string VersionText => $"Version {_updates.CurrentVersion}";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _updateStatus = "";

    [ObservableProperty]
    private string? _updateUrl;

    [ObservableProperty]
    private bool _hasUpdateLink;

    [ObservableProperty]
    private bool _canInstallUpdate;

    private string? _updateExeUrl;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            IsChecking = true;
            UpdateStatus = "Checking…";
            HasUpdateLink = false;
            CanInstallUpdate = false;
            AppUpdateCheck check = await _updates.CheckAsync();

            if (check.UpdateAvailable)
            {
                UpdateStatus = $"Update available: {check.LatestTag} (you have {check.CurrentVersion}).";
                UpdateUrl = check.ReleaseUrl;
                HasUpdateLink = check.ReleaseUrl is not null;
                _updateExeUrl = check.ExeDownloadUrl;
                CanInstallUpdate = _updateExeUrl is not null;
            }
            else if (check.NoReleasesYet)
            {
                UpdateStatus = "No releases have been published yet — you're running a development build.";
            }
            else if (check.LatestTag is not null)
            {
                UpdateStatus = $"You're up to date ({check.CurrentVersion}).";
            }
            else
            {
                UpdateStatus = "Could not reach GitHub. Check your connection and try again.";
            }
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_updateExeUrl is null)
        {
            return;
        }

        try
        {
            IsChecking = true;
            var progress = new Progress<double>(p => UpdateStatus = $"Downloading update… {p:P0}");
            await _updates.DownloadAndInstallAsync(_updateExeUrl, progress);
            // The app exits inside DownloadAndInstallAsync; this line only runs on failure paths.
        }
        catch (Exception e)
        {
            Log.Error(e, "In-app update failed");
            UpdateStatus = $"Update failed: {e.Message} — grab the EXE from the Releases page instead.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task RefreshManifestAsync()
    {
        try
        {
            IsChecking = true;
            await _manifest.GetManifestAsync(forceRefresh: true);
            UpdateStatus = $"Driver manifest refreshed ({_manifest.LoadedFrom}).";
        }
        catch (Exception e)
        {
            Log.Warning(e, "Manifest refresh failed");
            UpdateStatus = "Manifest refresh failed — see logs.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void OpenUpdateUrl() => Open(UpdateUrl);

    [RelayCommand]
    private void OpenLogs() => Open(AppPaths.Logs);

    [RelayCommand]
    private void OpenRepo() => Open(RepoUrl);

    private static void Open(string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open {Target}", target);
        }
    }
}
