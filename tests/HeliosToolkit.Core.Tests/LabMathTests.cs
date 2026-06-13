using HeliosToolkit.Core.Lab;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class TimerCalibrationMathTests
{
    [Fact]
    public void Stats_ComputesMeanStdevMinMax()
    {
        (double avg, double stdev, double min, double max) =
            TimerCalibrationMath.Stats(new[] { 1.0, 1.1, 0.9, 1.0 });
        Assert.Equal(1.0, avg, 10);
        Assert.Equal(Math.Sqrt(0.005), stdev, 10); // population stdev of these samples
        Assert.Equal(0.9, min);
        Assert.Equal(1.1, max);
    }

    [Fact]
    public void PickBest_LowestScoreWins()
    {
        var worse = new TimerCandidateResult { RequestedHundredNs = 5000, AvgSleepMs = 1.10, StdevMs = 0.05 };
        var better = new TimerCandidateResult { RequestedHundredNs = 5030, AvgSleepMs = 1.02, StdevMs = 0.01 };
        Assert.Same(better, TimerCalibrationMath.PickBest(new[] { worse, better }));
        Assert.Null(TimerCalibrationMath.PickBest(Array.Empty<TimerCandidateResult>()));
    }

    [Fact]
    public void ShouldKeepDefault_WhenWinWithinNoise()
    {
        var @default = new TimerCandidateResult { RequestedHundredNs = 5000, AvgSleepMs = 1.05, StdevMs = 0.02 };
        var marginalWinner = new TimerCandidateResult { RequestedHundredNs = 5030, AvgSleepMs = 1.04, StdevMs = 0.03 };
        // advantage = 0.07-0.07... compute: default score 0.05+0.02=0.07; winner 0.04+0.03=0.07 → advantage 0 ≤ 0.03
        Assert.True(TimerCalibrationMath.ShouldKeepDefault(marginalWinner, @default));

        var clearWinner = new TimerCandidateResult { RequestedHundredNs = 5034, AvgSleepMs = 1.005, StdevMs = 0.002 };
        // advantage = 0.07 - 0.007 = 0.063 > 0.002 → keep the calibrated value
        Assert.False(TimerCalibrationMath.ShouldKeepDefault(clearWinner, @default));
    }

    [Fact]
    public void ShouldKeepDefault_TrueWhenDefaultIsBest()
    {
        var @default = new TimerCandidateResult { RequestedHundredNs = 5000, AvgSleepMs = 1.001, StdevMs = 0.001 };
        Assert.True(TimerCalibrationMath.ShouldKeepDefault(@default, @default));
    }
}

public class FrameStatsTests
{
    [Fact]
    public void Percentile_LinearInterpolation()
    {
        var sorted = new[] { 1.0, 2.0, 3.0, 4.0 };
        Assert.Equal(1.0, FrameStats.Percentile(sorted, 0));
        Assert.Equal(4.0, FrameStats.Percentile(sorted, 100));
        Assert.Equal(2.5, FrameStats.Percentile(sorted, 50));
        Assert.Equal(3.91, FrameStats.Percentile(sorted, 97), 10);
    }

    [Fact]
    public void FromFrameTimes_ComputesFpsAndLows()
    {
        // 99 frames at 10ms + one 50ms spike.
        var times = Enumerable.Repeat(10.0, 99).Append(50.0).ToList();
        BenchStats stats = FrameStats.FromFrameTimes(times);

        Assert.Equal(100, stats.FrameCount);
        Assert.Equal(1000.0 / 10.4, stats.AvgFps, 3);
        Assert.True(stats.OnePercentLowFps < stats.AvgFps);
        Assert.Equal(50.0, stats.MaxFrameTimeMs);
        Assert.Equal(1.04, stats.DurationSeconds, 10);
    }

    [Fact]
    public void ParseCsv_PresentMon2Header()
    {
        const string csv =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,MsBetweenPresents,MsGPUBusy\n" +
            "game.exe,123,0x1,DXGI,0,4.16,3.1\n" +
            "game.exe,123,0x1,DXGI,0,4.20,3.0\n" +
            "game.exe,123,0x1,DXGI,0,NA,3.0\n";
        var times = FrameStats.ParseFrameTimesCsv(csv);
        Assert.Equal(new[] { 4.16, 4.20 }, times);
    }

    [Fact]
    public void ParseCsv_LegacyFrameTimeHeader()
    {
        const string csv = "Application,FrameTime\napp.exe,16.6\napp.exe,16.7\n";
        Assert.Equal(2, FrameStats.ParseFrameTimesCsv(csv).Count);
    }

    [Fact]
    public void ParseCsv_NoKnownColumn_ReturnsEmpty()
    {
        Assert.Empty(FrameStats.ParseFrameTimesCsv("A,B\n1,2\n"));
    }

    [Fact]
    public void FormatDelta_Signs()
    {
        Assert.Equal("+10.0%", FrameStats.FormatDelta(100, 110));
        Assert.Equal("-5.0%", FrameStats.FormatDelta(100, 95).Replace("−", "-"));
    }
}

public class DpcAnalysisTests
{
    [Theory]
    [InlineData(50, DpcVerdict.Good)]
    [InlineData(100, DpcVerdict.Noticeable)]
    [InlineData(499, DpcVerdict.Noticeable)]
    [InlineData(750, DpcVerdict.Concerning)]
    [InlineData(1500, DpcVerdict.Bad)]
    public void Classify_Bands(double maxUs, DpcVerdict expected)
    {
        Assert.Equal(expected, DpcAnalysis.Classify(maxUs));
    }

    [Theory]
    [InlineData("nvlddmkm.sys", "NVIDIA")]
    [InlineData("NETWTW10.SYS", "Wi-Fi")]
    [InlineData("Wdf01000.sys", "framework")]
    [InlineData("totally-unknown.sys", "usual fix")]
    public void Advice_MapsModules(string module, string expectedFragment)
    {
        Assert.Contains(expectedFragment, DpcAnalysis.Advice(module), StringComparison.OrdinalIgnoreCase);
    }
}

public class PingStatsTests
{
    [Fact]
    public void FromRtts_ComputesJitterAndLoss()
    {
        var stats = PingStats.FromRtts(new[] { 10.0, 14.0, 12.0 }, sent: 5);
        Assert.Equal(12.0, stats.AvgMs, 10);
        Assert.Equal(3.0, stats.JitterMs, 10); // (|14-10| + |12-14|) / 2
        Assert.Equal(14.0, stats.MaxMs);
        Assert.Equal(2, stats.Lost);
        Assert.Equal(40.0, stats.LossPercent, 10);
    }

    [Fact]
    public void FromRtts_AllLost()
    {
        var stats = PingStats.FromRtts(Array.Empty<double>(), sent: 4);
        Assert.Equal(4, stats.Lost);
        Assert.Equal(100.0, stats.LossPercent);
    }
}
