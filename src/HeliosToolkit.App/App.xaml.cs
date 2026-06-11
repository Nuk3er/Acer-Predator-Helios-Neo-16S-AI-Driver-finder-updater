using System.IO;
using System.Windows;
using System.Windows.Threading;
using HeliosToolkit.App.Services;
using HeliosToolkit.App.ViewModels;
using HeliosToolkit.App.Views;
using HeliosToolkit.App.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;

namespace HeliosToolkit.App;

public partial class App
{
    private static readonly IHost AppHost = Host
        .CreateDefaultBuilder()
        .UseSerilog((_, configuration) => configuration
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "helios-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
        .ConfigureServices((_, services) =>
        {
            // Shell
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<INavigationViewPageProvider, NavigationPageProvider>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();

            // Pages + view models (singletons: NavigationView keeps page state alive)
            services.AddSingleton<DashboardPage>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<DevicesPage>();
            services.AddSingleton<DevicesViewModel>();
            services.AddSingleton<NvidiaPage>();
            services.AddSingleton<NvidiaViewModel>();
            services.AddSingleton<WindowsTweaksPage>();
            services.AddSingleton<WindowsTweaksViewModel>();
            services.AddSingleton<BackupPage>();
            services.AddSingleton<BackupViewModel>();
            services.AddSingleton<SettingsPage>();
            services.AddSingleton<SettingsViewModel>();
        })
        .Build();

    public static T GetRequiredService<T>() where T : class => AppHost.Services.GetRequiredService<T>();

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        AppPaths.EnsureCreated();
        await AppHost.StartAsync();

        Log.Information("Helios Neo Toolkit starting (version {Version})",
            typeof(App).Assembly.GetName().Version);

        ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0xD1),
            ApplicationTheme.Dark);

        AppHost.Services.GetRequiredService<MainWindow>().Show();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await AppHost.StopAsync();
        AppHost.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception");
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}\n\nDetails were written to the log in {AppPaths.Logs}.",
            "Helios Neo Toolkit",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
