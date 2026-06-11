using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.Tweaks;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace HeliosToolkit.App.ViewModels;

public partial class BackupViewModel : ObservableObject
{
    private readonly TweakEngine _engine;
    private readonly BackupStore _backup;
    private readonly RestorePointService _restorePoints;
    private readonly ProfileService _profiles;
    private readonly IContentDialogService _dialogs;
    private readonly ISnackbarService _snackbar;

    public BackupViewModel(
        TweakEngine engine,
        BackupStore backup,
        RestorePointService restorePoints,
        ProfileService profiles,
        IContentDialogService dialogs,
        ISnackbarService snackbar)
    {
        _engine = engine;
        _backup = backup;
        _restorePoints = restorePoints;
        _profiles = profiles;
        _dialogs = dialogs;
        _snackbar = snackbar;
        RefreshBackupCount();
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackedUpSummary))]
    [NotifyPropertyChangedFor(nameof(HasBackups))]
    private int _backedUpCount;

    public bool HasBackups => BackedUpCount > 0;

    public string BackedUpSummary => BackedUpCount == 0
        ? "No tweaks have been applied yet, so there is nothing to revert."
        : $"{BackedUpCount} tweak(s) have a saved original value and can be reverted.";

    public string BackupFolder => AppPaths.Backup;

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        try
        {
            IsBusy = true;
            RestorePointResult result = await _restorePoints.CreateAsync("Helios Neo Toolkit — manual");
            (string title, string message, ControlAppearance look) = result switch
            {
                RestorePointResult.Created => ("Restore point created", "You can roll Windows back to this point from System Restore.", ControlAppearance.Success),
                RestorePointResult.Disabled => ("System Restore is off", "Turn on System Protection for your C: drive in Windows to use restore points.", ControlAppearance.Caution),
                _ => ("Could not create restore point", "See the log for details. Your value-level backups still protect every tweak.", ControlAppearance.Danger),
            };
            _snackbar.Show(title, message, look, new SymbolIcon(SymbolRegular.ShieldCheckmark24), TimeSpan.FromSeconds(7));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RevertEverythingAsync()
    {
        var confirm = await _dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "Revert every tweak?",
            Content = "This restores the original value of every setting Helios Toolkit changed, " +
                      "putting your system back the way it was before any tweak. Some changes need a reboot to fully apply.",
            PrimaryButtonText = "Revert everything",
            CloseButtonText = "Cancel",
        });

        if (confirm != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            IsBusy = true;
            int count = await _engine.RevertAllAsync();
            RefreshBackupCount();
            _snackbar.Show("Reverted", $"{count} tweak(s) restored to their original values.",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.ArrowUndo24), TimeSpan.FromSeconds(6));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportProfileAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export tweak profile",
            Filter = "Helios profile (*.json)|*.json",
            FileName = "helios-profile.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _profiles.ExportAsync(dialog.FileName, "Helios profile");
            _snackbar.Show("Profile exported", dialog.FileName,
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.Save24), TimeSpan.FromSeconds(5));
        }
        catch (Exception e)
        {
            Log.Error(e, "Profile export failed");
            _snackbar.Show("Export failed", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportProfileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import tweak profile",
            Filter = "Helios profile (*.json)|*.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ProfileImportReport report = await _profiles.ImportAsync(dialog.FileName);
            RefreshBackupCount();
            _snackbar.Show("Profile imported",
                $"{report.Applied} applied, {report.Reverted} reverted, {report.Skipped} skipped, {report.Failed} failed.",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(7));
        }
        catch (Exception e)
        {
            Log.Error(e, "Profile import failed");
            _snackbar.Show("Import failed", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        try
        {
            global::System.IO.Directory.CreateDirectory(AppPaths.Backup);
            global::System.Diagnostics.Process.Start(
                new global::System.Diagnostics.ProcessStartInfo(AppPaths.Backup) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open backup folder");
        }
    }

    private void RefreshBackupCount() => BackedUpCount = _backup.BackedUpTweakIds().Count;
}
