using System.Globalization;

namespace HeliosToolkit.Core.Lab;

/// <summary>Summary statistics for one benchmark capture.</summary>
public sealed record BenchStats
{
    public required double AvgFps { get; init; }
    public required double OnePercentLowFps { get; init; }
    public required double PointOnePercentLowFps { get; init; }
    public required double AvgFrameTimeMs { get; init; }
    public required double MaxFrameTimeMs { get; init; }
    public required int FrameCount { get; init; }
    public required double DurationSeconds { get; init; }
}

public static class FrameStats
{
    /// <summary>Linear-interpolated percentile over an ascending-sorted list. p in 0..100.</summary>
    public static double Percentile(IReadOnlyList<double> sortedAscending, double p)
    {
        int n = sortedAscending.Count;
        if (n == 0)
        {
            return 0;
        }

        if (n == 1)
        {
            return sortedAscending[0];
        }

        double rank = (p / 100.0) * (n - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sortedAscending[lower];
        }

        double weight = rank - lower;
        return sortedAscending[lower] * (1 - weight) + sortedAscending[upper] * weight;
    }

    public static BenchStats FromFrameTimes(IReadOnlyList<double> frameTimesMs)
    {
        if (frameTimesMs.Count == 0)
        {
            return new BenchStats
            {
                AvgFps = 0, OnePercentLowFps = 0, PointOnePercentLowFps = 0,
                AvgFrameTimeMs = 0, MaxFrameTimeMs = 0, FrameCount = 0, DurationSeconds = 0,
            };
        }

        var sorted = frameTimesMs.OrderBy(t => t).ToList();
        double total = frameTimesMs.Sum();
        double avg = total / frameTimesMs.Count;

        // "1% low FPS" = the FPS equivalent of the 99th-percentile frame time.
        double p99 = Percentile(sorted, 99);
        double p999 = Percentile(sorted, 99.9);

        return new BenchStats
        {
            AvgFps = 1000.0 / avg,
            OnePercentLowFps = p99 > 0 ? 1000.0 / p99 : 0,
            PointOnePercentLowFps = p999 > 0 ? 1000.0 / p999 : 0,
            AvgFrameTimeMs = avg,
            MaxFrameTimeMs = sorted[^1],
            FrameCount = frameTimesMs.Count,
            DurationSeconds = total / 1000.0,
        };
    }

    /// <summary>
    /// Extracts frame times from a PresentMon CSV. Header-driven: finds the
    /// MsBetweenPresents column (PresentMon 2.x) or FrameTime (older builds),
    /// case-insensitively. Non-numeric cells (NA) are skipped.
    /// </summary>
    public static List<double> ParseFrameTimesCsv(string csv)
    {
        var frameTimes = new List<double>();
        using var reader = new StringReader(csv);

        string? header = reader.ReadLine();
        if (header is null)
        {
            return frameTimes;
        }

        string[] columns = header.Split(',');
        int index = Array.FindIndex(columns,
            c => c.Trim().Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = Array.FindIndex(columns,
                c => c.Trim().Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
        }

        if (index < 0)
        {
            return frameTimes;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string[] cells = line.Split(',');
            if (cells.Length > index
                && double.TryParse(cells[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double ms)
                && ms > 0)
            {
                frameTimes.Add(ms);
            }
        }

        return frameTimes;
    }

    /// <summary>"+5.2%" / "−3.1%" style delta of b vs a; green-worthy decided by caller via sign and higherIsBetter.</summary>
    public static string FormatDelta(double a, double b)
    {
        if (a == 0)
        {
            return "—";
        }

        double pct = (b - a) / a * 100.0;
        return $"{(pct >= 0 ? "+" : "")}{pct:0.0}%";
    }
}
