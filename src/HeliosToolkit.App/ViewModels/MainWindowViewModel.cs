using CommunityToolkit.Mvvm.ComponentModel;

namespace HeliosToolkit.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationTitle = "Helios Neo Toolkit";
}
