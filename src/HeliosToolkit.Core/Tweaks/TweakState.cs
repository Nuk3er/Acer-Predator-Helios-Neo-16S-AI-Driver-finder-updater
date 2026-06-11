namespace HeliosToolkit.Core.Tweaks;

/// <summary>Result of detecting a tweak's current state on the machine.</summary>
public enum TweakState
{
    /// <summary>Detection has not run yet or failed.</summary>
    Unknown,

    /// <summary>Every value the tweak touches matches the applied state.</summary>
    Applied,

    /// <summary>The system is in its default / non-tweaked state.</summary>
    NotApplied,

    /// <summary>Some of the values match the tweak, some do not.</summary>
    Mixed,

    /// <summary>The target (service, task, device) does not exist on this system.</summary>
    NotApplicable,
}
