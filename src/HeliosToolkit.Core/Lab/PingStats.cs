namespace HeliosToolkit.Core.Lab;

public sealed record PingResultStats(double AvgMs, double JitterMs, double MaxMs, int Sent, int Lost)
{
    public double LossPercent => Sent == 0 ? 0 : 100.0 * Lost / Sent;
}

public static class PingStats
{
    /// <summary>
    /// Jitter = mean absolute difference between consecutive round-trips —
    /// the spike measure that matters for games, not the standard deviation.
    /// </summary>
    public static PingResultStats FromRtts(IReadOnlyList<double> rttsMs, int sent)
    {
        int lost = Math.Max(0, sent - rttsMs.Count);
        if (rttsMs.Count == 0)
        {
            return new PingResultStats(0, 0, 0, sent, lost);
        }

        double avg = rttsMs.Average();
        double max = rttsMs.Max();

        double jitter = 0;
        if (rttsMs.Count > 1)
        {
            double sum = 0;
            for (int i = 1; i < rttsMs.Count; i++)
            {
                sum += Math.Abs(rttsMs[i] - rttsMs[i - 1]);
            }

            jitter = sum / (rttsMs.Count - 1);
        }

        return new PingResultStats(avg, jitter, max, sent, lost);
    }
}
