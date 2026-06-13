using System.Runtime.InteropServices;
using HeliosToolkit.App.Services.Tweaks;
using Microsoft.Win32;
using Serilog;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Holds a raised system timer resolution while enabled, and opts this process out
/// of timer-resolution throttling so the request survives being minimized on Win11.
/// The target value comes from the Lab calibrator (HKCU\SOFTWARE\HeliosToolkit,
/// 100 ns units) and falls back to 0.5 ms.
/// </summary>
public sealed class TimerResolutionService
{
    private const string StateKey = @"SOFTWARE\HeliosToolkit";
    private const string CalibratedValueName = "CalibratedTimerRes";
    public const uint DefaultHundredNs = 5000; // 0.5 ms

    public bool IsHolding { get; private set; }

    /// <summary>What we will request (calibrated, or the 0.5 ms default).</summary>
    public uint TargetHundredNs
    {
        get
        {
            object? value = RegistryHelper.ReadValue("HKCU", StateKey, CalibratedValueName);
            // Sanity window: 0.45–0.60 ms; anything else means a corrupt value — use default.
            return value is int i && i is >= 4500 and <= 6000 ? (uint)i : DefaultHundredNs;
        }
    }

    /// <summary>What the kernel actually granted for the current hold (100 ns units).</summary>
    public uint GrantedHundredNs { get; private set; }

    public double TargetMs => TargetHundredNs / 10_000.0;

    /// <summary>Persists a calibrated value and re-applies it when a hold is active.</summary>
    public void SaveCalibration(uint hundredNs)
    {
        RegistryHelper.WriteValue("HKCU", StateKey, CalibratedValueName, (int)hundredNs, RegistryValueKind.DWord);
        RegistryHelper.WriteValue("HKCU", StateKey, "CalibratedAtUtc",
            DateTimeOffset.UtcNow.ToString("O"), RegistryValueKind.String);
        Log.Information("Calibrated timer resolution saved: {Value} (0.{Frac:0000} ms)", hundredNs, hundredNs % 10000);

        if (IsHolding)
        {
            Reapply(hundredNs);
        }
    }

    public void Start()
    {
        if (IsHolding)
        {
            return;
        }

        try
        {
            OptOutOfTimerThrottling();
            uint target = TargetHundredNs;
            int status = NtSetTimerResolution(target, true, out uint actual);
            IsHolding = status == 0;
            GrantedHundredNs = actual;
            Log.Information("Timer hold started: requested {Req} → granted {Actual} (100ns units)", target, actual);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warning(e, "NtSetTimerResolution unavailable");
        }
    }

    public void Stop()
    {
        if (!IsHolding)
        {
            return;
        }

        try
        {
            NtSetTimerResolution(TargetHundredNs, false, out _);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warning(e, "Failed releasing timer resolution");
        }
        finally
        {
            IsHolding = false;
            GrantedHundredNs = 0;
        }
    }

    /// <summary>Swaps the active hold to a new value (used right after calibration).</summary>
    public void Reapply(uint hundredNs)
    {
        try
        {
            NtSetTimerResolution(hundredNs, true, out uint actual);
            GrantedHundredNs = actual;
            Log.Information("Timer hold re-applied: {Req} → {Actual}", hundredNs, actual);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            Log.Warning(e, "Reapply failed");
        }
    }

    /// <summary>Exposed for the calibrator, which needs the same throttling opt-out.</summary>
    public static void OptOutOfTimerThrottling()
    {
        // PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = 1,
            ControlMask = 0x4,
            StateMask = 0, // 0 = do NOT throttle (honor our timer request even when minimized)
        };

        int size = Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>();
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(state, buffer, false);
            // ProcessPowerThrottling = 4
            SetProcessInformation(GetCurrentProcess(), 4, buffer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int infoClass, IntPtr info, uint size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}
