using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeliosToolkit.App.ViewModels;

public sealed partial class NvidiaViewModel : TweakPageViewModel
{
    private readonly NvidiaPackageDebloater _debloater;
    private readonly ISnackbarService _snackbar;
    private string? _extractedFolder;

    public NvidiaViewModel(
        TweakCatalog catalog,
        TweakEngine engine,
        IContentDialogService dialogs,
        ISnackbarService snackbar,
        NvidiaPackageDebloater debloater)
        : base(TweakPage.Nvidia, catalog, engine, dialogs, snackbar)
    {
        _debloater = debloater;
        _snackbar = snackbar;
    }

    public ObservableCollection<NvidiaComponentViewModel> Components { get; } = new();

    [ObservableProperty]
    private bool _isDebloatBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComponents))]
    private bool _hasExtracted;

    [ObservableProperty]
    private string _debloatStatus =
        "Pick a downloaded NVIDIA driver .exe to strip GeForce Experience, the NVIDIA App, telemetry and other extras before installing.";

    public bool HasComponents => HasExtracted && Components.Count > 0;

    [RelayCommand]
    private async Task PickAndExtractAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select the downloaded NVIDIA driver package",
            Filter = "NVIDIA driver package (*.exe)|*.exe",
            InitialDirectory = DownloadService.DriversFolder,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsDebloatBusy = true;
            HasExtracted = false;
            Components.Clear();
            DebloatStatus = "Extracting package… this can take a minute for a full driver.";

            (string folder, IReadOnlyList<NvidiaComponent> components) =
                await _debloater.ExtractAsync(dialog.FileName);

            _extractedFolder = folder;
            foreach (NvidiaComponent component in components)
            {
                Components.Add(new NvidiaComponentViewModel(component));
            }

            HasExtracted = true;
            OnPropertyChanged(nameof(HasComponents));
            DebloatStatus = $"Found {components.Count} components. Untick what you don't want, then build the trimmed installer.";
        }
        catch (Exception e)
        {
            Log.Error(e, "NVIDIA package extraction failed");
            DebloatStatus = "Could not extract this package. Make sure it's an official NVIDIA driver .exe. " +
                            "You can also use NVCleanstall instead.";
            _snackbar.Show("Extraction failed", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsDebloatBusy = false;
        }
    }

    [RelayCommand]
    private async Task BuildAndLaunchAsync()
    {
        if (_extractedFolder is null)
        {
            return;
        }

        try
        {
            IsDebloatBusy = true;
            var keep = Components.Where(c => c.IsKept).Select(c => c.Component).ToList();
            DebloatResult result = await _debloater.ApplyAsync(_extractedFolder, keep);

            DebloatStatus = $"Trimmed installer ready: kept {result.KeptComponents}, removed {result.RemovedComponents}. " +
                            "Launching setup — choose 'Custom (Advanced)' and 'Perform a clean installation' for best results.";

            Process.Start(new ProcessStartInfo(result.SetupExePath) { UseShellExecute = true });
            _snackbar.Show("Launching NVIDIA setup", "Walk through the installer to finish.",
                ControlAppearance.Success, new SymbolIcon(SymbolRegular.Play24), TimeSpan.FromSeconds(6));
        }
        catch (Exception e)
        {
            Log.Error(e, "Debloat apply/launch failed");
            _snackbar.Show("Could not build installer", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsDebloatBusy = false;
        }
    }
}
