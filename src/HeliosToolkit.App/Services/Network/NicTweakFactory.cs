using System.Text.Json;
using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.App.Services.Tweaks;
using HeliosToolkit.App.Services.Tweaks.Primitives;
using HeliosToolkit.Core.Tweaks;
using Microsoft.Win32;
using Serilog;

namespace HeliosToolkit.App.Services.Network;

/// <summary>
/// Builds the three NIC latency tweaks against the adapter that owns the default
/// route. Everything detects honestly: no adapter / no knob ⇒ NotApplicable.
/// </summary>
public sealed class NicTweakFactory(ActiveAdapterService adapters)
{
    private const string NicClassRoot =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    public ITweak InterruptModerationOff(TweakMetadata meta) => new DelegateTweak(
        meta,
        detect: async ct =>
        {
            ActiveAdapter? adapter = await adapters.GetActiveAdapterAsync(ct);
            if (adapter is null)
            {
                return TweakState.NotApplicable;
            }

            string cmd = $"Get-NetAdapterAdvancedProperty -Name {ActiveAdapterService.PsQuote(adapter.Name)} " +
                         "-RegistryKeyword '*InterruptModeration' -ErrorAction SilentlyContinue | " +
                         "Select-Object -First 1 -ExpandProperty RegistryValue | ConvertTo-Json -Compress";
            ProcessResult r = await ActiveAdapterService.RunPsAsync(cmd, ct);
            string? value = FirstJsonScalar(r);
            if (value is null)
            {
                return TweakState.NotApplicable; // knob not exposed (typical for Wi-Fi)
            }

            return value == "0" ? TweakState.Applied : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            ActiveAdapter? adapter = await adapters.GetActiveAdapterAsync(ct)
                ?? throw new InvalidOperationException("No active network adapter found.");

            string read = $"Get-NetAdapterAdvancedProperty -Name {ActiveAdapterService.PsQuote(adapter.Name)} " +
                          "-RegistryKeyword '*InterruptModeration' -ErrorAction SilentlyContinue | " +
                          "Select-Object -First 1 -ExpandProperty RegistryValue | ConvertTo-Json -Compress";
            string? original = FirstJsonScalar(await ActiveAdapterService.RunPsAsync(read, ct));
            if (original is null)
            {
                throw new InvalidOperationException(
                    $"'{adapter.Name}' does not expose the InterruptModeration knob (common on Wi-Fi).");
            }

            backup.Capture(new BackupEntry
            {
                TweakId = meta.Id, Kind = "powershell",
                Target = $"{adapter.Name}|*InterruptModeration",
                Existed = true, OriginalValue = original,
            });

            string set = $"Set-NetAdapterAdvancedProperty -Name {ActiveAdapterService.PsQuote(adapter.Name)} " +
                         "-RegistryKeyword '*InterruptModeration' -RegistryValue 0";
            ProcessResult r = await ActiveAdapterService.RunPsAsync(set, ct);
            if (!r.Success)
            {
                throw new InvalidOperationException($"Set-NetAdapterAdvancedProperty failed: {r.StdErr}");
            }
        },
        revert: async (backup, ct) =>
        {
            BackupEntry? e = backup.ForTweak(meta.Id).FirstOrDefault(x => x.Kind == "powershell");
            if (e?.OriginalValue is null)
            {
                return;
            }

            (string adapterName, _) = SplitTarget(e.Target);
            string set = $"Set-NetAdapterAdvancedProperty -Name {ActiveAdapterService.PsQuote(adapterName)} " +
                         $"-RegistryKeyword '*InterruptModeration' -RegistryValue {int.Parse(e.OriginalValue)}";
            await ActiveAdapterService.RunPsAsync(set, ct);
        });

    public ITweak RscOff(TweakMetadata meta) => new DelegateTweak(
        meta,
        detect: async ct =>
        {
            ActiveAdapter? adapter = await adapters.GetActiveAdapterAsync(ct);
            if (adapter is null)
            {
                return TweakState.NotApplicable;
            }

            (bool? v4, bool? v6) = await ReadRscAsync(adapter.Name, ct);
            if (v4 is null && v6 is null)
            {
                return TweakState.NotApplicable; // adapter doesn't support RSC
            }

            bool anyOn = v4 == true || v6 == true;
            return anyOn ? TweakState.NotApplied : TweakState.Applied;
        },
        apply: async (backup, ct) =>
        {
            ActiveAdapter? adapter = await adapters.GetActiveAdapterAsync(ct)
                ?? throw new InvalidOperationException("No active network adapter found.");

            (bool? v4, bool? v6) = await ReadRscAsync(adapter.Name, ct);
            if (v4 is null && v6 is null)
            {
                throw new InvalidOperationException($"'{adapter.Name}' does not support RSC.");
            }

            backup.Capture(new BackupEntry
            {
                TweakId = meta.Id, Kind = "powershell",
                Target = $"{adapter.Name}|rsc",
                Existed = true,
                OriginalValue = $"{(v4 == true ? 1 : 0)},{(v6 == true ? 1 : 0)}",
            });

            ProcessResult r = await ActiveAdapterService.RunPsAsync(
                $"Disable-NetAdapterRsc -Name {ActiveAdapterService.PsQuote(adapter.Name)}", ct);
            if (!r.Success)
            {
                throw new InvalidOperationException($"Disable-NetAdapterRsc failed: {r.StdErr}");
            }
        },
        revert: async (backup, ct) =>
        {
            BackupEntry? e = backup.ForTweak(meta.Id).FirstOrDefault(x => x.Kind == "powershell");
            if (e?.OriginalValue is null)
            {
                return;
            }

            (string adapterName, _) = SplitTarget(e.Target);
            string[] flags = e.OriginalValue.Split(',');
            string v4 = flags.Length > 0 && flags[0] == "1" ? "$true" : "$false";
            string v6 = flags.Length > 1 && flags[1] == "1" ? "$true" : "$false";
            await ActiveAdapterService.RunPsAsync(
                $"Set-NetAdapterRsc -Name {ActiveAdapterService.PsQuote(adapterName)} -IPv4Enabled {v4} -IPv6Enabled {v6}", ct);
        });

