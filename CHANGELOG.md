# Changelog

All notable changes to **Helios Neo Toolkit** are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/) and versions follow [SemVer](https://semver.org/).

## [1.0.0] — 2026-06-13 — "The Lab Update"

A major release focused on measuring and proving performance, plus one-click optimization.

### Added
- **Lab page** with four tools:
  - **Timer Resolution Calibrator** — sweeps requested timer values 0.5000→0.5100 ms in 0.0002 ms
    steps on a TIME_CRITICAL P-core-pinned thread, measures real Sleep(1) wake-up latency + jitter,
    and finds your machine's optimum (often ~0.503–0.504 ms). Honesty rule keeps 0.5000 when the win
    is inside the noise. One click applies it everywhere (tweak, tray, logon task); hand-drawn chart.
  - **DPC / ISR latency monitor** — a real-time kernel ETW trace (LatencyMon, built in) that names the
    driver causing micro-stutter, with green/yellow/red verdicts and plain-language fix advice.
  - **FPS / frame-time benchmark** — Intel PresentMon capture (downloaded on demand, checksum-pinned)
    with avg / 1% low / 0.1% low, and an **A/B compare** that lists exactly which tweaks differed
    between two runs — so every tweak is proven, not guessed.
  - **Ping & jitter test** — quick connection check to pair with the network tweaks.
- **Game Boost** (Dashboard tile + tray) — one click: calibrated timer hold + Helios Ultimate power
  plan + Do Not Disturb + a curated kill-list of background apps (gracefully closed, restarted
  de-elevated on un-Boost). Crash-safe: session state is persisted before each step and rolled back
  on next launch if the app dies mid-Boost.
- **Autopilot** — watches your chosen game exes (WMI process-trace events with a polling fallback) and
  Boosts/restores automatically, with a 10 s debounce so launcher respawns don't flap.
- **Per-game P-core pinning** (off by default) — pins a watched game to the 275HX's 8 performance
  cores with High priority; skips gracefully on anti-cheat-protected processes.
- **Network latency pack** — three revertible NIC tweaks: interrupt moderation off (standardized
  *InterruptModeration keyword), Receive Segment Coalescing off, and never-power-down-the-adapter.
- **Hold timer at logon** — optional task that starts Helios minimized to the tray holding your
  calibrated timer.

### Notes
- New dependency: `Microsoft.Diagnostics.Tracing.TraceEvent` (MIT), referenced so its native bits stay
  out of the single-file bundle.

## [0.2.0] — 2026-06-11

### Added
- **Driver fixing via Windows Update** (Devices page): searches the Windows Update Agent for
  driver updates, flags the ones that fix your problem devices, multiselect + silent install
  with reboot detection; per-device **Microsoft Update Catalog** button for manual downloads;
  **Install from folder (INF)** via pnputil for drivers you downloaded yourself.
- **NVIDIA driver-profile automation (NVAPI)**: one toggle applies Prefer maximum performance,
  Threaded optimization, V-Sync off, Max pre-rendered frames 1 and Texture filtering =
  High performance to the global profile — setting IDs straight from NVIDIA's published
  NvApiDriverSettings.h, originals backed up, full revert, graceful degradation when NVAPI
  is unavailable.
- **CPU tweaks** for the Core Ultra 9 275HX: minimum processor state 100% (AC), Energy
  Performance Preference = maximum performance (AC), disable C-states (risky, labeled).
- **SSD/NVMe tweaks**: never power down disks (AC), NTFS last-access timestamps off,
  8.3 short-name generation off, TRIM status check.
- **Tray mode**: minimizing hides to the tray; the 0.5 ms timer-resolution hold keeps working
  while you game. Double-click to restore.
- **In-app auto-update**: Settings can download the new EXE and swap itself.

### Fixed
- NVIDIA page crashed on open (`BroomSparkle24` is not a valid icon in WPF-UI 4.3 — now `Broom24`).
- powercfg-based tweaks (CPU boost mode, core parking) showed "Not applied" after applying:
  hidden power settings don't appear in `powercfg /query`, detection now reads the
  ACSettingIndex registry values of the active scheme directly.
- Reverting a power tweak that was never explicitly set now removes the override entirely
  instead of writing a guessed default.
- System Restore "Provider load failure" (System Protection disabled/stripped) is now treated
  as "restore unavailable" instead of an error.
- Update check no longer logs an error when the repo has no published releases yet.

## [0.1.0] — 2026-06-11

First release. Built for the Acer Predator Helios Neo 16S AI (PHN16S-71) on Windows 11.

### Added
- **Dashboard**: PHN16S-71 model verification, CPU/GPU/RAM/NVMe/display/OS summary cards,
  driver-health score, quick actions.
- **Devices & Drivers**: full PnP inventory with problem-device surfacing and plain-English
  error descriptions; curated, repo-hosted driver manifest (Intel iGPU/Wi-Fi/BT/chipset/ME/NPU,
  Realtek audio, PredatorSense, BIOS) with 24 h cache and embedded offline fallback; live
  NVIDIA Game Ready lookup via NVIDIA's AjaxDriverService with runtime pfid resolution;
  streaming downloads with progress + SHA-256 and guided (never silent) installs.
- **NVIDIA driver debloater**: extracts the official driver package, strips GFE / NVIDIA App /
  telemetry / ShadowPlay / nodejs / nView while protecting Optimus & Dynamic Boost, patches
  `setup.cfg`, launches the trimmed installer.
- **NVIDIA tweaks**: HAGS, MSI interrupt mode, dynamic P-state lock, MPO, telemetry
  tasks/service, recommended low-latency driver settings card.
- **Windows tweaks** (23): GameDVR off, Game Mode, fullscreen-optimization control, toast
  notifications, Ultimate Performance plan, CPU boost mode, core parking, USB selective
  suspend, PCIe ASPM, hibernation/Fast Startup, mouse acceleration (live SPI apply), input
  queue sizes, 0.5 ms timer-resolution hold with power-throttling opt-out,
  Win32PrioritySeparation, MMCSS network throttling + Games profile, Nagle/TcpAckFrequency,
  SysMain, telemetry leftovers (DiagTrack/Appraiser/CEIP), visual-effects preset, legacy BCD
  clock flags (HPET/dynamic tick), Memory Integrity (VBS/HVCI), Intel APO info card — every
  tweak risk-labeled SAFE/SITUATIONAL/RISKY with live state detection and revert.
- **Safety**: System Restore point before the session's first change, append-only JSON backup
  of every original value (first-seen wins), Revert everything, tweak-profile export/import,
  first-run onboarding dialog.
- **Settings**: GitHub release update check, driver-manifest refresh, log access.
- **CI/CD**: Linux unit tests + win-x64 cross-compile check, Windows single-file EXE build,
  automatic GitHub Release (with EXE + SHA-256) on `v*` tags or manual workflow dispatch.
- 155 unit tests covering version parsing/comparison (incl. NVIDIA's WMI version format),
  manifest forward compatibility and tweak-catalog metadata invariants.
