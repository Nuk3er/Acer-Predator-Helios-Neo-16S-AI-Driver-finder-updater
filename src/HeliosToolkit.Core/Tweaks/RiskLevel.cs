namespace HeliosToolkit.Core.Tweaks;

/// <summary>
/// How likely a tweak is to cause problems on a modern Windows 11 gaming laptop.
/// </summary>
public enum RiskLevel
{
    /// <summary>Broad consensus that it helps (or is at worst neutral) and does not break anything.</summary>
    Safe,

    /// <summary>Helps in some setups/games, hurts or does nothing in others. Read the description.</summary>
    Situational,

    /// <summary>Can hurt frame pacing, battery, thermals or security. Only for users who test before/after.</summary>
    Risky,

    /// <summary>Informational card only — nothing is changed by the app.</summary>
    Info,
}
