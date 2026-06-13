using System.Text.Json;
using HeliosToolkit.App.Services.System;
using Serilog;

namespace HeliosToolkit.App.Services.Network;

public sealed record ActiveAdapter(string Name, string InterfaceGuid, string Description, bool IsWifi);

/// <summary>
/// Finds the network adapter that owns the default route (the one games actually use).
/// PowerShell NetAdapter cmdlets, JSON output, cached for 60 s.
/// </summary>
public sealed class ActiveAdapterService
{
    private ActiveAdapter? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<ActiveAdapter?> GetActiveAdapterAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < TimeSpan.FromSeconds(60))
        {
            return _cached;
        }

        const string command =
            "$i = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | " +
            "Sort-Object RouteMetric | Select-Object -First 1 -ExpandProperty ifIndex; " +
            "if ($i) { Get-NetAdapter -InterfaceIndex $i | " +
            "Select-Object Name, InterfaceGuid, InterfaceDescription, PhysicalMediaType | ConvertTo-Json -Compress }";

        ProcessResult result = await RunPsAsync(command, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            Log.Warning("Active adapter lookup failed: {Err}", result.StdErr);
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(result.StdOut);
            JsonElement root = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement[0]
                : doc.RootElement;

            string name = root.GetProperty("Name").GetString() ?? "";
            string guid = root.GetProperty("InterfaceGuid").GetString() ?? "";
            string description = root.TryGetProperty("InterfaceDescription", out JsonElement d)
                ? d.GetString() ?? "" : "";
            string media = root.TryGetProperty("PhysicalMediaType", out JsonElement m)
                ? m.ToString() : "";
            bool isWifi = media.Contains("802.11", StringComparison.OrdinalIgnoreCase)
                || description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
                || description.Contains("Wireless", StringComparison.OrdinalIgnoreCase);

            if (name.Length == 0)
            {
                return null;
            }

            _cached = new ActiveAdapter(name, guid, description, isWifi);
            _cachedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Warning(e, "Active adapter JSON unparsable: {Json}", result.StdOut);
            return null;
        }
    }

    /// <summary>Runs a PowerShell snippet non-interactively and captures output.</summary>
    public static Task<ProcessResult> RunPsAsync(string command, CancellationToken ct = default) =>
        ProcessRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
            ct);

    /// <summary>Escapes a value for use inside single quotes in PowerShell.</summary>
    public static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";
}
