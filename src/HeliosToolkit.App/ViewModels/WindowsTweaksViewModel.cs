using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Wpf.Ui;

namespace HeliosToolkit.App.ViewModels;

public sealed class WindowsTweaksViewModel : TweakPageViewModel
{
    public WindowsTweaksViewModel(
        TweakCatalog catalog, TweakEngine engine, IContentDialogService dialogs, ISnackbarService snackbar)
        : base(TweakPage.Windows, catalog, engine, dialogs, snackbar)
    {
    }
}
