using HeliosToolkit.App.ViewModels;

namespace HeliosToolkit.App.Views.Pages;

public partial class WindowsTweaksPage
{
    public WindowsTweaksViewModel ViewModel { get; }

    public WindowsTweaksPage(WindowsTweaksViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
