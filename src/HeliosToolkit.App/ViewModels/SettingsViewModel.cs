using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Boost;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Lab;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.App.Services.Update;
using Serilog;

namespace HeliosToolkit.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string RepoUrl =
        "https://github.com/nuk3er/acer-predator-helios-neo-16s-ai-driver-finder-updater";

    private readonly AppUpdateService _updates;
    private readonly ManifestService _manifest;
    private readonly LogonTaskService _logonTask;
    private readonly BoostConfigStore _boostStore;

    public SettingsViewModel(
        AppUpdateService updates, ManifestService manifest,
        LogonTaskService logonTask, BoostConfigStore boostStore)
    {
        _updates = updates;
        _manifest = manifest;
        _logonTask = logonTask;
        _boostStore = boostStore;
        _ = RefreshLogonTaskAsync();
        LoadBoostConfig();
    }

    public string VersionText => $"Version {_updates.CurrentVersion}";

    // ───────────── Logon task ─────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogonTaskLabel))]
    private bool _logonTaskInstalled;

    public string LogonTaskLabel => LogonTaskInstalled
        ? "Helios starts minimized at logon and holds your calibrated timer."
        : "Off — the timer hold only runs while you have Helios open.";

    [RelayCommand]
    private async Task ToggleLogonTaskAsync()
    {
        if (LogonTaskInstalled)
        {
            await _logonTask.UninstallAsync();
        }
        else
        {
            await _logonTask.InstallAsync();
        }

        await RefreshLogonTaskAsync();
    }

    private async Task RefreshLogonTaskAsync() => LogonTaskInstalled = await _logonTask.IsInstalledAsync();

    // ───────────── Game Boost config ─────────────

    public ObservableCollection<string> KillList { get; } = new();
    public ObservableCollection<WatchedGameRow> WatchedGames { get; } = new();
    public ObservableCollection<ProcessPick> RunningProcesses { get; } = new();

    [ObservableProperty]
    private ProcessPick? _selectedProcess;

    [RelayCommand]
    private void RefreshProcessList()
    {
        RunningProcesses.Clear();
        foreach ((string name, string title) in PresentMonService.RunningWindowedProcesses())
        {
            RunningProcesses.Add(new ProcessPick(name, title));
        }
    }

    [RelayCommand]
    private void AddToKillList()
    {
        string? path = SelectedProcess is null ? null : TryGetPath(SelectedProcess.ProcessName);
        if (path is not null && !KillList.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            KillList.Add(path);
            SaveBoostConfig();
        }
    }

    [RelayCommand]
    private void RemoveFromKillList(string? path)
    {
        if (path is not null && KillList.Remove(path))
        {
            SaveBoostConfig();
        }
    }

    [RelayCommand]
    private void AddWatchedGame()
    {
        if (SelectedProcess is { } pick &&
            !WatchedGames.Any(g => g.ExeName.Equals(pick.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            WatchedGames.Add(Track(new WatchedGameRow(pick.ProcessName, false)));
            SaveBoostConfig();
        }
    }

    /// <summary>Persists whenever a row's pin toggle changes.</summary>
    private WatchedGameRow Track(WatchedGameRow row)
    {
        row.PropertyChanged += (_, _) => SaveBoostConfig();
        return row;
    }

    [RelayCommand]
    private void RemoveWatchedGame(WatchedGameRow? row)
    {
        if (row is not null && WatchedGames.Remove(row))
        {
            SaveBoostConfig();
        }
    }

    public void SaveBoostConfig()
    {
        BoostConfig config = _boostStore.LoadConfig() with
        {
            KillList = KillList.ToList(),
            WatchedGames = WatchedGames.Select(g => new WatchedGame(g.ExeName, g.PinToPCores)).ToList(),
        };
        _boostStore.SaveConfig(config);
    }

    private void LoadBoostConfig()
    {
        BoostConfig config = _boostStore.LoadConfig();
        foreach (string path in config.KillList)
        {
            KillList.Add(path);
        }

        foreach (WatchedGame game in config.WatchedGames)
        {
            WatchedGames.Add(Track(new WatchedGameRow(game.ExeName, game.PinToPCores)));
        }

        RefreshProcessList();
    }

    private static string? TryGetPath(string exeName)
    {
        try
        {
            string baseName = global::System.IO.Path.GetFileNameWithoutExtension(exeName);
            foreach (Process p in Process.GetProcessesByName(baseName))
            {
                using (p)
                {
                    string? path = p.MainModule?.FileName;
                    if (path is not null)
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not resolve path for {Exe}", exeName);
        }

        return null;
    }

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
