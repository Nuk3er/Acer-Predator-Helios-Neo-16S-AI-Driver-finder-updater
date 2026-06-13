namespace HeliosToolkit.Core.Lab;

/// <summary>Measured result for one candidate timer resolution.</summary>
public sealed record TimerCandidateResult
{
    /// <summary>The resolution requested via NtSetTimerResolution, in 100 ns units (5000 = 0.5 ms).</summary>
    public required uint RequestedHundredNs { get; init; }

    /// <summary>What the kernel actually granted, in 100 ns units.</summary>
    public uint GrantedHundredNs { get; init; }

    public required double AvgSleepMs { get; init; }
    public required double StdevMs { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }

    public double RequestedMs => RequestedHundredNs / 10_000.0;

    /// <summary>
    /// Lower is better: how late Sleep(1) wakes on average, plus how much that jitters.
    /// The community methodology (MeasureSleep) optimizes exactly these two.
    /// </summary>
    public double Score => Math.Abs(AvgSleepMs - 1.0) + StdevMs;
}

public static class TimerCalibrationMath
{
    public const uint DefaultRequestHundredNs = 5000; // 0.5 ms

    /// <summary>Sample statistics (population stdev) for a set of measured sleep durations.</summary>
    public static (double Avg, double Stdev, double Min, double Max) Stats(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        double sum = 0, min = double.MaxValue, max = double.MinValue;
        foreach (double s in samples)
        {
            sum += s;
            min = Math.Min(min, s);
            max = Math.Max(max, s);
        }

        double avg = sum / samples.Count;
        double sq = 0;
        foreach (double s in samples)
        {
            sq += (s - avg) * (s - avg);
        }

        return (avg, Math.Sqrt(sq / samples.Count), min, max);
    }

    /// <summary>The candidate with the lowest score, or null when there are no results.</summary>
    public static TimerCandidateResult? PickBest(IReadOnlyList<TimerCandidateResult> results) =>
        results.Count == 0 ? null : results.MinBy(r => r.Score);

    /// <summary>
    /// Honesty rule: recommend keeping the plain 0.5 ms request when the winner's advantage
    /// over it is smaller than the winner's own jitter — a "win" inside the noise floor
    /// is not a win worth a nonstandard setting.
    /// </summary>
    public static bool ShouldKeepDefault(TimerCandidateResult best, TimerCandidateResult? defaultResult)
    {
        if (best.RequestedHundredNs == DefaultRequestHundredNs || defaultResult is null)
        {
            return best.RequestedHundredNs == DefaultRequestHundredNs;
        }

        double advantage = defaultResult.Score - best.Score;
        return advantage <= best.StdevMs;
    }
}
