# Changelog

All notable changes to **Helios Neo Toolkit** are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/) and versions follow [SemVer](https://semver.org/).

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
