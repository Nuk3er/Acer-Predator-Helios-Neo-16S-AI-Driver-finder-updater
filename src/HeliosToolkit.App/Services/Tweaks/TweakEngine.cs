using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;
using Serilog;

namespace HeliosToolkit.App.Services.Tweaks;

public enum ApplyOutcome { Applied, Reverted, Failed }

public sealed record TweakActionResult(ApplyOutcome Outcome, TweakState NewState, string? Error = null);

/// <summary>
/// Drives detect / apply / revert across the catalog. Guarantees a restore point and
/// value-level backups exist before the first change of a session.
/// </summary>
public sealed class TweakEngine
{
    private readonly TweakCatalog _catalog;
    private readonly BackupStore _backup;
    private readonly RestorePointService _restorePoints;
    private readonly SemaphoreSlim _applyGate = new(1, 1);

    private bool _restorePointAttempted;

    public TweakEngine(TweakCatalog catalog, BackupStore backup, RestorePointService restorePoints)
    {
        _catalog = catalog;
        _backup = backup;
        _restorePoints = restorePoints;
    }

    /// <summary>Raised once when the session's first apply triggers a restore point attempt.</summary>
    public event Action<RestorePointResult>? RestorePointCreated;

    public async Task<IReadOnlyDictionary<string, TweakState>> DetectAllAsync(CancellationToken ct = default)
    {
        var results = new Dictionary<string, TweakState>();
        foreach (ITweak tweak in _catalog.All)
        {
            ct.ThrowIfCancellationRequested();
            results[tweak.Meta.Id] = await SafeDetectAsync(tweak, ct);
        }

        return results;
    }

    public Task<TweakState> DetectAsync(string id, CancellationToken ct = default) =>
        SafeDetectAsync(_catalog.Get(id), ct);

    public async Task<TweakActionResult> ApplyAsync(string id, CancellationToken ct = default)
    {
        ITweak tweak = _catalog.Get(id);
        await _applyGate.WaitAsync(ct);
        try
        {
            await EnsureRestorePointAsync();
            await tweak.ApplyAsync(_backup, ct);
            TweakState state = await SafeDetectAsync(tweak, ct);
            Log.Information("Applied tweak {Id} → {State}", id, state);
            return new TweakActionResult(ApplyOutcome.Applied, state);
        }
        catch (Exception e)
        {
            Log.Error(e, "Apply failed for {Id}", id);
            return new TweakActionResult(ApplyOutcome.Failed, await SafeDetectAsync(tweak, ct), e.Message);
        }
        finally
        {
            _applyGate.Release();
        }
    }

    public async Task<TweakActionResult> RevertAsync(string id, CancellationToken ct = default)
    {
        ITweak tweak = _catalog.Get(id);
        await _applyGate.WaitAsync(ct);
        try
        {
            await tweak.RevertAsync(_backup, ct);
            TweakState state = await SafeDetectAsync(tweak, ct);
            Log.Information("Reverted tweak {Id} → {State}", id, state);
            return new TweakActionResult(ApplyOutcome.Reverted, state);
        }
        catch (Exception e)
        {
            Log.Error(e, "Revert failed for {Id}", id);
            return new TweakActionResult(ApplyOutcome.Failed, await SafeDetectAsync(tweak, ct), e.Message);
        }
        finally
        {
            _applyGate.Release();
        }
    }

    /// <summary>Reverts every tweak that has a backup entry. Returns how many were reverted.</summary>
    public async Task<int> RevertAllAsync(CancellationToken ct = default)
    {
        int count = 0;
        foreach (string id in _backup.BackedUpTweakIds())
        {
            if (TweakCatalogMetadata.All.Any(m => m.Id == id))
            {
                await RevertAsync(id, ct);
                count++;
            }
        }

        return count;
    }

    public bool HasBackup(string id) => _backup.HasAny(id);

    private async Task EnsureRestorePointAsync()
    {
        if (_restorePointAttempted)
        {
            return;
        }

        _restorePointAttempted = true;
        RestorePointResult result = await _restorePoints.CreateAsync("Helios Neo Toolkit — before tweaks");
        RestorePointCreated?.Invoke(result);
    }

    private static async Task<TweakState> SafeDetectAsync(ITweak tweak, CancellationToken ct)
    {
        try
        {
            return await tweak.DetectAsync(ct);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Detect failed for {Id}", tweak.Meta.Id);
            return TweakState.Unknown;
        }
    }
}
