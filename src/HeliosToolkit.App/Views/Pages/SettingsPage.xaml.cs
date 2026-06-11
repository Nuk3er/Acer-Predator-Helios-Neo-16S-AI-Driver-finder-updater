using HeliosToolkit.App.ViewModels;

namespace HeliosToolkit.App.Views.Pages;

public partial class SettingsPage
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
