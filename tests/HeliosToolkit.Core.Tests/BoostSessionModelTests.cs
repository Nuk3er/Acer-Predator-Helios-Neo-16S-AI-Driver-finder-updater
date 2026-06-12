using HeliosToolkit.Core.Boost;
using Xunit;

namespace HeliosToolkit.Core.Tests;

public class BoostSessionModelTests
{
    [Fact]
    public void Derive_InactiveSession_NoActions()
    {
        Assert.Empty(BoostRestorePlan.Derive(new BoostSessionState { Active = false }));
    }

    [Fact]
    public void Derive_FullSession_AllActionsInOrder()
    {
        var state = new BoostSessionState
        {
            Active = true,
            PreviousSchemeGuid = "381b4222-f694-41f0-9685-ff5bb260df2e",
            PreviousToastEnabled = 1,
            TimerWasHolding = false,
            ClosedProcesses = new[] { new ClosedProcessInfo(@"C:\apps\discord.exe") },
        };

        var actions = BoostRestorePlan.Derive(state);

        Assert.Equal(RestoreActionKind.ActivateScheme, actions[0].Kind);
        Assert.Equal(state.PreviousSchemeGuid, actions[0].Argument);
        Assert.Equal(RestoreActionKind.RestoreToast, actions[1].Kind);
        Assert.Equal("1", actions[1].Argument);
        Assert.Equal(RestoreActionKind.ReleaseTimerHold, actions[2].Kind);
        Assert.Equal(RestoreActionKind.RestartProcess, actions[3].Kind);
        Assert.Equal(@"C:\apps\discord.exe", actions[3].Argument);
    }

    [Fact]
    public void Derive_TimerWasAlreadyHolding_KeepsHold()
    {
        var state = new BoostSessionState { Active = true, TimerWasHolding = true };
        Assert.DoesNotContain(BoostRestorePlan.Derive(state),
            a => a.Kind == RestoreActionKind.ReleaseTimerHold);
    }

    [Fact]
    public void Derive_PartialState_CrashBeforeSchemeSwitch()
    {
        // Crash after persisting Active but before any capture: only toast-restore
        // (null ⇒ delete/leave default) survives; everything else absent.
        var state = new BoostSessionState { Active = true };
        var actions = BoostRestorePlan.Derive(state);

        Assert.DoesNotContain(actions, a => a.Kind == RestoreActionKind.ActivateScheme);
        Assert.DoesNotContain(actions, a => a.Kind == RestoreActionKind.RestartProcess);
        Assert.Contains(actions, a => a.Kind == RestoreActionKind.RestoreToast && a.Argument is null);
    }

    [Fact]
    public void Derive_SkipsBlankProcessPaths()
    {
        var state = new BoostSessionState
        {
            Active = true,
            ClosedProcesses = new[] { new ClosedProcessInfo(""), new ClosedProcessInfo(@"C:\x.exe") },
        };
        Assert.Single(BoostRestorePlan.Derive(state), a => a.Kind == RestoreActionKind.RestartProcess);
    }
}
