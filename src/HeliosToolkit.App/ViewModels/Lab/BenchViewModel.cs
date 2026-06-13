using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeliosToolkit.App.Services.Lab;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Lab;
using HeliosToolkit.Core.Tweaks;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace HeliosToolkit.App.ViewModels.Lab;

public sealed record ProcessChoice(string ProcessName, string Title)
{
    public override string ToString() => Title;
}

public partial class BenchViewModel : ObservableObject
{
    private readonly PresentMonService _presentMon;
    private readonly BenchRunStore _store;
    private readonly TweakEngine _tweaks;
    private readonly ISnackbarService _snackbar;

    public BenchViewModel(
        PresentMonService presentMon, BenchRunStore store, TweakEngine tweaks, ISnackbarService snackbar)
    {
        _presentMon = presentMon;
        _store = store;
        _tweaks = tweaks;
        _snackbar = snackbar;
        RefreshProcesses();
        ReloadRuns();
    }

    public ObservableCollection<ProcessChoice> Processes { get; } = new();
    public ObservableCollection<BenchRun> Runs { get; } = new();
    public int[] Durations { get; } = { 30, 60, 90, 120 };

    [ObservableProperty]
    private ProcessChoice? _selectedProcess;

    [ObservableProperty]
    private int _selectedDuration = 60;

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private string _status =
        "Pick a running game, label the run (e.g. 'before tweaks'), capture 60 s, then compare two runs to PROVE a tweak helped.";

    [ObservableProperty]
    private BenchRun? _runA;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    private BenchRun? _runB;

    [ObservableProperty]
    private string _comparisonText = "";

    [ObservableProperty]
    private IReadOnlyList<Point>? _chartA;

    [ObservableProperty]
    private IReadOnlyList<Point>? _chartB;

    public bool HasComparison => RunA is not null && RunB is not null;

    [RelayCommand]
    private void RefreshProcesses()
    {
        Processes.Clear();
        foreach ((string name, string title) in PresentMonService.RunningWindowedProcesses())
        {
            Processes.Add(new ProcessChoice(name, title));
        }
    }

    [RelayCommand]
    private async Task CaptureAsync()
    {
        if (IsCapturing || SelectedProcess is null)
        {
            return;
        }

        try
        {
            IsCapturing = true;
            if (!_presentMon.IsInstalled)
            {
                Status = "Downloading PresentMon (first time only)…";
                await _presentMon.EnsureInstalledAsync(new Progress<double>(p => Status = $"Downloading PresentMon… {p:P0}"));
            }

            Status = $"Capturing {SelectedDuration}s of {SelectedProcess.ProcessName}… play normally.";
            (BenchStats stats, IReadOnlyList<double> _, string csvPath) =
                await _presentMon.CaptureAsync(SelectedProcess.ProcessName, SelectedDuration);

            IReadOnlyDictionary<string, TweakState> tweakStates = await _tweaks.DetectAllAsync();
            var run = new BenchRun
            {
                Label = string.IsNullOrWhiteSpace(Label) ? $"{SelectedProcess.ProcessName} {DateTime.Now:HH:mm}" : Label.Trim(),
                Game = SelectedProcess.ProcessName,
                Stats = stats,
                CsvPath = csvPath,
                AppliedTweaks = tweakStates.ToDictionary(kv => kv.Key, kv => kv.Value == TweakState.Applied),
            };
            _store.Save(run);
            ReloadRuns();

            Status = $"Done: {stats.AvgFps:0} fps avg · {stats.OnePercentLowFps:0} fps 1% low · " +
                     $"{stats.PointOnePercentLowFps:0} fps 0.1% low ({stats.FrameCount} frames).";
            Label = "";
        }
        catch (Exception e)
        {
            Log.Error(e, "Bench capture failed");
            Status = $"Capture failed: {e.Message}";
            _snackbar.Show("Capture failed", e.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(8));
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand]
    private void Compare()
    {
        if (RunA is null || RunB is null)
        {
            return;
        }

        string Delta(double a, double b) => FrameStats.FormatDelta(a, b);
        var lines = new List<string>
        {
            $"Avg FPS:     {RunA.Stats.AvgFps:0.0}  →  {RunB.Stats.AvgFps:0.0}   ({Delta(RunA.Stats.AvgFps, RunB.Stats.AvgFps)})",
            $"1% low:      {RunA.Stats.OnePercentLowFps:0.0}  →  {RunB.Stats.OnePercentLowFps:0.0}   ({Delta(RunA.Stats.OnePercentLowFps, RunB.Stats.OnePercentLowFps)})",
            $"0.1% low:    {RunA.Stats.PointOnePercentLowFps:0.0}  →  {RunB.Stats.PointOnePercentLowFps:0.0}   ({Delta(RunA.Stats.PointOnePercentLowFps, RunB.Stats.PointOnePercentLowFps)})",
            $"Max frametime: {RunA.Stats.MaxFrameTimeMs:0.0} ms  →  {RunB.Stats.MaxFrameTimeMs:0.0} ms",
        };

        IReadOnlyList<string> diff = BenchRunStore.DifferingTweaks(RunA, RunB);
        lines.Add(diff.Count == 0
            ? "\nNo tweak differences recorded between these runs — the change came from elsewhere."
            : "\nTweaks that differ between A and B:\n  • " + string.Join("\n  • ", diff));

        ComparisonText = string.Join('\n', lines);
        ChartA = LoadFrameSeries(RunA);
        ChartB = LoadFrameSeries(RunB);
    }

    private static IReadOnlyList<Point>? LoadFrameSeries(BenchRun run)
    {
        try
        {
            if (!File.Exists(run.CsvPath))
            {
                return null;
            }

            List<double> times = FrameStats.ParseFrameTimesCsv(File.ReadAllText(run.CsvPath));
            return times.Select((ms, i) => new Point(i, ms)).ToList();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void ReloadRuns()
    {
        Runs.Clear();
        foreach (BenchRun run in _store.LoadAll())
        {
            Runs.Add(run);
        }
    }
}