    public ITweak PowerSavingOff(TweakMetadata meta) => new DelegateTweak(
        meta,
        detect: async ct =>
        {
            string? key = await FindNicClassKeyAsync(ct);
            if (key is null)
            {
                return TweakState.NotApplicable;
            }

            object? value = RegistryHelper.ReadValue("HKLM", key, "PnPCapabilities");
            return value is int i && (i & 0x18) == 0x18 ? TweakState.Applied : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            string key = await FindNicClassKeyAsync(ct)
                ?? throw new InvalidOperationException("Could not locate the adapter's class registry key.");

            object? current = RegistryHelper.ReadValue("HKLM", key, "PnPCapabilities");
            backup.Capture(new BackupEntry
            {
                TweakId = meta.Id, Kind = "registry",
                Target = $"HKLM\\{key}!PnPCapabilities",
                Existed = current is not null,
                OriginalValue = current is null ? null : RegistryHelper.Serialize(current, RegistryValueKind.DWord),
                ValueType = nameof(RegistryValueKind.DWord),
            });
            RegistryHelper.WriteValue("HKLM", key, "PnPCapabilities", 0x18, RegistryValueKind.DWord);
        },
        revert: async (backup, ct) =>
        {
            string? key = await FindNicClassKeyAsync(ct);
            if (key is null)
            {
                return;
            }

            BackupEntry? e = backup.ForTweak(meta.Id).FirstOrDefault(x => x.Kind == "registry");
            if (e is { Existed: true, OriginalValue: not null })
            {
                (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(e.OriginalValue);
                RegistryHelper.WriteValue("HKLM", key, "PnPCapabilities", value, kind);
            }
            else
            {
                RegistryHelper.DeleteValue("HKLM", key, "PnPCapabilities");
            }
        });

    /// <summary>The active adapter's 00NN class subkey, matched by NetCfgInstanceId == InterfaceGuid.</summary>
    private async Task<string?> FindNicClassKeyAsync(CancellationToken ct)
    {
        ActiveAdapter? adapter = await adapters.GetActiveAdapterAsync(ct);
        if (adapter is null || adapter.InterfaceGuid.Length == 0)
        {
            return null;
        }

        foreach (string sub in RegistryHelper.SubKeyNames("HKLM", NicClassRoot))
        {
            if (sub.Length != 4 || !int.TryParse(sub, out _))
            {
                continue;
            }

            string path = $@"{NicClassRoot}\{sub}";
            object? instanceId = RegistryHelper.ReadValue("HKLM", path, "NetCfgInstanceId");
            if (instanceId?.ToString()?.Equals(adapter.InterfaceGuid, StringComparison.OrdinalIgnoreCase) == true)
            {
                return path;
            }
        }

        Log.Warning("No class key for adapter {Guid}", adapter.InterfaceGuid);
        return null;
    }

    private static async Task<(bool? V4, bool? V6)> ReadRscAsync(string adapterName, CancellationToken ct)
    {
        string cmd = $"Get-NetAdapterRsc -Name {ActiveAdapterService.PsQuote(adapterName)} -ErrorAction SilentlyContinue | " +
                     "Select-Object IPv4Enabled, IPv6Enabled | ConvertTo-Json -Compress";
        ProcessResult r = await ActiveAdapterService.RunPsAsync(cmd, ct);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
        {
            return (null, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(r.StdOut);
            JsonElement root = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement[0]
                : doc.RootElement;
            bool? v4 = root.TryGetProperty("IPv4Enabled", out JsonElement e4) && e4.ValueKind != JsonValueKind.Null
                ? e4.GetBoolean() : null;
            bool? v6 = root.TryGetProperty("IPv6Enabled", out JsonElement e6) && e6.ValueKind != JsonValueKind.Null
                ? e6.GetBoolean() : null;
            return (v4, v6);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>Reads a single scalar (string/number) out of a PS ConvertTo-Json result.</summary>
    private static string? FirstJsonScalar(ProcessResult result)
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(result.StdOut);
            JsonElement root = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                ? doc.RootElement[0]
                : doc.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Number => root.GetRawText(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string AdapterName, string Knob) SplitTarget(string target)
    {
        int bar = target.IndexOf('|');
        return bar < 0 ? (target, "") : (target[..bar], target[(bar + 1)..]);
    }
}
