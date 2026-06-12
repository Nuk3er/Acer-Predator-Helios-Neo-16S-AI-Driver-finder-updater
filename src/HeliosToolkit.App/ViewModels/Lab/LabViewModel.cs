namespace HeliosToolkit.App.ViewModels.Lab;

/// <summary>Aggregates the Lab page's card view models.</summary>
public sealed class LabViewModel(CalibratorViewModel calibrator, PingViewModel ping)
{
    public CalibratorViewModel Calibrator { get; } = calibrator;

    public PingViewModel Ping { get; } = ping;
}
