using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;

namespace HeliosToolkit.App.Services.Tweaks;

/// <summary>
/// A single applyable/revertable tweak. Metadata is the pure description from
/// the Core catalog; the implementation lives here in the Windows-only project.
/// </summary>
public interface ITweak
{
    TweakMetadata Meta { get; }

    /// <summary>Reads the current machine state for this tweak.</summary>
    Task<TweakState> DetectAsync(CancellationToken ct = default);

    /// <summary>Captures originals into the sink, then applies the tweak.</summary>
    Task ApplyAsync(IBackupSink backup, CancellationToken ct = default);

    /// <summary>Restores the captured originals (or sensible defaults if none were captured).</summary>
    Task RevertAsync(IBackupSource backup, CancellationToken ct = default);
}

/// <summary>Informational cards that detect presence but never change anything.</summary>
public interface IInfoTweak : ITweak
{
    /// <summary>A URL the card can open (download page etc.), if any.</summary>
    string? LinkUrl { get; }
}
