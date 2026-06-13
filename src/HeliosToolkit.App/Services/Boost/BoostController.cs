using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Boost;
using Microsoft.Win32;
using Serilog;

namespace HeliosToolkit.App.Services.Boost;

/// <summary>
/// One-click Game Boost: calibrated timer hold + Ultimate power plan + Do Not
/// Disturb + a curated kill-list, with optional per-game P-core pinning. State
/// is persisted before every mutating step so a crash mid-boost is always
/// recoverable. NOT routed through the tweak engine — this is session state.
/// </summary>
public sealed class BoostController(
    BoostConfigStore store,
    TimerResolutionService timer,
    UltimateSchemeProvider ultimateScheme,
    CpuTopologyService topology)
{
    private const string ToastKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications";

    public bool IsActive { get; private set; }

    public event Action<bool>? StateChanged;

    public async Task BoostAsync(string trigger, CancellationToken ct = default)
    {
        if (IsActive)
        {
            return;
        }

        BoostConfig config = store.LoadConfig();

        // Capture current state and persist intent BEFORE touching anything.
        string? previousScheme = await PowerCfg.GetActiveSchemeGuidAsync(ct);
        int? previousToast = config.EnableDnd
            ? RegistryHelper.ReadValue("HKCU", ToastKey, "ToastEnabled") as int?
            : null;
        bool timerWasHolding = timer.IsHolding;

        var session = new BoostSessionState
        {
            Active = true,
            StartedUtc = DateTimeOffset.UtcNow,
            Trigger = trigger,
            PreviousSchemeGuid = config.UseUltimatePlan ? previousScheme : null,
            PreviousToastEnabled = config.EnableDnd ? (previousToast ?? -1) : null,
            TimerWasHolding = timerWasHolding,
            ClosedProcesses = Array.Empty<ClosedProcessInfo>(),
        };
        store.SaveSession(session);

        // 1. Timer hold (calibrated value).
        if (config.HoldTimer && !timerWasHolding)
        {
            timer.Start();
        }

        // 2. Ultimate power plan.
        if (config.UseUltimatePlan)
        {
            string? guid = await ultimateScheme.GetOrCreateAsync(ct);
            if (guid is not null)
            {
                await PowerCfg.SetActiveAsync(guid, ct);
            }
        }

        // 3. Do Not Disturb (toasts off).
        if (config.EnableDnd)
        {
            RegistryHelper.WriteValue("HKCU", ToastKey, "ToastEnabled", 0, RegistryValueKind.DWord);
        }

        // 4. Kill list — record each path before closing it.
        var closed = new List<ClosedProcessInfo>();
        foreach (string exePath in config.KillList)
        {
            if (CloseByPath(exePath))
            {
                closed.Add(new ClosedProcessInfo(exePath));
                session = session with { ClosedProcesses = closed.ToList() };
                store.SaveSession(session); // persist after each close
            }
        }

        IsActive = true;
        StateChanged?.Invoke(true);
        Log.Information("Boost ON ({Trigger}): timer={Timer}, plan={Plan}, dnd={Dnd}, killed={Killed}",
            trigger, config.HoldTimer, config.UseUltimatePlan, config.EnableDnd, closed.Count);
    }

    public async Task UnboostAsync(CancellationToken ct = default)
    {
        BoostSessionState? session = store.LoadSession();
        if (session is null)
        {
            IsActive = false;
            StateChanged?.Invoke(false);
            return;
        }

        await RestoreAsync(session, ct);
        store.ClearSession();
        IsActive = false;
        StateChanged?.Invoke(false);
        Log.Information("Boost OFF");
    }

    /// <summary>At startup: if a session was left active by a crash, roll it back.</summary>
    public async Task<bool> RecoverAsync(CancellationToken ct = default)
    {
        BoostSessionState? session = store.LoadSession();
        if (session is not { Active: true })
        {
            return false;
        }

        Log.Warning("Recovering Boost session left active from a previous run");
        await RestoreAsync(session, ct);
        store.ClearSession();
        return true;
    }

    /// <summary>Applies High priority + P-core affinity to a game process (best effort).</summary>
    public bool PinGame(Process game)
    {
        try
        {
            nuint mask = topology.PCoreMask;
            if (mask == 0)
            {
                return false;
            }

            game.PriorityClass = ProcessPriorityClass.High;
            game.ProcessorAffinity = (IntPtr)(long)mask;
            Log.Information("Pinned {Game} to P-cores (mask 0x{Mask:X})", game.ProcessName, (ulong)mask);
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            Log.Information(e, "Could not pin {Game} (protected process?)", game.ProcessName);
            return false;
        }
    }

    private async Task RestoreAsync(BoostSessionState session, CancellationToken ct)
    {
        foreach (RestoreAction action in BoostRestorePlan.Derive(session))
        {
            try
            {
                switch (action.Kind)
                {
                    case RestoreActionKind.ActivateScheme when action.Argument is { Length: > 0 }:
                        await PowerCfg.SetActiveAsync(action.Argument, ct);
                        break;

                    case RestoreActionKind.RestoreToast:
                        if (action.Argument is { Length: > 0 } && int.TryParse(action.Argument, out int toast) && toast >= 0)
                        {
                            RegistryHelper.WriteValue("HKCU", ToastKey, "ToastEnabled", toast, RegistryValueKind.DWord);
                        }
                        else
                        {
                            RegistryHelper.DeleteValue("HKCU", ToastKey, "ToastEnabled");
                        }

                        break;

                    case RestoreActionKind.ReleaseTimerHold:
                        timer.Stop();
                        break;

                    case RestoreActionKind.RestartProcess when action.Argument is { Length: > 0 }:
                        RestartDeElevated(action.Argument);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Restore action {Kind} failed", action.Kind);
            }
        }
    }

    private static bool CloseByPath(string exePath)
    {
        bool closedAny = false;
        string name = Path.GetFileNameWithoutExtension(exePath);
        foreach (Process p in Process.GetProcessesByName(name))
        {
            try
            {
                // Graceful first, force after 5 s.
                if (p.CloseMainWindow())
                {
                    if (!p.WaitForExit(5000))
                    {
                        p.Kill(entireProcessTree: true);
                    }
                }
                else
                {
                    p.Kill(entireProcessTree: true);
                }

                closedAny = true;
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                Log.Debug(e, "Could not close {Name}", name);
            }
            finally
            {
                p.Dispose();
            }
        }

        return closedAny;
    }

    /// <summary>
    /// Restarts an app via Explorer so it inherits the user's (non-elevated) token —
    /// we run as admin and a direct Process.Start would wrongly elevate it.
    /// </summary>
    private static void RestartDeElevated(string exePath)
    {
        if (!File.Exists(exePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{exePath}\"") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Debug(e, "Could not restart {Path}", exePath);
        }
    }
}
