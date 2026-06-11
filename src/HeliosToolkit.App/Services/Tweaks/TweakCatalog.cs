using HeliosToolkit.App.Services.Hardware;
using HeliosToolkit.App.Services.Safety;
using HeliosToolkit.App.Services.System;
using HeliosToolkit.App.Services.Tweaks.Primitives;
using HeliosToolkit.Core.Tweaks;
using Microsoft.Win32;

namespace HeliosToolkit.App.Services.Tweaks;

/// <summary>
/// Builds the concrete <see cref="ITweak"/> for every entry in the Core metadata
/// catalog. Construction fails fast if metadata and implementations ever drift.
/// </summary>
public sealed class TweakCatalog
{
    private readonly Dictionary<string, ITweak> _tweaks;

    public TweakCatalog(SystemInfoService systemInfo, TimerResolutionService timerResolution)
    {
        var list = new List<ITweak>
        {
            // ───── NVIDIA ─────
            Hags(),
            MsiMode(systemInfo),
            DynamicPstate(),
            Mpo(),
            new ScheduledTaskTweak(Meta("nv-telemetry-off"),
                    @"\NvTmMon*", @"\NvTmRep*", @"\NvProfileUpdater*", "NvTm*", "NvProfileUpdater*")
                .Combine(new ServiceStartupTweak(Meta("nv-telemetry-off"), "NvTelemetryContainer")),

            // ───── Windows: gaming features ─────
            GameDvrOff(),
            GameModeOn(),
            FsoGlobal(),
            NotificationsQuiet(),

            // ───── Windows: power ─────
            UltimatePerformance(),
            BoostAggressive(),
            CoreParkingOff(),
            UsbSuspendOff(),
            PcieAspmOff(),
            HibernateOff(),

            // ───── Windows: input ─────
            MouseAccelOff(),
            InputQueues(),
            TimerHold(timerResolution),

            // ───── Windows: scheduling / mmcss / net ─────
            PrioSeparation(),
            NetThrottling(),
            MmcssGames(),
            TcpNoDelay(),

            // ───── Windows: services / visuals / advanced ─────
            new ServiceStartupTweak(Meta("sysmain-off"), "SysMain"),
            TelemetryLeftovers(),
            VisualFxPerf(),
            BcdClockDefaults(),
            HvciOff(),
            IntelApoInfo(),
        };

        _tweaks = list.ToDictionary(t => t.Meta.Id, StringComparer.Ordinal);

        // Guard: every metadata entry must have exactly one implementation.
        IEnumerable<string> metaIds = TweakCatalogMetadata.All.Select(m => m.Id);
        var missing = metaIds.Where(id => !_tweaks.ContainsKey(id)).ToList();
        var extra = _tweaks.Keys.Where(id => TweakCatalogMetadata.All.All(m => m.Id != id)).ToList();
        if (missing.Count > 0 || extra.Count > 0)
        {
            throw new InvalidOperationException(
                $"Tweak catalog/metadata mismatch. Missing impls: [{string.Join(", ", missing)}]. " +
                $"Orphan impls: [{string.Join(", ", extra)}].");
        }
    }

    public IReadOnlyCollection<ITweak> All => _tweaks.Values;

    public IEnumerable<ITweak> ForPage(TweakPage page) => _tweaks.Values.Where(t => t.Meta.Page == page);

    public ITweak Get(string id) => _tweaks[id];

    private static TweakMetadata Meta(string id) =>
        TweakCatalogMetadata.All.First(m => m.Id == id);

    // ───────────────────────── NVIDIA ─────────────────────────

    private static ITweak Hags() => new RegistryValueTweak(
        Meta("hags-on"),
        new RegistryValueSpec
        {
            Hive = "HKLM",
            SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
            Name = "HwSchMode",
            AppliedValue = 2,
            Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false,
            DefaultValue = 1,
        });

