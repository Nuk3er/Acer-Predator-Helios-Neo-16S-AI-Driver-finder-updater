namespace HeliosToolkit.App.ViewModels.Lab;

/// <summary>Aggregates the Lab page's card view models.</summary>
public sealed class LabViewModel(
    CalibratorViewModel calibrator, DpcMonitorViewModel dpcMonitor, PingViewModel ping)
{
    public CalibratorViewModel Calibrator { get; } = calibrator;

    public DpcMonitorViewModel DpcMonitor { get; } = dpcMonitor;

    public PingViewModel Ping { get; } = ping;
}
