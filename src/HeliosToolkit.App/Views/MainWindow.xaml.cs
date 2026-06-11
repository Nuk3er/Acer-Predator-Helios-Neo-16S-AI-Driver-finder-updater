using HeliosToolkit.App.Services;
using HeliosToolkit.App.ViewModels;
using HeliosToolkit.App.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace HeliosToolkit.App.Views;

public partial class MainWindow
{
    private readonly OnboardingService _onboarding;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationViewPageProvider pageProvider,
        INavigationService navigationService,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        OnboardingService onboarding)
    {
        ViewModel = viewModel;
        _onboarding = onboarding;
        DataContext = this;
        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialog);
        RootNavigation.SetPageProviderService(pageProvider);
        navigationService.SetNavigationControl(RootNavigation);

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(DashboardPage));
        await _onboarding.ShowIfFirstRunAsync();
    }
}
