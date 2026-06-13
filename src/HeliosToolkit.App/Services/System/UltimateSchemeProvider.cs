using HeliosToolkit.App.Services.Tweaks;
using Microsoft.Win32;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Owns the "Helios Ultimate Performance" power scheme — a one-time duplicate of
/// Microsoft's hidden Ultimate Performance template. Shared by the power-ultimate
/// tweak and Game Boost so both activate the exact same scheme.
/// </summary>
public sealed class UltimateSchemeProvider
{
    private const string StateKey = @"SOFTWARE\HeliosToolkit";
    private const string ValueName = "UltimateSchemeGuid";

    /// <summary>Returns the Helios Ultimate scheme GUID, creating it if needed.</summary>
    public async Task<string?> GetOrCreateAsync(CancellationToken ct = default)
    {
        string? guid = RegistryHelper.ReadValue("HKCU", StateKey, ValueName) as string;

        IReadOnlyList<string> existing = await PowerCfg.ListSchemeGuidsAsync(ct);
        if (guid is not null && existing.Contains(guid, StringComparer.OrdinalIgnoreCase))
        {
            return guid;
        }

        guid = await PowerCfg.DuplicateSchemeAsync(PowerCfg.UltimatePerformanceTemplate, ct);
        if (guid is not null)
        {
            RegistryHelper.WriteValue("HKCU", StateKey, ValueName, guid, RegistryValueKind.String);
        }

        return guid;
    }
}
