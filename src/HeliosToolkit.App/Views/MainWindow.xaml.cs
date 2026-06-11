using HeliosToolkit.App.ViewModels;
using HeliosToolkit.App.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace HeliosToolkit.App.Views;

public partial class MainWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationViewPageProvider pageProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialog);
        RootNavigation.SetPageProviderService(pageProvider);

        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }
}
