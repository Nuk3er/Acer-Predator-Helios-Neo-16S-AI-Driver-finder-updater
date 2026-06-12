using HeliosToolkit.App.ViewModels.Lab;

namespace HeliosToolkit.App.Views.Pages;

public partial class LabPage
{
    public LabViewModel ViewModel { get; }

    public LabPage(LabViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
