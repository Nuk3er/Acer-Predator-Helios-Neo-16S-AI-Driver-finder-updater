using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using HeliosToolkit.App.Services;
using HeliosToolkit.App.Services.Drivers;
using HeliosToolkit.App.Services.Hardware;
using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.App.Services.Update;
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
            services.AddSingleton<OnboardingService>();
            services.AddSingleton<INavigationViewPageProvider, NavigationPageProvider>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();

            // Hardware / system info
            services.AddSingleton<WmiQueryService>();
            services.AddSingleton<SystemInfoService>();
            services.AddSingleton<DeviceInventoryService>();

            // Drivers
            services.AddSingleton(_ =>
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    $"HeliosToolkit/{typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
                return client;
            });
            services.AddSingleton<ManifestService>();
            services.AddSingleton<NvidiaDriverApiClient>();
            services.AddSingleton<DriverStatusService>();
            services.AddSingleton<DownloadService>();
            services.AddSingleton<DriverHealthState>();
            services.AddSingleton<NvidiaPackageDebloater>();
            services.AddSingleton<WindowsUpdateDriverService>();
            services.AddSingleton<HeliosToolkit.App.Services.Nvidia.NvApiDrs>();
            services.AddSingleton<TrayService>();

            // Network / CPU topology
            services.AddSingleton<HeliosToolkit.App.Services.Network.ActiveAdapterService>();
            services.AddSingleton<HeliosToolkit.App.Services.Network.NicTweakFactory>();
            services.AddSingleton<CpuTopologyService>();
            services.AddSingleton<UltimateSchemeProvider>();

            // Game Boost
            services.AddSingleton<HeliosToolkit.App.Services.Boost.BoostConfigStore>();
            services.AddSingleton<HeliosToolkit.App.Services.Boost.BoostController>();
            services.AddSingleton<HeliosToolkit.App.Services.Boost.GameWatchService>();
            services.AddSingleton<BoostViewModel>();

            // Tweak engine & safety
            services.AddSingleton<TimerResolutionService>();
            services.AddSingleton<TweakCatalog>();
            services.AddSingleton<BackupStore>();
            services.AddSingleton<RestorePointService>();
            services.AddSingleton<TweakEngine>();
            services.AddSingleton<ProfileService>();

            // Updates & settings
            services.AddSingleton<AppUpdateService>();

            // Lab
            services.AddSingleton<LogonTaskService>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.TimerCalibrationService>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.PingTestService>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.KernelModuleMap>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.DpcMonitorService>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.PresentMonService>();
            services.AddSingleton<HeliosToolkit.App.Services.Lab.BenchRunStore>();
            services.AddSingleton<ViewModels.Lab.CalibratorViewModel>();
            services.AddSingleton<ViewModels.Lab.DpcMonitorViewModel>();
            services.AddSingleton<ViewModels.Lab.BenchViewModel>();
            services.AddSingleton<ViewModels.Lab.PingViewModel>();
            services.AddSingleton<ViewModels.Lab.LabViewModel>();
            services.AddSingleton<LabPage>();

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
        HeliosToolkit.App.Services.Lab.DpcMonitorService.CleanupStaleSession();
        await AppHost.StartAsync();

        // Roll back any Boost session a previous crash left active.
        try
        {
            if (await AppHost.Services.GetRequiredService<HeliosToolkit.App.Services.Boost.BoostController>()
                    .RecoverAsync())
            {
                Log.Information("Recovered a leftover Boost session");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Boost recovery failed");
        }

        bool trayMode = e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
        Log.Information("Helios Neo Toolkit starting (version {Version}, tray={Tray})",
            typeof(App).Assembly.GetName().Version, trayMode);

        ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0xD1),
            ApplicationTheme.Dark);

        MainWindow window = AppHost.Services.GetRequiredService<MainWindow>();
        if (trayMode)
        {
            // Logon-task mode: live in the tray and hold the calibrated timer.
            AppHost.Services.GetRequiredService<TimerResolutionService>().Start();
        }
        else
        {
            window.Show();
        }
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
