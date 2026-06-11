using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Wpf.Ui;

namespace HeliosToolkit.App.ViewModels;

/// <summary>
/// Shared logic for both tweak pages: loads the tweaks for one page, detects their
/// state in parallel, and groups them by category. The NVIDIA and Windows pages are
/// just this with a different <see cref="TweakPage"/>.
/// </summary>
public abstract partial class TweakPageViewModel : ObservableObject
{
    private readonly TweakCatalog _catalog;
    private readonly TweakEngine _engine;
    private readonly IContentDialogService _dialogs;
    private readonly ISnackbarService _snackbar;
    private readonly TweakPage _page;

    protected TweakPageViewModel(
        TweakPage page,
        TweakCatalog catalog,
        TweakEngine engine,
        IContentDialogService dialogs,
        ISnackbarService snackbar)
    {
        _page = page;
        _catalog = catalog;
        _engine = engine;
        _dialogs = dialogs;
        _snackbar = snackbar;

        GroupedTweaks = CollectionViewSource.GetDefaultView(Tweaks);
        GroupedTweaks.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakItemViewModel.Category)));

        _ = LoadAsync();
    }

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new();

    public ICollectionView GroupedTweaks { get; }

    [ObservableProperty]
    private bool _isLoading = true;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            if (Tweaks.Count == 0)
            {
                foreach (ITweak tweak in _catalog.ForPage(_page))
                {
                    Tweaks.Add(new TweakItemViewModel(tweak, _engine, _dialogs, _snackbar));
                }
            }

            await Task.WhenAll(Tweaks.Select(t => t.RefreshAsync()));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
