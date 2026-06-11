using System.Runtime.InteropServices;
using Serilog;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Holds a 0.5 ms system timer resolution while enabled, and opts this process out
/// of timer-resolution throttling so the request survives being minimized on Win11.
/// </summary>
public sealed class TimerResolutionService
{
    private const uint TargetHundredNs = 5000; // 0.5 ms

    public bool IsHolding { get; private set; }

    public void Start()
    {
        if (IsHolding)
        {
            return;
        }

        try
        {
            OptOutOfTimerThrottling();
            int status = NtSetTimerResolution(TargetHundredNs, true, out uint actual);
            IsHolding = status == 0;
            Log.Information("Timer resolution hold started: status={Status}, actual={Actual}00ns", status, actual);
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
        }
    }

    private static void OptOutOfTimerThrottling()
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
