using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Lab;
using Serilog;

namespace HeliosToolkit.App.ViewModels.Lab;

public partial class DpcMonitorViewModel : ObservableObject, IDisposable
{
    private readonly DpcMonitorService _monitor;
    private readonly DispatcherTimer _uiTimer;
    private CancellationTokenSource? _autoStopCts;

    public DpcMonitorViewModel(DpcMonitorService monitor)
    {
        _monitor = monitor;
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiTimer.Tick += (_, _) => RefreshRows();
    }

    public ObservableCollection<DpcDriverRow> Rows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopLabel))]
    private bool _isRunning;

    public string StartStopLabel => IsRunning ? "Stop" : "Start monitoring";

    [ObservableProperty]
    private string _statusText =
        "Records how long each driver hogs the CPU servicing interrupts — the real cause of micro-stutter. " +
        "Start it, then go play (or just use the PC) for 30–60 seconds. Needs no game; it watches the whole system.";

    [RelayCommand]
    private async Task StartStopAsync()
    {
        if (IsRunning)
        {
            StopInternal();
            return;
        }

        try
        {
            _monitor.Start();
            IsRunning = true;
            StatusText = "Monitoring… use the PC normally for 30–60 s, then Stop. Worst offenders rise to the top.";
            _uiTimer.Start();

            // Auto-stop after 60 s so a forgotten session never lingers.
            _autoStopCts = new CancellationTokenSource();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), _autoStopCts.Token);
                if (IsRunning)
                {
                    StopInternal();
                    StatusText = "Auto-stopped after 60 s. " + Verdict();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "DPC monitor failed to start");
            IsRunning = false;
            StatusText = $"Could not start the trace: {e.Message}. (Needs administrator — which the app already has.)";
        }
    }

    private void StopInternal()
    {
        _autoStopCts?.Cancel();
        _uiTimer.Stop();
        _monitor.Stop();
        IsRunning = false;
        RefreshRows();
        if (StatusText.StartsWith("Monitoring", StringComparison.Ordinal))
        {
            StatusText = "Stopped. " + Verdict();
        }
    }

    private void RefreshRows()
    {
        var rows = _monitor.SnapshotRows();
        Rows.Clear();
        foreach (DpcDriverRow row in rows)
        {
            Rows.Add(row);
        }
    }

    private string Verdict()
    {
        if (Rows.Count == 0)
        {
            return "No DPC/ISR activity captured.";
        }

        DpcDriverRow worst = Rows[0];
        return $"Worst: {worst.Module} peaked at {worst.MaxUs:0} µs. {worst.Advice}";
    }

    public void Dispose()
    {
        _autoStopCts?.Cancel();
        _uiTimer.Stop();
        _monitor.Stop();
    }
}
