namespace HeliosToolkit.Core.Boost;

public sealed record ClosedProcessInfo(string Path);

/// <summary>
/// Persisted Boost session state. Written BEFORE each mutating step so a crash
/// mid-boost can always be rolled back on next launch.
/// </summary>
public sealed record BoostSessionState
{
    public bool Active { get; init; }
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>"manual" or "auto:&lt;game.exe&gt;".</summary>
    public string Trigger { get; init; } = "manual";

    /// <summary>Power scheme that was active before Boost switched to Helios Ultimate.</summary>
    public string? PreviousSchemeGuid { get; init; }

    /// <summary>ToastEnabled value before Boost set DND (null = value was absent).</summary>
    public int? PreviousToastEnabled { get; init; }

    /// <summary>True when the timer hold was already on before Boost (so un-Boost keeps it).</summary>
    public bool TimerWasHolding { get; init; }

    public IReadOnlyList<ClosedProcessInfo> ClosedProcesses { get; init; } = Array.Empty<ClosedProcessInfo>();
}

public enum RestoreActionKind
{
    /// <summary>Switch the active power scheme back (argument = GUID).</summary>
    ActivateScheme,

    /// <summary>Restore HKCU ToastEnabled (argument = original value, or null ⇒ delete).</summary>
    RestoreToast,

    /// <summary>Stop holding the raised timer resolution (only when Boost started it).</summary>
    ReleaseTimerHold,

    /// <summary>Restart a process Boost closed (argument = exe path).</summary>
    RestartProcess,
}

public sealed record RestoreAction(RestoreActionKind Kind, string? Argument);

/// <summary>
/// Pure derivation of the restore steps from a persisted session — unit-testable,
/// tolerant of partial state (every action is independently safe to run).
/// </summary>
public static class BoostRestorePlan
{
    public static IReadOnlyList<RestoreAction> Derive(BoostSessionState state)
    {
        var actions = new List<RestoreAction>();

        if (!state.Active)
        {
            return actions;
        }

        if (!string.IsNullOrWhiteSpace(state.PreviousSchemeGuid))
        {
            actions.Add(new RestoreAction(RestoreActionKind.ActivateScheme, state.PreviousSchemeGuid));
        }

        // Only restore DND when we captured a previous value at all (capture happens
        // before the write, so "no entry" means Boost never touched notifications).
        actions.Add(new RestoreAction(
            RestoreActionKind.RestoreToast,
            state.PreviousToastEnabled?.ToString()));

        if (!state.TimerWasHolding)
        {
            actions.Add(new RestoreAction(RestoreActionKind.ReleaseTimerHold, null));
        }

        foreach (ClosedProcessInfo closed in state.ClosedProcesses)
        {
            if (!string.IsNullOrWhiteSpace(closed.Path))
            {
                actions.Add(new RestoreAction(RestoreActionKind.RestartProcess, closed.Path));
            }
        }

        return actions;
    }
}
