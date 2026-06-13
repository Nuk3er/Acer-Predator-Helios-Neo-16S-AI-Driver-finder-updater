<div align="center">

# Helios Neo Toolkit

**Driver finder, NVIDIA debloater & gaming optimizer built for one machine:<br>
the Acer Predator Helios Neo 16S AI (PHN16S-71)**

[![Build](https://github.com/Nuk3er/Acer-Predator-Helios-Neo-16S-AI-Driver-finder-updater/actions/workflows/build.yml/badge.svg)](https://github.com/Nuk3er/Acer-Predator-Helios-Neo-16S-AI-Driver-finder-updater/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/Nuk3er/Acer-Predator-Helios-Neo-16S-AI-Driver-finder-updater?color=00e5d1&label=release)](../../releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Nuk3er/Acer-Predator-Helios-Neo-16S-AI-Driver-finder-updater/total?color=00e5d1)](../../releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D6)](#)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](#)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

*Intel Core Ultra 9 275HX · GeForce RTX 5070 / 5070 Ti Laptop GPU · 16″ 2560×1600 OLED 240 Hz*

</div>

---

## 📥 Download

**Current build (v1.0.0):** [`release/HeliosToolkit.exe`](release/HeliosToolkit.exe) — open it
and click **Download raw file**. Single self-contained EXE, nothing to install.
Checksum: [`release/HeliosToolkit.exe.sha256`](release/HeliosToolkit.exe.sha256).

Once GitHub Actions runs on this account, tagged builds publish automatically to the
**[Releases page](../../releases/latest)**, which then becomes the preferred download
(and what the in-app updater watches).

- The EXE is unsigned, so SmartScreen warns on first run: **More info → Run anyway.**
  Each release ships a `HeliosToolkit.exe.sha256` you can verify with `Get-FileHash`.
- The app asks for **administrator rights** (registry, services, power plans and driver work).
- Built for a (debloated) Windows 11 — every tweak detects its current state first, so an
  already-tuned install simply shows what's left to do.

## ✨ Features

- **Lab** *(measure, don't guess)* — **Timer Resolution Calibrator** that benchmarks your machine's
  optimal sub-millisecond timer value; a built-in **DPC/ISR latency monitor** that names the driver
  causing micro-stutter; an **Intel PresentMon FPS/frame-time benchmark** with A/B compare that shows
  which tweaks actually moved the needle; and a ping/jitter test.
- **Game Boost** *(one click)* — calibrated timer hold + Ultimate power plan + Do Not Disturb + a
  curated kill-list, with optional **autopilot** (auto-boost when your game launches, restore on exit)
  and optional **P-core pinning** for the 275HX. Crash-safe: it always restores.
- **Dashboard** — verifies your machine is a PHN16S-71 and shows CPU / GPU / RAM / NVMe /
  240 Hz display / OS cards plus a driver-health score and the Game Boost tile.
- **Devices & Drivers** — scans every PnP device, pins broken or driverless devices to the top
  with plain-English problem descriptions, checks the GPU driver against **NVIDIA's official
  lookup API** and the rest against a **curated driver manifest for this exact laptop**
  (updated in this repo — the app picks it up without needing a new EXE). Downloads show
  progress + SHA-256; installers always launch interactively, never silently.
- **NVIDIA driver debloater** — point it at a downloaded Game Ready package and it extracts it,
  lets you strip GeForce Experience, the NVIDIA App, telemetry, ShadowPlay & friends while
  laptop-critical parts (Advanced Optimus, Dynamic Boost) stay locked ON, patches `setup.cfg`,
  and launches the trimmed installer — the NVCleanstall workflow, built in.
- **NVIDIA tweaks** — HAGS, MSI interrupt mode, dynamic P-state lock, MPO, telemetry tasks,
  plus a recommended low-latency settings card (Reflex / max performance / G-SYNC).
- **Windows tweaks** — a full catalog (GameDVR, Game Mode, fullscreen optimizations, Ultimate
  Performance plan, CPU boost mode, core parking, USB/PCIe power, mouse acceleration, input
  queues, 0.5 ms timer-resolution hold, Win32PrioritySeparation, MMCSS, Nagle, SysMain,
  telemetry leftovers, visual effects, HPET/dynamic tick, VBS/HVCI…) — each one labeled
  **SAFE / SITUATIONAL / RISKY**, honestly described (including which famous tweaks do nothing
  on modern Windows 11), detected live, and individually revertible.
- **Backup & Profiles** — System Restore point before the first change, JSON backup of every
  original value, **Revert everything**, and tweak-profile export/import.
- **Settings** — in-app update check against this repo's Releases, manifest refresh, logs.

## 🗺️ Roadmap

- [x] **Driver fixing via Windows Update** *(v0.2.0)*
- [x] **NVIDIA driver-profile automation (NVAPI)** *(v0.2.0)*
- [x] **CPU & SSD tweak pack** *(v0.2.0)*
- [x] **Tray mode & in-app auto-update** *(v0.2.0)*
- [x] **Timer Resolution Calibrator** *(v1.0.0)*
- [x] **DPC/ISR latency monitor** *(v1.0.0)*
- [x] **PresentMon FPS/frametime benchmark with A/B compare** *(v1.0.0)*
- [x] **Game Boost + autopilot + P-core pinning** *(v1.0.0)*
- [x] **Network latency pack** *(v1.0.0)*
- [ ] **Acer support-page integration** — auto-fill "latest version" in the driver manifest from
      Acer's official PHN16S-71 download listings (planned as a repo-side updater action).
- [ ] **Code signing** — no more SmartScreen warning.
- [ ] **More Predator models** — additional curated manifests (Helios 16/18, Neo 14…).
- [ ] **Localization** — German and more.

Have an idea? [Open an issue](../../issues).

## 🛠️ Building from source

Requires the .NET 8 SDK (builds on Windows, Linux or macOS):

```bash
dotnet test tests/HeliosToolkit.Core.Tests          # 214 unit tests
dotnet publish src/HeliosToolkit.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 🚀 Cutting a release (maintainer notes)

1. Update [`CHANGELOG.md`](CHANGELOG.md) and merge everything to `main`.
2. Go to **Actions → Build → Run workflow**, enter the version (e.g. `0.2.0`) — CI builds the
   EXE on a Windows runner, creates tag `v0.2.0` and publishes the GitHub Release with
   `HeliosToolkit.exe` + SHA-256 attached automatically.
   (Pushing a `v*` tag does the same thing.)
3. The app's Settings page notifies users of the new release.

## 🛡️ Safety model

- Nothing changes without your click; driver installs are always interactive.
- Before the first tweak of a session the app offers a System Restore point.
- Every original value is snapshotted to `%ProgramData%\HeliosToolkit\backup` before a tweak
  writes anything — **Revert everything** puts it all back.
- Risky tweaks require explicit confirmation and explain exactly what they touch and why you
  might not want them.

## 📜 License

MIT — see [LICENSE](LICENSE).
