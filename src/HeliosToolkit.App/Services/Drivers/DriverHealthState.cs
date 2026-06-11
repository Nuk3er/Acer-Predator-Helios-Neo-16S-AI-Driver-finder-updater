using CommunityToolkit.Mvvm.ComponentModel;

namespace HeliosToolkit.App.Services.Drivers;

/// <summary>
/// Shared result of the last device/driver scan, shown on the dashboard.
/// Score: 100 − 15 per problem device − 5 per outdated component − 10 extra
/// if the NVIDIA driver is outdated, clamped to 0–100.
/// </summary>
public partial class DriverHealthState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private int? _score;

    [ObservableProperty]
    private string _summary = "Run a device scan to find broken devices and outdated drivers.";

    public bool HasData => Score is not null;

    public string ScoreText => Score?.ToString() ?? "—";

    public void Update(int problemDevices, int outdatedComponents, bool nvidiaOutdated)
    {
        int score = 100
            - 15 * problemDevices
            - 5 * outdatedComponents
            - (nvidiaOutdated ? 10 : 0);
        Score = Math.Clamp(score, 0, 100);

        var parts = new List<string>();
        parts.Add(problemDevices == 0 ? "no problem devices" : $"{problemDevices} problem device(s)");
        parts.Add(outdatedComponents == 0 ? "drivers look current" : $"{outdatedComponents} update(s) available");
        if (nvidiaOutdated)
        {
            parts.Add("NVIDIA driver outdated");
        }

        Summary = char.ToUpperInvariant(parts[0][0]) + string.Join(", ", parts)[1..] + ".";
    }
}
