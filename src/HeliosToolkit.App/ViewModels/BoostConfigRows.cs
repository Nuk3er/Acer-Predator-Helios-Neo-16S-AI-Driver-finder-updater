using CommunityToolkit.Mvvm.ComponentModel;

namespace HeliosToolkit.App.ViewModels;

public sealed record ProcessPick(string ProcessName, string Title)
{
    public override string ToString() => Title;
}

/// <summary>A watched game row in Settings; PinToPCores is editable inline.</summary>
public partial class WatchedGameRow : ObservableObject
{
    public WatchedGameRow(string exeName, bool pinToPCores)
    {
        ExeName = exeName;
        _pinToPCores = pinToPCores;
    }

    public string ExeName { get; }

    [ObservableProperty]
    private bool _pinToPCores;
}
