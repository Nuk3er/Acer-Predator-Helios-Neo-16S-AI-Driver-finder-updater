using HeliosToolkit.App.ViewModels;

namespace HeliosToolkit.App.Views.Pages;

public partial class NvidiaPage
{
    public NvidiaViewModel ViewModel { get; }

    public NvidiaPage(NvidiaViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
