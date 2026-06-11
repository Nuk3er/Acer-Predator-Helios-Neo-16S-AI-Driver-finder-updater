using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.Core.Tweaks;
using Microsoft.Win32;

namespace HeliosToolkit.App.Services.Tweaks.Primitives;

/// <summary>One registry value a tweak sets, with the knowledge of how to revert it.</summary>
public sealed record RegistryValueSpec
{
    public required string Hive { get; init; }
    public required string SubKey { get; init; }
    public required string Name { get; init; }
    public required object AppliedValue { get; init; }
    public required RegistryValueKind Kind { get; init; }

    /// <summary>
    /// What revert should do when no original was captured:
    /// true  ⇒ delete the value (it represents a Windows default-by-absence),
    /// false ⇒ write <see cref="DefaultValue"/>.
    /// </summary>
    public bool DeleteOnRevertWhenAbsent { get; init; } = true;

    public object? DefaultValue { get; init; }

    public string Target => $"{Hive}\\{SubKey}!{Name}";

    public bool Matches(object? current) => current is not null && ValuesEqual(current, AppliedValue);

    internal static bool ValuesEqual(object a, object b)
    {
        if (a is int or long || b is int or long)
        {
            return Convert.ToInt64(a) == Convert.ToInt64(b);
        }

        if (a is string[] sa && b is string[] sb)
        {
            return sa.SequenceEqual(sb);
        }

        return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A tweak built from one or more registry values. Detection compares every value:
/// all-match ⇒ Applied, none-match ⇒ NotApplied, otherwise Mixed.
/// </summary>
public class RegistryValueTweak : ITweak
{
    private readonly IReadOnlyList<RegistryValueSpec> _specs;

    public RegistryValueTweak(TweakMetadata meta, params RegistryValueSpec[] specs)
    {
        Meta = meta;
        _specs = specs;
    }

    public TweakMetadata Meta { get; }

    public virtual Task<TweakState> DetectAsync(CancellationToken ct = default)
    {
        int matched = 0;
        foreach (RegistryValueSpec spec in _specs)
        {
            object? current = RegistryHelper.ReadValue(spec.Hive, spec.SubKey, spec.Name);
            if (spec.Matches(current))
            {
                matched++;
            }
        }

        TweakState state = matched == _specs.Count ? TweakState.Applied
            : matched == 0 ? TweakState.NotApplied
            : TweakState.Mixed;
        return Task.FromResult(state);
    }

    public virtual Task ApplyAsync(IBackupSink backup, CancellationToken ct = default)
    {
        foreach (RegistryValueSpec spec in _specs)
        {
            object? current = RegistryHelper.ReadValue(spec.Hive, spec.SubKey, spec.Name);
            RegistryValueKind? currentKind = RegistryHelper.ValueKind(spec.Hive, spec.SubKey, spec.Name);

            backup.Capture(new BackupEntry
            {
                TweakId = Meta.Id,
                Kind = "registry",
                Target = spec.Target,
                Existed = current is not null,
                OriginalValue = current is null ? null : RegistryHelper.Serialize(current, currentKind ?? spec.Kind),
                ValueType = (currentKind ?? spec.Kind).ToString(),
            });

            RegistryHelper.WriteValue(spec.Hive, spec.SubKey, spec.Name, spec.AppliedValue, spec.Kind);
        }

        return Task.CompletedTask;
    }

    public virtual Task RevertAsync(IBackupSource backup, CancellationToken ct = default)
    {
        var entries = backup.ForTweak(Meta.Id)
            .Where(e => e.Kind == "registry")
            .ToDictionary(e => e.Target, StringComparer.OrdinalIgnoreCase);

        foreach (RegistryValueSpec spec in _specs)
        {
            if (entries.TryGetValue(spec.Target, out BackupEntry? entry))
            {
                if (entry.Existed && entry.OriginalValue is not null)
                {
                    (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(entry.OriginalValue);
                    RegistryHelper.WriteValue(spec.Hive, spec.SubKey, spec.Name, value, kind);
                }
                else
                {
                    RegistryHelper.DeleteValue(spec.Hive, spec.SubKey, spec.Name);
                }
            }
            else if (spec.DeleteOnRevertWhenAbsent)
            {
                RegistryHelper.DeleteValue(spec.Hive, spec.SubKey, spec.Name);
            }
            else if (spec.DefaultValue is not null)
            {
                RegistryHelper.WriteValue(spec.Hive, spec.SubKey, spec.Name, spec.DefaultValue, spec.Kind);
            }
        }

        return Task.CompletedTask;
    }
}
