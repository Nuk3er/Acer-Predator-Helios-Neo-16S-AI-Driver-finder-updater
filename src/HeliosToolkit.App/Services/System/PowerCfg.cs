using System.Text.RegularExpressions;

namespace HeliosToolkit.App.Services.System;

/// <summary>
/// Thin wrapper over powercfg.exe. Everything is addressed by GUID, never by
/// localized display name, so it works on any Windows language.
/// </summary>
public static partial class PowerCfg
{
    public const string UltimatePerformanceTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public static async Task<string?> GetActiveSchemeGuidAsync(CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("powercfg", "/getactivescheme", ct);
        Match m = GuidRegex().Match(r.StdOut);
        return m.Success ? m.Value : null;
    }

    public static Task<ProcessResult> SetActiveAsync(string guid, CancellationToken ct = default) =>
        ProcessRunner.RunAsync("powercfg", $"/setactive {guid}", ct);

    /// <summary>Duplicates a scheme and returns the new GUID.</summary>
    public static async Task<string?> DuplicateSchemeAsync(string templateGuid, CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("powercfg", $"-duplicatescheme {templateGuid}", ct);
        Match m = GuidRegex().Match(r.StdOut);
        return m.Success ? m.Value : null;
    }

    public static async Task<IReadOnlyList<string>> ListSchemeGuidsAsync(CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("powercfg", "/list", ct);
        return GuidRegex().Matches(r.StdOut).Select(m => m.Value).Distinct().ToList();
    }

    public static Task<ProcessResult> SetAcValueIndexAsync(
        string scheme, string subgroup, string setting, long value, CancellationToken ct = default) =>
        ProcessRunner.RunAsync("powercfg", $"-setacvalueindex {scheme} {subgroup} {setting} {value}", ct);

    /// <summary>Reads the AC index for a setting from "powercfg /query" output.</summary>
    public static async Task<long?> GetAcValueIndexAsync(
        string scheme, string subgroup, string setting, CancellationToken ct = default)
    {
        ProcessResult r = await ProcessRunner.RunAsync("powercfg", $"/query {scheme} {subgroup} {setting}", ct);
        Match m = AcIndexRegex().Match(r.StdOut);
        if (m.Success && long.TryParse(
                m.Groups[1].Value, global::System.Globalization.NumberStyles.HexNumber, null, out long value))
        {
            return value;
        }

        return null;
    }

    public static Task<ProcessResult> SetHibernateAsync(bool on, CancellationToken ct = default) =>
        ProcessRunner.RunAsync("powercfg", $"/hibernate {(on ? "on" : "off")}", ct);

    public static async Task<bool> IsHibernateEnabledAsync(CancellationToken ct = default)
    {
        object? value = Tweaks.RegistryHelper.ReadValue(
            "HKLM", @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled");
        await Task.CompletedTask;
        return value is int i && i != 0;
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"Current AC Power Setting Index:\s*0x([0-9a-fA-F]+)")]
    private static partial Regex AcIndexRegex();
}
