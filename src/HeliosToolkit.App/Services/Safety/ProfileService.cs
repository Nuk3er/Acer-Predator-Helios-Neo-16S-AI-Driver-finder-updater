using System.IO;
using System.Text.Json;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Serilog;

namespace HeliosToolkit.App.Services.Safety;

public sealed record TweakProfile
{
    public string Name { get; init; } = "Helios profile";
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>tweak id → desired "on" state. Absent ids are left untouched on import.</summary>
    public Dictionary<string, bool> Tweaks { get; init; } = new();
}

public sealed record ProfileImportReport(int Applied, int Reverted, int Skipped, int Failed);

/// <summary>Exports the current applied-tweak set and re-applies a saved one.</summary>
public sealed class ProfileService(TweakEngine engine)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task ExportAsync(string path, string name, CancellationToken ct = default)
    {
        IReadOnlyDictionary<string, TweakState> states = await engine.DetectAllAsync(ct);
        var profile = new TweakProfile
        {
            Name = name,
            Tweaks = states
                .Where(kv => kv.Value is TweakState.Applied or TweakState.NotApplied)
                .ToDictionary(kv => kv.Key, kv => kv.Value == TweakState.Applied),
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(profile, Json), ct);
        Log.Information("Exported profile '{Name}' to {Path}", name, path);
    }

    public async Task<ProfileImportReport> ImportAsync(string path, CancellationToken ct = default)
    {
        TweakProfile? profile = JsonSerializer.Deserialize<TweakProfile>(await File.ReadAllTextAsync(path, ct));
        if (profile is null)
        {
            throw new InvalidDataException("Profile file is empty or invalid.");
        }

        int applied = 0, reverted = 0, skipped = 0, failed = 0;
        foreach ((string id, bool wantOn) in profile.Tweaks)
        {
            ct.ThrowIfCancellationRequested();
            if (TweakCatalogMetadata.All.All(m => m.Id != id))
            {
                skipped++;
                continue;
            }

            TweakState current = await engine.DetectAsync(id, ct);
            if (current == TweakState.NotApplicable)
            {
                skipped++;
                continue;
            }

            bool isOn = current == TweakState.Applied;
            if (isOn == wantOn)
            {
                skipped++;
                continue;
            }

            TweakActionResult result = wantOn
                ? await engine.ApplyAsync(id, ct)
                : await engine.RevertAsync(id, ct);

            if (result.Outcome == ApplyOutcome.Failed)
            {
                failed++;
            }
            else if (wantOn)
            {
                applied++;
            }
            else
            {
                reverted++;
            }
        }

        Log.Information("Imported profile '{Name}': {Applied} applied, {Reverted} reverted, {Skipped} skipped, {Failed} failed",
            profile.Name, applied, reverted, skipped, failed);
        return new ProfileImportReport(applied, reverted, skipped, failed);
    }
}