    private static ITweak MsiMode(SystemInfoService systemInfo) => new DelegateTweak(
        Meta("nv-msi-mode"),
        detect: async ct =>
        {
            string? key = await MsiKeyAsync(systemInfo);
            if (key is null)
            {
                return TweakState.NotApplicable;
            }

            object? v = RegistryHelper.ReadValue("HKLM", key, "MSISupported");
            return v is int i && i == 1 ? TweakState.Applied : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            string? key = await MsiKeyAsync(systemInfo);
            if (key is null)
            {
                return;
            }

            object? current = RegistryHelper.ReadValue("HKLM", key, "MSISupported");
            backup.Capture(new BackupEntry
            {
                TweakId = "nv-msi-mode",
                Kind = "registry",
                Target = $"HKLM\\{key}!MSISupported",
                Existed = current is not null,
                OriginalValue = current is null ? null : RegistryHelper.Serialize(current, RegistryValueKind.DWord),
                ValueType = nameof(RegistryValueKind.DWord),
            });
            RegistryHelper.WriteValue("HKLM", key, "MSISupported", 1, RegistryValueKind.DWord);
        },
        revert: async (backup, ct) =>
        {
            string? key = await MsiKeyAsync(systemInfo);
            if (key is null)
            {
                return;
            }

            BackupEntry? e = backup.ForTweak("nv-msi-mode").FirstOrDefault(x => x.Kind == "registry");
            if (e is { Existed: true, OriginalValue: not null })
            {
                (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(e.OriginalValue);
                RegistryHelper.WriteValue("HKLM", key, "MSISupported", value, kind);
            }
            else
            {
                RegistryHelper.DeleteValue("HKLM", key, "MSISupported");
            }
        });

    private static async Task<string?> MsiKeyAsync(SystemInfoService systemInfo)
    {
        SystemSnapshot snapshot = await systemInfo.GetSnapshotAsync();
        if (string.IsNullOrEmpty(snapshot.GpuPnpDeviceId))
        {
            return null;
        }

        string key = $@"SYSTEM\CurrentControlSet\Enum\{snapshot.GpuPnpDeviceId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
        return key;
    }

    private static ITweak DynamicPstate() => new DelegateTweak(
        Meta("nv-dynamic-pstate"),
        detect: ct =>
        {
            string? key = NvidiaAdapterKey.Find();
            if (key is null)
            {
                return Task.FromResult(TweakState.NotApplicable);
            }

            object? v = RegistryHelper.ReadValue("HKLM", key, "DisableDynamicPstate");
            return Task.FromResult(v is int i && i == 1 ? TweakState.Applied : TweakState.NotApplied);
        },
        apply: (backup, ct) =>
        {
            string? key = NvidiaAdapterKey.Find();
            if (key is null)
            {
                return Task.CompletedTask;
            }

            object? current = RegistryHelper.ReadValue("HKLM", key, "DisableDynamicPstate");
            backup.Capture(new BackupEntry
            {
                TweakId = "nv-dynamic-pstate",
                Kind = "registry",
                Target = $"HKLM\\{key}!DisableDynamicPstate",
                Existed = current is not null,
                OriginalValue = current is null ? null : RegistryHelper.Serialize(current, RegistryValueKind.DWord),
                ValueType = nameof(RegistryValueKind.DWord),
            });
            RegistryHelper.WriteValue("HKLM", key, "DisableDynamicPstate", 1, RegistryValueKind.DWord);
            return Task.CompletedTask;
        },
        revert: (backup, ct) =>
        {
            string? key = NvidiaAdapterKey.Find();
            if (key is null)
            {
                return Task.CompletedTask;
            }

            BackupEntry? e = backup.ForTweak("nv-dynamic-pstate").FirstOrDefault(x => x.Kind == "registry");
            if (e is { Existed: true, OriginalValue: not null })
            {
                (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(e.OriginalValue);
                RegistryHelper.WriteValue("HKLM", key, "DisableDynamicPstate", value, kind);
            }
            else
            {
                RegistryHelper.DeleteValue("HKLM", key, "DisableDynamicPstate");
            }

            return Task.CompletedTask;
        });

    private static ITweak Mpo() => new RegistryValueTweak(
        Meta("mpo-off"),
        new RegistryValueSpec
        {
            Hive = "HKLM",
            SubKey = @"SOFTWARE\Microsoft\Windows\Dwm",
            Name = "OverlayTestMode",
            AppliedValue = 5,
            Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = true,
        });

    // ───────────────────────── Windows: gaming ─────────────────────────

    private static ITweak GameDvrOff() => new RegistryValueTweak(
        Meta("gamedvr-off"),
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"System\GameConfigStore", Name = "GameDVR_Enabled",
            AppliedValue = 0, Kind = RegistryValueKind.DWord, DeleteOnRevertWhenAbsent = false, DefaultValue = 1,
        },
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", Name = "AppCaptureEnabled",
            AppliedValue = 0, Kind = RegistryValueKind.DWord, DeleteOnRevertWhenAbsent = false, DefaultValue = 1,
        });

