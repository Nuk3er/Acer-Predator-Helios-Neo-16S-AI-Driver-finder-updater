namespace HeliosToolkit.Core.Tweaks;

/// <summary>
/// The complete, honest tweak table. Descriptions say exactly what is touched and
/// when a tweak is NOT worth it — that honesty is the point of this app.
/// The Windows-only implementations are bound to these ids in the App project,
/// which fails fast at startup if the two ever drift apart.
/// </summary>
public static class TweakCatalogMetadata
{
    public static readonly IReadOnlyList<TweakMetadata> All = new TweakMetadata[]
    {
        // ───────────────────────── NVIDIA page ─────────────────────────
        new()
        {
            Id = "hags-on",
            Name = "Hardware-accelerated GPU scheduling (HAGS)",
            Page = TweakPage.Nvidia,
            Category = "GPU driver behavior",
            Risk = RiskLevel.Safe,
            RequiresReboot = true,
            Description =
                "Lets the GPU manage its own command queues instead of round-tripping through the CPU. " +
                "Slightly lower input latency and it is required for DLSS Frame Generation. " +
                "Recommended ON for an RTX 50 laptop. Sets HKLM\\...\\GraphicsDrivers!HwSchMode = 2.",
        },
        new()
        {
            Id = "nv-msi-mode",
            Name = "MSI interrupt mode for the GPU",
            Page = TweakPage.Nvidia,
            Category = "GPU driver behavior",
            Risk = RiskLevel.Situational,
            RequiresReboot = true,
            Description =
                "Message-Signaled Interrupts avoid shared legacy interrupt lines. Modern NVIDIA drivers " +
                "already enable this on RTX cards, so the toggle usually just confirms it — turning it on " +
                "only helps if something forced line-based interrupts. Sets MSISupported = 1 under the " +
                "GPU's Interrupt Management key.",
        },
        new()
        {
            Id = "nv-dynamic-pstate",
            Name = "Disable dynamic P-states (lock GPU clocks high)",
            Page = TweakPage.Nvidia,
            Category = "GPU driver behavior",
            Risk = RiskLevel.Risky,
            RequiresReboot = true,
            Description =
                "Stops the GPU from downclocking at light load, which removes clock-ramp stutter in a few " +
                "edge cases. Sets DisableDynamicPstate = 1 in the NVIDIA adapter class key.",
            Warning =
                "On a laptop this means the GPU never idles: noticeably more heat, fan noise and battery " +
                "drain even on the desktop, and Dynamic Boost has less headroom to shift power to the CPU. " +
                "Most people should leave this OFF — try 'Prefer maximum performance' in the driver profile first.",
        },
        new()
        {
            Id = "mpo-off",
            Name = "Disable Multi-Plane Overlay (MPO)",
            Page = TweakPage.Nvidia,
            Category = "GPU driver behavior",
            Risk = RiskLevel.Situational,
            RequiresReboot = true,
            RebootNote = "Sign out and back in",
            Description =
                "MPO lets Windows compose some windows directly in the display hardware. Years ago it caused " +
                "flicker/stutter on NVIDIA and the classic fix was to disable it. On Windows 11 24H2 with " +
                "current drivers it mostly behaves, and disabling it can break smooth windowed HDR/VRR. " +
                "Only try this if you see desktop flicker or windowed-game stutter. Sets " +
                "HKLM\\SOFTWARE\\Microsoft\\Windows\\Dwm!OverlayTestMode = 5.",
        },
        new()
        {
            Id = "nv-telemetry-off",
            Name = "Disable NVIDIA telemetry tasks & service",
            Page = TweakPage.Nvidia,
            Category = "Telemetry & services",
            Risk = RiskLevel.Safe,
            Description =
                "Disables the NvTmMon/NvTmRep/NvProfileUpdater scheduled tasks and the NvTelemetryContainer " +
                "service if present. Drivers installed clean (or debloated with this app) usually don't have " +
                "them — then this shows 'Not applicable'. No performance cost either way; it's about privacy " +
                "and background noise.",
        },

        // ───────────────────────── Windows page ─────────────────────────
        new()
        {
            Id = "gamedvr-off",
            Name = "Game DVR / background recording off",
            Page = TweakPage.Windows,
            Category = "Gaming features",
            Risk = RiskLevel.Safe,
            Description =
                "Stops the Xbox Game Bar capture pipeline from hooking games and recording in the background. " +
                "One of the few tweaks with a broad consensus: small but real win, zero downside if you don't " +
                "use Game Bar capture. Sets GameDVR_Enabled = 0 and AppCaptureEnabled = 0 under HKCU.",
        },
        new()
        {
            Id = "gamemode-on",
            Name = "Windows Game Mode on",
            Page = TweakPage.Windows,
            Category = "Gaming features",
            Risk = RiskLevel.Safe,
            Description =
                "Game Mode keeps Windows Update and driver installs from interrupting a running game and gives " +
                "the game scheduling priority. On modern Windows 11 it should stay ON — turning it off is an " +
                "outdated tip. Sets HKCU\\Software\\Microsoft\\GameBar!AutoGameModeEnabled = 1 (the default).",
        },
        new()
        {
            Id = "fso-global",
            Name = "Disable fullscreen optimizations (global)",
            Page = TweakPage.Windows,
            Category = "Gaming features",
            Risk = RiskLevel.Situational,
            Description =
                "Forces classic exclusive-fullscreen behavior instead of Windows' optimized borderless layer. " +
                "On Windows 11 24H2 the optimized path is usually equal or better and is required for Auto HDR, " +
                "so the modern advice is to leave this alone — it exists here for the cases (old games, overlay " +
                "conflicts) where classic fullscreen still wins. Sets the GameDVR_FSE* values in HKCU\\System\\GameConfigStore.",
        },
        new()
        {
            Id = "notifications-quiet",
            Name = "Disable toast notifications",
            Page = TweakPage.Windows,
            Category = "Gaming features",
            Risk = RiskLevel.Safe,
            Description =
                "Kills toast pop-ups globally (HKCU\\...\\PushNotifications!ToastEnabled = 0). Windows 11 already " +
                "suppresses notifications while a fullscreen game runs via Do Not Disturb — check " +
                "Settings → System → Notifications if you only want the automatic game rule.",
        },

        new()
        {
            Id = "power-ultimate",
            Name = "Ultimate Performance power plan",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Situational,
            Description =
                "Creates (once) and activates a copy of Microsoft's hidden Ultimate Performance scheme, named " +
                "'Helios Ultimate Performance'. Minimizes latency from power management micro-decisions. On a " +
                "laptop, use it plugged in — on battery it eats runtime for near-zero gain. Your previous plan " +
                "is remembered and restored on revert.",
        },
        new()
        {
            Id = "boost-aggressive",
            Name = "CPU boost mode: Aggressive (AC)",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Situational,
            Description =
                "Tells the processor to boost immediately and hold boost clocks more eagerly when plugged in. " +
                "The 275HX runs hot under all-core boost — expect more fan noise; thermals, not this setting, " +
                "are usually the limit. powercfg PERFBOOSTMODE = 2 on the active plan (AC only).",
        },
        new()
        {
            Id = "core-parking-off",
            Name = "Disable core parking (AC)",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Situational,
            Description =
                "Keeps all cores unparked so threads never wait for a parked core to wake (sets CPMINCORES to " +
                "100% for both core classes on AC). Can shave latency spikes in CPU-bound games; costs idle " +
                "power. Windows 11's scheduler is already decent at this on hybrid CPUs — measure before/after.",
        },
        new()
        {
            Id = "usb-suspend-off",
            Name = "USB selective suspend off (AC)",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Safe,
            Description =
                "Stops Windows from power-suspending USB devices while plugged in — the classic fix for mice " +
                "that hitch after idling and audio interfaces that crackle. No downside on AC power.",
        },
        new()
        {
            Id = "pcie-aspm-off",
            Name = "PCIe link power management off (AC)",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Situational,
            Description =
                "Disables ASPM so the PCIe links to the GPU and NVMe never drop into low-power states while " +
                "plugged in. Removes rare wake-from-L1 hitches; slightly higher idle power. Pointless on battery.",
        },
        new()
        {
            Id = "hibernate-off",
            Name = "Hibernation & Fast Startup off",
            Page = TweakPage.Windows,
            Category = "Power",
            Risk = RiskLevel.Situational,
            Description =
                "Runs 'powercfg /h off'. Frees the hiberfil.sys disk space and disables Fast Startup, which is a " +
                "known source of weird driver state after 'shutdown' (a Fast-Startup boot is really a resume). " +
                "Trade-off: cold boots only, no hibernate on low battery.",
        },

        new()
        {
            Id = "mouse-accel-off",
            Name = "Mouse acceleration off (Enhance Pointer Precision)",
            Page = TweakPage.Windows,
            Category = "Input",
            Risk = RiskLevel.Safe,
            Description =
                "Disables Windows pointer acceleration so the same hand movement always moves the cursor the " +
                "same distance — essential for consistent aim, and the first thing any FPS player should check. " +
                "Games using raw input aren't affected either way. Sets MouseSpeed/MouseThreshold1/2 = 0 and " +
                "applies live via SystemParametersInfo.",
        },
        new()
        {
            Id = "input-queues",
            Name = "Smaller keyboard/mouse data queues",
            Page = TweakPage.Windows,
            Category = "Input",
            Risk = RiskLevel.Situational,
            RequiresReboot = true,
            Description =
                "Shrinks the kbdclass/mouclass buffer from 100 to 20 packets. A popular 'latency' tweak with no " +
                "credible measurement behind it — the queue only fills when the system is already stalling. " +
                "Included because it's harmless and you asked for everything; expect nothing.",
        },
        new()
        {
            Id = "timer-res-hold",
            Name = "Hold 0.5 ms timer resolution while the app runs",
            Page = TweakPage.Windows,
            Category = "Input",
            Risk = RiskLevel.Safe,
            Description =
                "Requests a 0.5 ms system timer (NtSetTimerResolution) and opts this process out of timer " +
                "coalescing, for snappier frame pacing in games that don't raise the timer themselves. On " +
                "Windows 11 a raised timer only applies while some process requests it, so this holds only " +
                "while Helios Toolkit is running — keep it minimized while gaming.",
        },

        new()
        {
            Id = "prio-separation",
            Name = "Foreground scheduling boost (Win32PrioritySeparation 0x26)",
            Page = TweakPage.Windows,
            Category = "CPU scheduling",
            Risk = RiskLevel.Situational,
            Description =
                "Switches the scheduler to short, fixed quanta with a strong foreground boost — the variant of " +
                "this classic tweak most often recommended for gaming. Default is 0x2. Effects are small and " +
                "workload-dependent; revert if anything feels off.",
        },

        new()
        {
            Id = "net-throttling",
            Name = "MMCSS network throttling off + SystemResponsiveness 10",
            Page = TweakPage.Windows,
            Category = "Multimedia scheduling (MMCSS)",
            Risk = RiskLevel.Situational,
            RequiresReboot = true,
            Description =
                "NetworkThrottlingIndex = 0xFFFFFFFF stops Windows from rate-limiting network processing while " +
                "multimedia (games/audio) runs — relevant mostly for high-packet-rate games. SystemResponsiveness " +
                "10 (default 20) reserves less CPU for background tasks during multimedia playback.",
        },
        new()
        {
            Id = "mmcss-games",
            Name = "MMCSS 'Games' task priority high",
            Page = TweakPage.Windows,
            Category = "Multimedia scheduling (MMCSS)",
            Risk = RiskLevel.Situational,
            Description =
                "Raises GPU Priority/Priority/Scheduling Category in the MMCSS Games profile. Only affects " +
                "applications that actually register with MMCSS as games — many modern titles don't, which is " +
                "why reviews of this tweak are mixed. Harmless to try.",
        },

        new()
        {
            Id = "tcp-nodelay",
            Name = "Disable Nagle's algorithm (TcpAckFrequency/TCPNoDelay)",
            Page = TweakPage.Windows,
            Category = "Network",
            Risk = RiskLevel.Situational,
            RequiresReboot = true,
            Description =
                "Sets TcpAckFrequency = 1 and TCPNoDelay = 1 on your active network adapter. Only matters for " +
                "games that send many tiny TCP packets (some MMOs — the old WoW fix); most modern shooters use " +
                "UDP and see zero difference. Can slightly increase bandwidth overhead.",
        },

        new()
        {
            Id = "sysmain-off",
            Name = "SysMain (Superfetch) service off",
            Page = TweakPage.Windows,
            Category = "Services & telemetry",
            Risk = RiskLevel.Situational,
            Description =
                "Stops the prefetch/preload service. On a fast NVMe SSD its benefit is small, and it has a " +
                "history of random background disk/CPU churn. Disabling can slightly slow app launches; on " +
                "this hardware most people notice nothing either way.",
        },
        new()
        {
            Id = "telemetry-leftovers",
            Name = "Telemetry leftovers (DiagTrack, Appraiser, CEIP)",
            Page = TweakPage.Windows,
            Category = "Services & telemetry",
            Risk = RiskLevel.Safe,
            Description =
                "Disables the Connected User Experiences service (DiagTrack) and the Compatibility Appraiser / " +
                "CEIP scheduled tasks — the pieces that occasionally burn CPU/disk at the worst time. On your " +
                "already-debloated install these are likely gone and this will show 'Not applicable'.",
        },

        new()
        {
            Id = "visualfx-perf",
            Name = "Visual effects: performance preset",
            Page = TweakPage.Windows,
            Category = "Visuals & shell",
            Risk = RiskLevel.Situational,
            RebootNote = "Sign out for full effect",
            Description =
                "Switches the visual-effects radio button to 'best performance' (VisualFXSetting = 2): no " +
                "animations, shadows or transparency. Frees a sliver of GPU/CPU; purely cosmetic trade. Font " +
                "smoothing stays on — nobody wants jagged text.",
        },

        new()
        {
            Id = "bcd-clock-defaults",
            Name = "Legacy timer tweaks (HPET / dynamic tick / platform clock)",
            Page = TweakPage.Windows,
            Category = "Advanced / experimental",
            Risk = RiskLevel.Risky,
            RequiresReboot = true,
            Description =
                "The old 'bcdedit /set disabledynamictick yes' + 'useplatformclock false' combo from forum lore. " +
                "Windows picks the right platform clock by itself on this hardware generation. Applying writes " +
                "those BCD elements; reverting deletes them (including tscsyncpolicy), returning to clean defaults.",
            Warning =
                "On modern Windows 11 these flags more often HURT frame pacing than help, and a bad clock " +
                "configuration can cause stutter, audio drift or timing bugs system-wide. Apply only to A/B " +
                "test, measure, and revert if you can't prove a win.",
        },
        new()
        {
            Id = "hvci-off",
            Name = "Memory Integrity (VBS/HVCI) off",
            Page = TweakPage.Windows,
            Category = "Advanced / experimental",
            Risk = RiskLevel.Risky,
            RequiresReboot = true,
            Description =
                "Turns off hypervisor-enforced code integrity. On some titles VBS costs a measurable 5–10% FPS " +
                "and a bit of latency; on others nothing. Sets DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity!" +
                "Enabled = 0.",
            Warning =
                "This is a real security trade: Memory Integrity blocks kernel-level exploits and some anti-cheat " +
                "drivers behave differently without it. Decide deliberately, and turn it back on when you're not " +
                "chasing benchmarks.",
        },

        new()
        {
            Id = "intel-apo-info",
            Name = "Intel Application Optimization (APO)",
            Page = TweakPage.Windows,
            Category = "Info",
            Risk = RiskLevel.Info,
            Description =
                "Intel's per-game scheduling layer for hybrid CPUs supports the Core Ultra 200HX series and " +
                "measurably helps in supported titles. It can't be toggled from here — install 'Intel " +
                "Application Optimization' (with Dynamic Tuning) from Intel/Acer, then enable per game in the " +
                "APO app. This card just detects whether it's present.",
        },
    };
}
