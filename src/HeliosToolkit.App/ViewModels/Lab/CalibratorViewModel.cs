using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Lab;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.Core.Lab;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeliosToolkit.App.ViewModels.Lab;

public partial class CalibratorViewModel : ObservableObject
{
    private readonly TimerCalibrationService _calibration;
    private readonly TimerResolutionService _timer;
    private readonly ISnackbarService _snackbar;
    private CalibrationOutcome? _outcome;
    private CancellationTokenSource? _cts;

    public CalibratorViewModel(
        TimerCalibrationService calibration, TimerResolutionService timer, ISnackbarService snackbar)
    {
        _calibration = calibration;
        _timer = timer;
        _snackbar = snackbar;
        RefreshCurrentText();
    }

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _progressText =
        "Sweeps 0.5000 → 0.5100 ms in 0.0002 ms steps and measures how late Sleep(1) really wakes at each " +
        "value (~10 s, plugged in, system idle). The lowest, most consistent wake-up wins.";

    [ObservableProperty]
    private string _currentText = "";

    [ObservableProperty]
    private string _recommendationText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private bool _hasOutcome;

    public bool HasResult => HasOutcome;

    [ObservableProperty]
    private IReadOnlyList<Point>? _chartPoints;

    [ObservableProperty]
    private Point? _chartBest;

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
            return;
        }

        if (!PowerStatus.IsOnAcPower())
        {
            _snackbar.Show("Plug in first",
                "Calibration on battery measures the power saver, not your machine. Connect AC power and re-run.",
                ControlAppearance.Caution, new SymbolIcon(SymbolRegular.BatteryWarning24), TimeSpan.FromSeconds(7));
            return;
        }

        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            var progress = new Progress<CalibrationProgress>(p =>
                ProgressText = $"Measuring… {p.CompletedSteps}/{p.TotalSteps} " +
                               (p.Latest is null ? "" :
                                   $"— {p.Latest.RequestedMs:0.0000} ms → avg {p.Latest.AvgSleepMs:0.0000} ms (±{p.Latest.StdevMs:0.0000})"));

            CalibrationOutcome outcome = await _calibration.RunSweepAsync(progress, _cts.Token);
            _outcome = outcome;
            HasOutcome = true;

            ChartPoints = outcome.Results
                .OrderBy(r => r.RequestedMs)
                .Select(r => new Point(r.RequestedMs, r.Score))
                .ToList();
            ChartBest = new Point(outcome.Best.RequestedMs, outcome.Best.Score);

            ProgressText = $"Done. Best: {outcome.Best.RequestedMs:0.0000} ms — Sleep(1) wakes after " +
                           $"{outcome.Best.AvgSleepMs:0.0000} ms on average (±{outcome.Best.StdevMs:0.0000} ms jitter).";
            RecommendationText = outcome.RecommendKeepDefault
                ? "The measured win over plain 0.5000 ms is inside the noise — keeping the default is the honest call. " +
                  "You can still apply the measured value if you want it."
                : $"Recommendation: apply {outcome.Best.RequestedMs:0.0000} ms — it beat 0.5000 ms by more than its own jitter.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Calibration cancelled.";
        }
        catch (Exception e)
        {
            Log.Error(e, "Calibration failed");
            ProgressText = $"Calibration failed: {e.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts = null;
        }
    }

    [RelayCommand]
    private void ApplyBest()
    {
        if (_outcome is null)
        {
            return;
        }

        _timer.SaveCalibration(_outcome.Best.RequestedHundredNs);
        RefreshCurrentText();
        _snackbar.Show("Calibration applied",
            $"The timer hold now requests {_outcome.Best.RequestedMs:0.0000} ms everywhere (tweak, tray, logon task).",
            ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(6));
    }

    [RelayCommand]
    private void UseDefault()
    {
        _timer.SaveCalibration(TimerCalibrationMath.DefaultRequestHundredNs);
        RefreshCurrentText();
        _snackbar.Show("Default restored", "Timer hold requests plain 0.5000 ms again.",
            ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle24), TimeSpan.FromSeconds(5));
    }

    private void RefreshCurrentText()
    {
        CurrentText = $"Current hold target: {_timer.TargetMs:0.0000} ms" +
                      (_timer.IsHolding ? $" (holding now, granted {_timer.GrantedHundredNs / 10_000.0:0.0000} ms)" : "");
    }
}
