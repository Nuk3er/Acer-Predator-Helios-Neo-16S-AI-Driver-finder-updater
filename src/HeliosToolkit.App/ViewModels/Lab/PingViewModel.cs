using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Lab;
using HeliosToolkit.Core.Lab;
using Serilog;

namespace HeliosToolkit.App.ViewModels.Lab;

public partial class PingViewModel(PingTestService pingTest) : ObservableObject
{
    [ObservableProperty]
    private string _host = "1.1.1.1";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _resultText = "20 pings, 150 ms apart — jitter (spike-to-spike variation) matters more than the average.";

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning || string.IsNullOrWhiteSpace(Host))
        {
            return;
        }

        try
        {
            IsRunning = true;
            ResultText = $"Pinging {Host}…";
            PingResultStats stats = await pingTest.RunAsync(Host.Trim());
            ResultText = stats.Sent == stats.Lost
                ? $"All {stats.Sent} pings to {Host} were lost — host unreachable or ICMP blocked."
                : $"{Host}: avg {stats.AvgMs:0.0} ms · jitter {stats.JitterMs:0.0} ms · max {stats.MaxMs:0.0} ms" +
                  (stats.Lost > 0 ? $" · {stats.LossPercent:0}% loss(!)" : " · no loss");
        }
        catch (Exception e)
        {
            Log.Warning(e, "Ping test failed");
            ResultText = $"Ping failed: {e.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
