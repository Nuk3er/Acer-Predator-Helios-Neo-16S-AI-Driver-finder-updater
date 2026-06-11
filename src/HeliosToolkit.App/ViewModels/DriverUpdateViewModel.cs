using CommunityToolkit.Mvvm.ComponentModel;
using HeliosToolkit.App.Services.Drivers;

namespace HeliosToolkit.App.ViewModels;

/// <summary>A checkable driver update found on Windows Update.</summary>
public partial class DriverUpdateViewModel : ObservableObject
{
    public DriverUpdateViewModel(DriverUpdateCandidate candidate)
    {
        Candidate = candidate;
        _isSelected = true;
    }

    public DriverUpdateCandidate Candidate { get; }

    public string Title => Candidate.Title;

    public bool IsForProblemDevice => Candidate.MatchedDeviceName is not null;

    public string DetailLine
    {
        get
        {
            var parts = new List<string>();
            if (Candidate.MatchedDeviceName is { } device)
            {
                parts.Add($"fixes: {device}");
            }

            if (Candidate.DriverClass is { Length: > 0 } cls)
            {
                parts.Add(cls);
            }

            if (Candidate.DriverDate is { } date)
            {
                parts.Add(date.ToString("yyyy-MM-dd"));
            }

            if (Candidate.HardwareId is { Length: > 0 } hw)
            {
                parts.Add(hw);
            }

            return string.Join(" · ", parts);
        }
    }

    [ObservableProperty]
    private bool _isSelected;
}
