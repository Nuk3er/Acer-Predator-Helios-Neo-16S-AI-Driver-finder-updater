using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>
/// Escape hatch for tweaks whose mechanism doesn't fit a declarative primitive
/// (powercfg, the NVIDIA adapter-key hunt, timer resolution, the BCD pair, etc.).
/// </summary>
public sealed class DelegateTweak : ITweak
{
    private readonly Func<CancellationToken, Task<TweakState>> _detect;
    private readonly Func<IBackupSink, CancellationToken, Task> _apply;
    private readonly Func<IBackupSource, CancellationToken, Task> _revert;

    public DelegateTweak(
        TweakMetadata meta,
        Func<CancellationToken, Task<TweakState>> detect,
        Func<IBackupSink, CancellationToken, Task> apply,
        Func<IBackupSource, CancellationToken, Task> revert)
    {
        Meta = meta;
        _detect = detect;
        _apply = apply;
        _revert = revert;
    }

    public TweakMetadata Meta { get; }

    public Task<TweakState> DetectAsync(CancellationToken ct = default) => _detect(ct);

    public Task ApplyAsync(IBackupSink backup, CancellationToken ct = default) => _apply(backup, ct);

    public Task RevertAsync(IBackupSource backup, CancellationToken ct = default) => _revert(backup, ct);
}

/// <summary>Static informational card: detects presence, never changes anything.</summary>
public sealed class InfoTweak : IInfoTweak
{
    private readonly Func<CancellationToken, Task<TweakState>> _detect;

    public InfoTweak(TweakMetadata meta, string? linkUrl, Func<CancellationToken, Task<TweakState>> detect)
    {
        Meta = meta;
        LinkUrl = linkUrl;
        _detect = detect;
    }

    public TweakMetadata Meta { get; }
    public string? LinkUrl { get; }

    public Task<TweakState> DetectAsync(CancellationToken ct = default) => _detect(ct);

    public Task ApplyAsync(IBackupSink backup, CancellationToken ct = default) => Task.CompletedTask;

    public Task RevertAsync(IBackupSource backup, CancellationToken ct = default) => Task.CompletedTask;
}
