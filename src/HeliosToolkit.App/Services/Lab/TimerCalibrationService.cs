using System.Diagnostics;
using System.Runtime.InteropServices;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.Core.Lab;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

public sealed record CalibrationProgress(int CompletedSteps, int TotalSteps, TimerCandidateResult? Latest);

public sealed record CalibrationOutcome(
    IReadOnlyList<TimerCandidateResult> Results,
    TimerCandidateResult Best,
    bool RecommendKeepDefault);

/// <summary>
/// Finds the machine's optimal requested timer resolution near 0.5 ms — the
/// MeasureSleep methodology: for each candidate, request it via
/// NtSetTimerResolution and measure how late (and how consistently) Sleep(1)
/// actually wakes, using the high-precision counter.
/// </summary>
public sealed class TimerCalibrationService(
    TimerResolutionService timerService, CpuTopologyService topology)
{
    private const uint SweepStart = 5000;   // 0.5000 ms
    private const uint SweepEnd = 5100;     // 0.5100 ms
    private const uint SweepStep = 2;       // 0.0002 ms
    private const int WarmupSamples = 5;
    private const int SamplesPerStep = 100;
    private const int ConfirmationSamples = 150;

    public Task<CalibrationOutcome> RunSweepAsync(
        IProgress<CalibrationProgress>? progress = null, CancellationToken ct = default)
    {
        // Dedicated long-running thread: the measurement is timing-critical and
        // must never hop threads or share a scheduler with UI work.
        return Task.Factory.StartNew(
            () => RunSweep(progress, ct),
            ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private CalibrationOutcome RunSweep(IProgress<CalibrationProgress>? progress, CancellationToken ct)
    {
        bool wasHolding = timerService.IsHolding;
        TimerResolutionService.OptOutOfTimerThrottling();
        ElevateMeasurementThread();

        var candidates = new List<uint>();
        if (NtQueryTimerResolution(out _, out uint finest, out _) == 0 && finest < SweepStart)
        {
            candidates.Add(finest); // some machines support finer than 0.5 ms — measure it too
        }

        for (uint r = SweepStart; r <= SweepEnd; r += SweepStep)
        {
            candidates.Add(r);
        }

        int total = candidates.Count + 3; // + confirmation pass
        var results = new List<TimerCandidateResult>(candidates.Count);

        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                TimerCandidateResult result = MeasureCandidate(candidates[i], SamplesPerStep);
                results.Add(result);
                progress?.Report(new CalibrationProgress(i + 1, total, result));
            }

            // Confirmation pass: re-measure the 3 best with more samples to beat noise.
            var topThree = results.OrderBy(r => r.Score).Take(3).Select(r => r.RequestedHundredNs).ToList();
            for (int i = 0; i < topThree.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                TimerCandidateResult confirmed = MeasureCandidate(topThree[i], ConfirmationSamples);
                int index = results.FindIndex(r => r.RequestedHundredNs == confirmed.RequestedHundredNs);
                results[index] = confirmed;
                progress?.Report(new CalibrationProgress(candidates.Count + i + 1, total, confirmed));
            }
        }
        finally
        {
            // Release our sweep request; restore the user's hold at its target value.
            NtSetTimerResolution(SweepEnd, false, out _);
            if (wasHolding)
            {
                timerService.Reapply(timerService.TargetHundredNs);
            }
        }

        TimerCandidateResult best = TimerCalibrationMath.PickBest(results)!;
        TimerCandidateResult? defaultResult = results.FirstOrDefault(
            r => r.RequestedHundredNs == TimerCalibrationMath.DefaultRequestHundredNs);
        bool keepDefault = TimerCalibrationMath.ShouldKeepDefault(best, defaultResult);

        Log.Information(
            "Calibration finished: best {Best} (avg {Avg:0.0000} ms, stdev {Stdev:0.0000}), keepDefault={Keep}",
            best.RequestedHundredNs, best.AvgSleepMs, best.StdevMs, keepDefault);

        return new CalibrationOutcome(results, best, keepDefault);
    }

    private static TimerCandidateResult MeasureCandidate(uint requested, int sampleCount)
    {
        NtSetTimerResolution(requested, true, out uint granted);

        for (int i = 0; i < WarmupSamples; i++)
        {
            Thread.Sleep(1);
        }

        var samples = new double[sampleCount];
        double ticksToMs = 1000.0 / Stopwatch.Frequency;
        for (int i = 0; i < sampleCount; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            Thread.Sleep(1);
            long t1 = Stopwatch.GetTimestamp();
            samples[i] = (t1 - t0) * ticksToMs;
        }

        (double avg, double stdev, double min, double max) = TimerCalibrationMath.Stats(samples);
        return new TimerCandidateResult
        {
            RequestedHundredNs = requested,
            GrantedHundredNs = granted,
            AvgSleepMs = avg,
            StdevMs = stdev,
            MinMs = min,
            MaxMs = max,
        };
    }

    private void ElevateMeasurementThread()
    {
        try
        {
            IntPtr thread = GetCurrentThread();
            SetThreadPriority(thread, 15); // THREAD_PRIORITY_TIME_CRITICAL

            nuint pMask = topology.PCoreMask;
            if (pMask != 0)
            {
                // Pin to the first P-core so E-core scheduling never skews samples.
                nuint firstBit = pMask & (nuint)(-(nint)pMask);
                SetThreadAffinityMask(thread, (UIntPtr)firstBit);
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warning(e, "Could not elevate measurement thread; results may be noisier");
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(uint desired, bool set, out uint actual);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint minimum, out uint maximum, out uint current);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetThreadPriority(IntPtr thread, int priority);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);
}
