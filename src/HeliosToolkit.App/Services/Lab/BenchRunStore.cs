using System.IO;
using System.Text.Json;
using HeliosToolkit.Core.Lab;
using Serilog;

namespace HeliosToolkit.App.Services.Lab;

/// <summary>A saved benchmark run with the tweak state captured at the time.</summary>
public sealed record BenchRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; init; } = "";
    public string Game { get; init; } = "";
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public BenchStats Stats { get; init; } = null!;
    public string CsvPath { get; init; } = "";

    /// <summary>tweak id → applied (true) / not (false), from TweakEngine.DetectAllAsync at capture time.</summary>
    public Dictionary<string, bool> AppliedTweaks { get; init; } = new();

    public string Display => $"{Label} — {Stats.AvgFps:0} fps avg, {Stats.OnePercentLowFps:0} fps 1% low " +
                             $"({TimestampUtc.LocalDateTime:MMM d HH:mm})";
}

/// <summary>Persists benchmark runs as JSON in ProgramData and prunes old CSVs.</summary>
public sealed class BenchRunStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private const int MaxRuns = 50;

    public void Save(BenchRun run)
    {
        try
        {
            string path = Path.Combine(AppPaths.BenchRuns, $"{run.Id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(run, Json));
            Prune();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to save bench run");
        }
    }

    public IReadOnlyList<BenchRun> LoadAll()
    {
        var runs = new List<BenchRun>();
        if (!Directory.Exists(AppPaths.BenchRuns))
        {
            return runs;
        }

        foreach (string file in Directory.EnumerateFiles(AppPaths.BenchRuns, "*.json"))
        {
            try
            {
                BenchRun? run = JsonSerializer.Deserialize<BenchRun>(File.ReadAllText(file));
                if (run is not null)
                {
                    runs.Add(run);
                }
            }
            catch (Exception e) when (e is IOException or JsonException)
            {
                Log.Debug(e, "Skipping unreadable bench run {File}", file);
            }
        }

        return runs.OrderByDescending(r => r.TimestampUtc).ToList();
    }

    /// <summary>Tweak ids whose applied-state differs between two runs — the "what changed" list.</summary>
    public static IReadOnlyList<string> DifferingTweaks(BenchRun a, BenchRun b)
    {
        var ids = a.AppliedTweaks.Keys.Union(b.AppliedTweaks.Keys);
        return ids
            .Where(id => a.AppliedTweaks.GetValueOrDefault(id) != b.AppliedTweaks.GetValueOrDefault(id))
            .OrderBy(id => id)
            .ToList();
    }

    private void Prune()
    {
        try
        {
            var files = Directory.EnumerateFiles(AppPaths.BenchRuns, "*.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(MaxRuns)
                .ToList();
            foreach (FileInfo old in files)
            {
                old.Delete();
            }
        }
        catch (IOException e)
        {
            Log.Debug(e, "Bench prune failed");
        }
    }
}
