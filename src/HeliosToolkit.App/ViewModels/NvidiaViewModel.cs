using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Wpf.Ui;

namespace HeliosToolkit.App.ViewModels;

public sealed class NvidiaViewModel : TweakPageViewModel
{
    public NvidiaViewModel(
        TweakCatalog catalog, TweakEngine engine, IContentDialogService dialogs, ISnackbarService snackbar)
        : base(TweakPage.Nvidia, catalog, engine, dialogs, snackbar)
    {
    }
}
