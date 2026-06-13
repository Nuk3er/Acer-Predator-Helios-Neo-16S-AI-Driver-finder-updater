using System.IO;
using System.Text.Json;
using HeliosToolkit.Core.Boost;
using Serilog;

namespace HeliosToolkit.App.Services.Boost;

public sealed record WatchedGame(string ExeName, bool PinToPCores);

public sealed record BoostConfig
{
    public bool AutopilotEnabled { get; init; }

    /// <summary>Game exe names (e.g. "cs2.exe") that auto-trigger Boost.</summary>
    public IReadOnlyList<WatchedGame> WatchedGames { get; init; } = Array.Empty<WatchedGame>();

    /// <summary>Background apps Boost closes (full exe paths), restarted on un-Boost.</summary>
    public IReadOnlyList<string> KillList { get; init; } = Array.Empty<string>();

    public bool HoldTimer { get; init; } = true;
    public bool UseUltimatePlan { get; init; } = true;
    public bool EnableDnd { get; init; } = true;
}

/// <summary>Persists Boost configuration and the live session state (atomic writes).</summary>
public sealed class BoostConfigStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public BoostConfig LoadConfig()
    {
        try
        {
            if (File.Exists(AppPaths.BoostConfigFile))
            {
                return JsonSerializer.Deserialize<BoostConfig>(File.ReadAllText(AppPaths.BoostConfigFile)) ?? new();
            }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Warning(e, "Boost config unreadable; using defaults");
        }

        return new BoostConfig();
    }

    public void SaveConfig(BoostConfig config) => WriteAtomic(AppPaths.BoostConfigFile, config);

    public BoostSessionState? LoadSession()
    {
        try
        {
            if (File.Exists(AppPaths.BoostSessionFile))
            {
                return JsonSerializer.Deserialize<BoostSessionState>(File.ReadAllText(AppPaths.BoostSessionFile));
            }
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            Log.Warning(e, "Boost session file unreadable");
        }

        return null;
    }

    public void SaveSession(BoostSessionState state) => WriteAtomic(AppPaths.BoostSessionFile, state);

    public void ClearSession()
    {
        try
        {
            if (File.Exists(AppPaths.BoostSessionFile))
            {
                File.Delete(AppPaths.BoostSessionFile);
            }
        }
        catch (IOException e)
        {
            Log.Debug(e, "Could not delete boost session file");
        }
    }

    private static void WriteAtomic<T>(string path, T value)
    {
        try
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch (IOException e)
        {
            Log.Error(e, "Failed to persist {Path}", path);
        }
    }
}
