using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.Tweaks;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace HeliosToolkit.App.Services;

/// <summary>Shows a one-time welcome explaining the safety model on first run.</summary>
public sealed class OnboardingService(
    IContentDialogService dialogs, RestorePointService restorePoints)
{
    private const string StateKey = @"SOFTWARE\HeliosToolkit";
    private const string SeenValue = "OnboardingSeen";

    public async Task ShowIfFirstRunAsync()
    {
        if (HasSeen())
        {
            return;
        }

        MarkSeen();

        var result = await dialogs.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "Welcome to Helios Neo Toolkit",
            Content =
                "This tool is built for the Predator Helios Neo 16S AI (PHN16S-71).\n\n" +
                "• Nothing changes until you flip a switch — driver installs are always interactive.\n" +
                "• Before the first tweak, the app saves the original value of everything it touches and " +
                "offers a System Restore point.\n" +
                "• Every tweak is labeled Safe / Situational / Risky and can be reverted, individually or all at once " +
                "from the Backup page.\n" +
                "• Read the descriptions: some famous 'gaming tweaks' do nothing on modern Windows 11, and this tool " +
                "tells you which.\n" +
                "• New in 1.0 — the Lab page: calibrate your timer resolution, hunt stutter-causing drivers, " +
                "benchmark games and A/B your tweaks. The Dashboard's Game Boost button ties it all together.\n\n" +
                "Want to create a System Restore point right now as a baseline?",
            PrimaryButtonText = "Create restore point",
            CloseButtonText = "Maybe later",
        });

        if (result == ContentDialogResult.Primary)
        {
            RestorePointResult rp = await restorePoints.CreateAsync("Helios Neo Toolkit — baseline");
            Log.Information("Onboarding restore point: {Result}", rp);
        }
    }

    private static bool HasSeen() =>
        RegistryHelper.ReadValue("HKCU", StateKey, SeenValue) is int i && i == 1;

    private static void MarkSeen()
    {
        try
        {
            RegistryHelper.WriteValue("HKCU", StateKey, SeenValue, 1, RegistryValueKind.DWord);
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not persist onboarding flag");
        }
    }
}
