namespace HeliosToolkit.Core.Tweaks;

/// <summary>Which page of the app a tweak lives on.</summary>
public enum TweakPage
{
    Nvidia,
    Windows,
}

/// <summary>
/// Static description of a tweak. Pure data so the catalog can be sanity-checked by unit tests.
/// </summary>
public sealed record TweakMetadata
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required TweakPage Page { get; init; }

    /// <summary>Group header shown in the UI, e.g. "GPU", "Power", "Input", "Network".</summary>
    public required string Category { get; init; }

    public required RiskLevel Risk { get; init; }

    /// <summary>What the tweak does, why, and what it touches. Shown in the UI.</summary>
    public required string Description { get; init; }

    /// <summary>Extra caution text. Required for <see cref="RiskLevel.Risky"/> tweaks.</summary>
    public string? Warning { get; init; }

    /// <summary>True when the change only takes effect after a reboot (or sign-out, see <see cref="RebootNote"/>).</summary>
    public bool RequiresReboot { get; init; }

    /// <summary>Overrides the default "Reboot required" hint, e.g. "Sign out required".</summary>
    public string? RebootNote { get; init; }
}
