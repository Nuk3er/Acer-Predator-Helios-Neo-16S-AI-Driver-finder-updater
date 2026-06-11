using HeliosToolkit.App.ViewModels;

namespace HeliosToolkit.App.Views.Pages;

public partial class DevicesPage
{
    public DevicesViewModel ViewModel { get; }

    public DevicesPage(DevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
