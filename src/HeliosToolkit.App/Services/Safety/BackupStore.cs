using System.IO;
using System.Text.Json;
using Serilog;

namespace HeliosToolkit.App.Services.Safety;

/// <summary>One captured original value, enough to put it back exactly as it was.</summary>
public sealed record BackupEntry
{
    public required string TweakId { get; init; }
    public required string Kind { get; init; }      // "registry", "powercfg", "service", "task", "bcd"
    public required string Target { get; init; }     // key!value, scheme/sub/setting, service name, etc.
    public string? OriginalValue { get; init; }      // null ⇒ the value/element did not exist
    public string? ValueType { get; init; }          // registry kind, when relevant
    public bool Existed { get; init; }
    public DateTimeOffset CapturedUtc { get; init; } = DateTimeOffset.UtcNow;
}

public interface IBackupSink
{
    /// <summary>Records an original value the first time it is seen for a (tweak, target) pair.</summary>
    void Capture(BackupEntry entry);
}

public interface IBackupSource
{
    IReadOnlyList<BackupEntry> ForTweak(string tweakId);
    bool HasAny(string tweakId);
}

/// <summary>
/// Append-only JSON log of original values in %ProgramData%\HeliosToolkit\backup.
/// First-seen value for a (tweak, target) wins, so re-applying never overwrites the
/// true original. Thread-safe for the app's modest concurrency.
/// </summary>
public sealed class BackupStore : IBackupSink, IBackupSource
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(AppPaths.Backup, "original-values.json");
    private readonly object _gate = new();
    private readonly List<BackupEntry> _entries;

    public BackupStore()
    {
        _entries = Load(_path);
    }

    public void Capture(BackupEntry entry)
    {
        lock (_gate)
        {
            bool exists = _entries.Any(e =>
                e.TweakId == entry.TweakId &&
                e.Kind == entry.Kind &&
                e.Target.Equals(entry.Target, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                return; // first-seen original wins
            }

            _entries.Add(entry);
            Save();
        }
    }

    public IReadOnlyList<BackupEntry> ForTweak(string tweakId)
    {
        lock (_gate)
        {
            return _entries.Where(e => e.TweakId == tweakId).ToList();
        }
    }

    public bool HasAny(string tweakId)
    {
        lock (_gate)
        {
            return _entries.Any(e => e.TweakId == tweakId);
        }
    }

    public IReadOnlyList<string> BackedUpTweakIds()
    {
        lock (_gate)
        {
            return _entries.Select(e => e.TweakId).Distinct().ToList();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Backup);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, Json));
        }
        catch (IOException e)
        {
            Log.Error(e, "Failed to persist backup store");
        }
    }

    private static List<BackupEntry> Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<List<BackupEntry>>(File.ReadAllText(path)) ?? new();
            }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Warning(e, "Backup store unreadable; starting fresh");
        }

        return new List<BackupEntry>();
    }
}
