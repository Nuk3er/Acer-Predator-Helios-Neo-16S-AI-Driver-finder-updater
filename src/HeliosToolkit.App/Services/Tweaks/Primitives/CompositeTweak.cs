using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>
/// Presents several sub-tweaks (sharing one metadata id) as a single toggle.
/// Detect rolls the parts up: all Applied ⇒ Applied; all NotApplicable ⇒ NotApplicable;
/// none Applied ⇒ NotApplied; anything else ⇒ Mixed. Apply/Revert run every part.
/// </summary>
public sealed class CompositeTweak : ITweak
{
    private readonly IReadOnlyList<ITweak> _parts;

    public CompositeTweak(TweakMetadata meta, IReadOnlyList<ITweak> parts)
    {
        Meta = meta;
        _parts = parts;
    }

    public TweakMetadata Meta { get; }

    public async Task<TweakState> DetectAsync(CancellationToken ct = default)
    {
        var states = new List<TweakState>(_parts.Count);
        foreach (ITweak part in _parts)
        {
            states.Add(await part.DetectAsync(ct));
        }

        var relevant = states.Where(s => s != TweakState.NotApplicable).ToList();
        if (relevant.Count == 0)
        {
            return TweakState.NotApplicable;
        }

        if (relevant.All(s => s == TweakState.Applied))
        {
            return TweakState.Applied;
        }

        if (relevant.All(s => s == TweakState.NotApplied))
        {
            return TweakState.NotApplied;
        }

        return TweakState.Mixed;
    }

    public async Task ApplyAsync(IBackupSink backup, CancellationToken ct = default)
    {
        foreach (ITweak part in _parts)
        {
            await part.ApplyAsync(backup, ct);
        }
    }

    public async Task RevertAsync(IBackupSource backup, CancellationToken ct = default)
    {
        foreach (ITweak part in _parts)
        {
            await part.RevertAsync(backup, ct);
        }
    }
}

public static class TweakCombineExtensions
{
    /// <summary>Combines two tweaks (same metadata id) into one composite toggle.</summary>
    public static CompositeTweak Combine(this ITweak first, ITweak second)
    {
        if (first.Meta.Id != second.Meta.Id)
        {
            throw new ArgumentException(
                $"Cannot combine tweaks with different ids: '{first.Meta.Id}' vs '{second.Meta.Id}'.");
        }

        var parts = new List<ITweak>();
        AddParts(parts, first);
        AddParts(parts, second);
        return new CompositeTweak(first.Meta, parts);
    }

    private static void AddParts(List<ITweak> parts, ITweak tweak)
    {
        if (tweak is CompositeTweak)
        {
            // Flatten by re-querying: composite exposes its behavior, so just add it whole.
            parts.Add(tweak);
        }
        else
        {
            parts.Add(tweak);
        }
    }
}