    private static ITweak GameModeOn() => new RegistryValueTweak(
        Meta("gamemode-on"),
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"SOFTWARE\Microsoft\GameBar", Name = "AutoGameModeEnabled",
            AppliedValue = 1, Kind = RegistryValueKind.DWord, DeleteOnRevertWhenAbsent = false, DefaultValue = 1,
        });

    private static ITweak FsoGlobal() => new RegistryValueTweak(
        Meta("fso-global"),
        Spec("HKCU", @"System\GameConfigStore", "GameDVR_FSEBehavior", 2),
        Spec("HKCU", @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2),
        Spec("HKCU", @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1),
        Spec("HKCU", @"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1));

    private static ITweak NotificationsQuiet() => new RegistryValueTweak(
        Meta("notifications-quiet"),
        new RegistryValueSpec
        {
            Hive = "HKCU",
            SubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PushNotifications",
            Name = "ToastEnabled",
            AppliedValue = 0,
            Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false,
            DefaultValue = 1,
        });

    // ───────────────────────── Windows: power ─────────────────────────

    private static ITweak UltimatePerformance() => new DelegateTweak(
        Meta("power-ultimate"),
        detect: async ct =>
        {
            string? active = await PowerCfg.GetActiveSchemeGuidAsync(ct);
            object? stored = RegistryHelper.ReadValue("HKCU", HeliosStateKey, "UltimateSchemeGuid");
            return stored is string g && g.Equals(active, StringComparison.OrdinalIgnoreCase)
                ? TweakState.Applied
                : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            string? previous = await PowerCfg.GetActiveSchemeGuidAsync(ct);
            backup.Capture(new BackupEntry
            {
                TweakId = "power-ultimate", Kind = "powercfg", Target = "active-scheme",
                Existed = previous is not null, OriginalValue = previous,
            });

            // Reuse our duplicate if it already exists, else create one.
            object? existing = RegistryHelper.ReadValue("HKCU", HeliosStateKey, "UltimateSchemeGuid");
            string? guid = existing as string;
            if (guid is null || !(await PowerCfg.ListSchemeGuidsAsync(ct)).Contains(guid, StringComparer.OrdinalIgnoreCase))
            {
                guid = await PowerCfg.DuplicateSchemeAsync(PowerCfg.UltimatePerformanceTemplate, ct);
                if (guid is not null)
                {
                    RegistryHelper.WriteValue("HKCU", HeliosStateKey, "UltimateSchemeGuid", guid, RegistryValueKind.String);
                }
            }

            if (guid is not null)
            {
                await PowerCfg.SetActiveAsync(guid, ct);
            }
        },
        revert: async (backup, ct) =>
        {
            BackupEntry? e = backup.ForTweak("power-ultimate").FirstOrDefault(x => x.Kind == "powercfg");
            if (e?.OriginalValue is { Length: > 0 } previous)
            {
                await PowerCfg.SetActiveAsync(previous, ct);
            }
        });

    private static ITweak BoostAggressive() => PowerSetting(
        "boost-aggressive",
        subgroup: "54533251-82be-4824-96c1-47b60b740d00",
        setting: "be337238-0d82-4146-a960-4f3749d470c7",
        appliedValue: 2,
        defaultValue: 3); // 3 = Aggressive At Guaranteed (Windows default varies; restore captured original)

    private static ITweak CoreParkingOff() => new DelegateTweak(
        Meta("core-parking-off"),
        detect: async ct =>
        {
            long? min1 = await PowerCfg.GetAcValueIndexAsync(
                "scheme_current", "54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", ct);
            return min1 == 100 ? TweakState.Applied : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            await CapturePowerAsync(backup, "core-parking-off",
                "0cc5b647-c1df-4637-891a-dec35c318583", "54533251-82be-4824-96c1-47b60b740d00", ct);
            await PowerCfg.SetAcValueIndexAsync("scheme_current",
                "54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", 100, ct);
            await PowerCfg.SetActiveAsync("scheme_current", ct);
        },
        revert: (backup, ct) => RevertPowerAsync(backup, "core-parking-off",
            "54533251-82be-4824-96c1-47b60b740d00", "0cc5b647-c1df-4637-891a-dec35c318583", 100, ct));

    private static ITweak UsbSuspendOff() => PowerSetting(
        "usb-suspend-off",
        subgroup: "2a737441-1930-4402-8d77-b2bebba308a3",
        setting: "48e6b7a6-50f5-4782-a5d4-53bb3f3b7dc8",
        appliedValue: 0,
        defaultValue: 1);

    private static ITweak PcieAspmOff() => PowerSetting(
        "pcie-aspm-off",
        subgroup: "501a4d13-42af-4429-9fd1-a8218c268e20",
        setting: "ee12f906-d277-404b-b6da-e5fa1a576df5",
        appliedValue: 0,
        defaultValue: 2);

    private static ITweak HibernateOff() => new DelegateTweak(
        Meta("hibernate-off"),
        detect: async ct => await PowerCfg.IsHibernateEnabledAsync(ct) ? TweakState.NotApplied : TweakState.Applied,
        apply: async (backup, ct) =>
        {
            bool wasOn = await PowerCfg.IsHibernateEnabledAsync(ct);
            backup.Capture(new BackupEntry
            {
                TweakId = "hibernate-off", Kind = "powercfg", Target = "hibernate",
                Existed = true, OriginalValue = wasOn ? "on" : "off",
            });
            await PowerCfg.SetHibernateAsync(false, ct);
        },
        revert: async (backup, ct) =>
        {
            BackupEntry? e = backup.ForTweak("hibernate-off").FirstOrDefault(x => x.Kind == "powercfg");
            if (e?.OriginalValue == "on")
            {
                await PowerCfg.SetHibernateAsync(true, ct);
            }
        });

    // ───────────────────────── Windows: input ─────────────────────────

    private static ITweak MouseAccelOff() => new SystemParametersTweak(
        Meta("mouse-accel-off"),
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"Control Panel\Mouse", Name = "MouseSpeed",
            AppliedValue = "0", Kind = RegistryValueKind.String, DeleteOnRevertWhenAbsent = false, DefaultValue = "1",
        },
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"Control Panel\Mouse", Name = "MouseThreshold1",
            AppliedValue = "0", Kind = RegistryValueKind.String, DeleteOnRevertWhenAbsent = false, DefaultValue = "6",
        },
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"Control Panel\Mouse", Name = "MouseThreshold2",
            AppliedValue = "0", Kind = RegistryValueKind.String, DeleteOnRevertWhenAbsent = false, DefaultValue = "10",
        });

    private static ITweak InputQueues() => new RegistryValueTweak(
        Meta("input-queues"),
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters",
            Name = "KeyboardDataQueueSize", AppliedValue = 20, Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = true,
        },
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters",
            Name = "MouseDataQueueSize", AppliedValue = 20, Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = true,
        });

    private static ITweak TimerHold(TimerResolutionService timer) => new DelegateTweak(
        Meta("timer-res-hold"),
        detect: ct => Task.FromResult(timer.IsHolding ? TweakState.Applied : TweakState.NotApplied),
        apply: (backup, ct) =>
        {
            timer.Start();
            return Task.CompletedTask;
        },
        revert: (backup, ct) =>
        {
            timer.Stop();
            return Task.CompletedTask;
        });

    // ───────────────────────── Windows: scheduling / net ─────────────────────────

    private static ITweak PrioSeparation() => new RegistryValueTweak(
        Meta("prio-separation"),
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl",
            Name = "Win32PrioritySeparation", AppliedValue = 0x26, Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false, DefaultValue = 2,
        });

    private static ITweak NetThrottling() => new RegistryValueTweak(
        Meta("net-throttling"),
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            Name = "NetworkThrottlingIndex", AppliedValue = unchecked((int)0xFFFFFFFF), Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false, DefaultValue = 10,
        },
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            Name = "SystemResponsiveness", AppliedValue = 10, Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false, DefaultValue = 20,
        });

    private static ITweak MmcssGames() => new RegistryValueTweak(
        Meta("mmcss-games"),
        Spec("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8),
        Spec("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6),
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
            Name = "Scheduling Category", AppliedValue = "High", Kind = RegistryValueKind.String,
            DeleteOnRevertWhenAbsent = false, DefaultValue = "Medium",
        },
        new RegistryValueSpec
        {
            Hive = "HKLM", SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
            Name = "SFIO Priority", AppliedValue = "High", Kind = RegistryValueKind.String,
            DeleteOnRevertWhenAbsent = false, DefaultValue = "Normal",
        });

    private static ITweak TcpNoDelay() => new DelegateTweak(
        Meta("tcp-nodelay"),
        detect: ct =>
        {
            string? iface = ActiveNicInterfaceKey();
            if (iface is null)
            {
                return Task.FromResult(TweakState.NotApplicable);
            }

            object? ack = RegistryHelper.ReadValue("HKLM", iface, "TcpAckFrequency");
            object? nodelay = RegistryHelper.ReadValue("HKLM", iface, "TCPNoDelay");
            bool applied = ack is int a && a == 1 && nodelay is int n && n == 1;
            return Task.FromResult(applied ? TweakState.Applied : TweakState.NotApplied);
        },
        apply: (backup, ct) =>
        {
            string? iface = ActiveNicInterfaceKey();
            if (iface is null)
            {
                return Task.CompletedTask;
            }

            foreach (string name in new[] { "TcpAckFrequency", "TCPNoDelay" })
            {
                object? current = RegistryHelper.ReadValue("HKLM", iface, name);
                backup.Capture(new BackupEntry
                {
                    TweakId = "tcp-nodelay", Kind = "registry", Target = $"HKLM\\{iface}!{name}",
                    Existed = current is not null,
                    OriginalValue = current is null ? null : RegistryHelper.Serialize(current, RegistryValueKind.DWord),
                    ValueType = nameof(RegistryValueKind.DWord),
                });
                RegistryHelper.WriteValue("HKLM", iface, name, 1, RegistryValueKind.DWord);
            }

            return Task.CompletedTask;
        },
        revert: (backup, ct) =>
        {
            foreach (BackupEntry e in backup.ForTweak("tcp-nodelay").Where(x => x.Kind == "registry"))
            {
                (string hive, string sub, string name) = SplitTarget(e.Target);
                if (e.Existed && e.OriginalValue is not null)
                {
                    (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(e.OriginalValue);
                    RegistryHelper.WriteValue(hive, sub, name, value, kind);
                }
                else
                {
                    RegistryHelper.DeleteValue(hive, sub, name);
                }
            }

            return Task.CompletedTask;
        });

    // ───────────────────────── Windows: services / visuals / advanced ─────────────────────────

    private static ITweak TelemetryLeftovers() => new ServiceStartupTweak(Meta("telemetry-leftovers"), "DiagTrack")
        .Combine(new ScheduledTaskTweak(Meta("telemetry-leftovers"),
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"));

    private static ITweak VisualFxPerf() => new RegistryValueTweak(
        Meta("visualfx-perf"),
        new RegistryValueSpec
        {
            Hive = "HKCU", SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            Name = "VisualFXSetting", AppliedValue = 2, Kind = RegistryValueKind.DWord,
            DeleteOnRevertWhenAbsent = false, DefaultValue = 0,
        });

    private static ITweak BcdClockDefaults() => new DelegateTweak(
        Meta("bcd-clock-defaults"),
        detect: async ct =>
        {
            ProcessResult r = await ProcessRunner.RunAsync("bcdedit", "/enum {current}", ct);
            bool dynamicTickOff = r.StdOut.Contains("disabledynamictick", StringComparison.OrdinalIgnoreCase)
                && r.StdOut.Contains("Yes", StringComparison.OrdinalIgnoreCase);
            bool platformClock = r.StdOut.Contains("useplatformclock", StringComparison.OrdinalIgnoreCase);
            return dynamicTickOff || platformClock ? TweakState.Applied : TweakState.NotApplied;
        },
        apply: async (backup, ct) =>
        {
            backup.Capture(new BackupEntry
            {
                TweakId = "bcd-clock-defaults", Kind = "bcd", Target = "clock-elements",
                Existed = true, OriginalValue = "default",
            });
            await ProcessRunner.RunAsync("bcdedit", "/set disabledynamictick yes", ct);
            await ProcessRunner.RunAsync("bcdedit", "/set useplatformclock false", ct);
        },
        revert: async (backup, ct) =>
        {
            await ProcessRunner.RunAsync("bcdedit", "/deletevalue useplatformclock", ct);
            await ProcessRunner.RunAsync("bcdedit", "/deletevalue disabledynamictick", ct);
            await ProcessRunner.RunAsync("bcdedit", "/deletevalue tscsyncpolicy", ct);
        });

    private static ITweak HvciOff() => new DelegateTweak(
        Meta("hvci-off"),
        detect: ct =>
        {
            object? v = RegistryHelper.ReadValue(
                "HKLM",
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                "Enabled");
            // Applied (off) when explicitly 0; otherwise treat as on/default.
            return Task.FromResult(v is int i && i == 0 ? TweakState.Applied : TweakState.NotApplied);
        },
        apply: (backup, ct) =>
        {
            const string sub = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
            object? current = RegistryHelper.ReadValue("HKLM", sub, "Enabled");
            backup.Capture(new BackupEntry
            {
                TweakId = "hvci-off", Kind = "registry", Target = $"HKLM\\{sub}!Enabled",
                Existed = current is not null,
                OriginalValue = current is null ? null : RegistryHelper.Serialize(current, RegistryValueKind.DWord),
                ValueType = nameof(RegistryValueKind.DWord),
            });
            RegistryHelper.WriteValue("HKLM", sub, "Enabled", 0, RegistryValueKind.DWord);
            return Task.CompletedTask;
        },
        revert: (backup, ct) =>
        {
            const string sub = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
            BackupEntry? e = backup.ForTweak("hvci-off").FirstOrDefault(x => x.Kind == "registry");
            if (e is { Existed: true, OriginalValue: not null })
            {
                (object value, RegistryValueKind kind) = RegistryHelper.Deserialize(e.OriginalValue);
                RegistryHelper.WriteValue("HKLM", sub, "Enabled", value, kind);
            }
            else
            {
                RegistryHelper.WriteValue("HKLM", sub, "Enabled", 1, RegistryValueKind.DWord);
            }

            return Task.CompletedTask;
        });

    private static IInfoTweak IntelApoInfo() => new InfoTweak(
        Meta("intel-apo-info"),
        linkUrl: "https://www.intel.com/content/www/us/en/download/820190/intel-application-optimization.html",
        detect: ct =>
        {
            bool present =
                RegistryHelper.SubKeyExists("HKLM", @"SYSTEM\CurrentControlSet\Services\intelapo") ||
                RegistryHelper.SubKeyExists("HKLM", @"SYSTEM\CurrentControlSet\Services\Intel(R) Application Optimization");
            return Task.FromResult(present ? TweakState.Applied : TweakState.NotApplicable);
        });

    // ───────────────────────── helpers ─────────────────────────

    private const string HeliosStateKey = @"SOFTWARE\HeliosToolkit";

    private static RegistryValueSpec Spec(string hive, string sub, string name, int value) => new()
    {
        Hive = hive, SubKey = sub, Name = name, AppliedValue = value, Kind = RegistryValueKind.DWord,
        DeleteOnRevertWhenAbsent = true,
    };

    private static ITweak PowerSetting(string id, string subgroup, string setting, long appliedValue, long defaultValue) =>
        new DelegateTweak(
            Meta(id),
            detect: async ct =>
            {
                long? value = await PowerCfg.GetAcValueIndexAsync("scheme_current", subgroup, setting, ct);
                return value == appliedValue ? TweakState.Applied : TweakState.NotApplied;
            },
            apply: async (backup, ct) =>
            {
                await CapturePowerAsync(backup, id, setting, subgroup, ct);
                await PowerCfg.SetAcValueIndexAsync("scheme_current", subgroup, setting, appliedValue, ct);
                await PowerCfg.SetActiveAsync("scheme_current", ct);
            },
            revert: (backup, ct) => RevertPowerAsync(backup, id, subgroup, setting, defaultValue, ct));

    private static async Task CapturePowerAsync(
        IBackupSink backup, string id, string setting, string subgroup, CancellationToken ct)
    {
        long? original = await PowerCfg.GetAcValueIndexAsync("scheme_current", subgroup, setting, ct);
        backup.Capture(new BackupEntry
        {
            TweakId = id, Kind = "powercfg", Target = $"{subgroup}/{setting}",
            Existed = original is not null, OriginalValue = original?.ToString(),
        });
    }

    private static async Task RevertPowerAsync(
        IBackupSource backup, string id, string subgroup, string setting, long fallback, CancellationToken ct)
    {
        BackupEntry? e = backup.ForTweak(id).FirstOrDefault(x => x.Kind == "powercfg");
        long value = e?.OriginalValue is not null && long.TryParse(e.OriginalValue, out long original)
            ? original
            : fallback;
        await PowerCfg.SetAcValueIndexAsync("scheme_current", subgroup, setting, value, ct);
        await PowerCfg.SetActiveAsync("scheme_current", ct);
    }

    private static string? ActiveNicInterfaceKey()
    {
        // Pick the first interface that has a default gateway configured.
        const string interfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        foreach (string sub in RegistryHelper.SubKeyNames("HKLM", interfaces))
        {
            string path = $@"{interfaces}\{sub}";
            object? gw = RegistryHelper.ReadValue("HKLM", path, "DefaultGateway")
                ?? RegistryHelper.ReadValue("HKLM", path, "DhcpDefaultGateway");
            if (gw is string s && s.Length > 0)
            {
                return path;
            }

            if (gw is string[] arr && arr.Any(x => x.Length > 0))
            {
                return path;
            }
        }

        return null;
    }

    private static (string Hive, string SubKey, string Name) SplitTarget(string target)
    {
        int bang = target.LastIndexOf('!');
        string path = target[..bang];
        string name = target[(bang + 1)..];
        int slash = path.IndexOf('\\');
        return (path[..slash], path[(slash + 1)..], name);
    }
}
