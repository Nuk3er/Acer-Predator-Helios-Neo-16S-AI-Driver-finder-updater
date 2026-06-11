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

**Current build (v0.2.0):** [`release/HeliosToolkit.exe`](release/HeliosToolkit.exe) — open it
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

- **Dashboard** — verifies your machine is a PHN16S-71 and shows CPU / GPU / RAM / NVMe /
  240 Hz display / OS cards plus a driver-health score.
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

- [x] **Driver fixing via Windows Update** *(v0.2.0)* — finds drivers for problem devices,
      multiselect, silent install, plus per-device Microsoft Update Catalog links and a
      pnputil "install from folder" for manual downloads.
- [x] **NVIDIA driver-profile automation** *(v0.2.0)* — one-click NVAPI bundle: Prefer maximum
      performance, threaded optimization, V-Sync off, pre-rendered frames 1, texture filtering
      high-performance. Fully revertible; degrades gracefully if NVAPI refuses.
- [x] **CPU & SSD tweak pack** *(v0.2.0)* — min processor state, EPP performance bias, C-state
      control (risky, labeled), NVMe idle, NTFS last-access/8.3, TRIM check.
- [x] **Tray mode** *(v0.2.0)* — minimize to tray; the 0.5 ms timer hold keeps running.
- [x] **In-app auto-update** *(v0.2.0)* — download & swap new releases from Settings.
- [ ] **Acer support-page integration** — auto-fill "latest version" in the driver manifest from
      Acer's official PHN16S-71 download listings (planned as a repo-side updater action).
- [ ] **Before/after benchmark helper** — PresentMon-based FPS + frame-time capture so every
      tweak can be measured instead of guessed.
- [ ] **Code signing** — no more SmartScreen warning.
- [ ] **More Predator models** — additional curated manifests (Helios 16/18, Neo 14…).
- [ ] **Localization** — German and more.

Have an idea? [Open an issue](../../issues).

## 🛠️ Building from source

Requires the .NET 8 SDK (builds on Windows, Linux or macOS):

```bash
dotnet test tests/HeliosToolkit.Core.Tests          # 155 unit tests
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
