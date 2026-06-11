using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.Core.Tweaks;
using Microsoft.Win32;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>
/// Disables (or restores) a Windows service by its registry Start value, which
/// works even for services the current user can't control via the SCM. If the
/// service key doesn't exist the tweak reports NotApplicable.
/// </summary>
public sealed class ServiceStartupTweak : ITweak
{
    private const int StartDisabled = 4;
    private readonly string _serviceName;
    private readonly int _appliedStart;

    public ServiceStartupTweak(TweakMetadata meta, string serviceName, int appliedStart = StartDisabled)
    {
        Meta = meta;
        _serviceName = serviceName;
        _appliedStart = appliedStart;
    }

    public TweakMetadata Meta { get; }

    private string SubKey => $@"SYSTEM\CurrentControlSet\Services\{_serviceName}";

    public Task<TweakState> DetectAsync(CancellationToken ct = default)
    {
        if (!RegistryHelper.SubKeyExists("HKLM", SubKey))
        {
            return Task.FromResult(TweakState.NotApplicable);
        }

        object? start = RegistryHelper.ReadValue("HKLM", SubKey, "Start");
        TweakState state = start is int i && i == _appliedStart ? TweakState.Applied : TweakState.NotApplied;
        return Task.FromResult(state);
    }

    public Task ApplyAsync(IBackupSink backup, CancellationToken ct = default)
    {
        if (!RegistryHelper.SubKeyExists("HKLM", SubKey))
        {
            return Task.CompletedTask;
        }

        object? start = RegistryHelper.ReadValue("HKLM", SubKey, "Start");
        backup.Capture(new BackupEntry
        {
            TweakId = Meta.Id,
            Kind = "service",
            Target = _serviceName,
            Existed = start is not null,
            OriginalValue = start is int i ? i.ToString() : null,
        });

        RegistryHelper.WriteValue("HKLM", SubKey, "Start", _appliedStart, RegistryValueKind.DWord);
        return Task.CompletedTask;
    }

    public Task RevertAsync(IBackupSource backup, CancellationToken ct = default)
    {
        if (!RegistryHelper.SubKeyExists("HKLM", SubKey))
        {
            return Task.CompletedTask;
        }

        BackupEntry? entry = backup.ForTweak(Meta.Id).FirstOrDefault(e => e.Kind == "service");
        if (entry?.OriginalValue is not null && int.TryParse(entry.OriginalValue, out int original))
        {
            RegistryHelper.WriteValue("HKLM", SubKey, "Start", original, RegistryValueKind.DWord);
        }

        return Task.CompletedTask;
    }
}
