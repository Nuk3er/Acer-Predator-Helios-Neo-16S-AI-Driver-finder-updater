using HeliosToolkit.App.ViewModels;

namespace HeliosToolkit.App.Views.Pages;

public partial class DashboardPage
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
