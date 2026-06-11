# Helios Neo Toolkit

Driver finder, updater and gaming optimizer built specifically for the
**Acer Predator Helios Neo 16S AI (PHN16S-71)** — Intel Core Ultra 9 275HX,
NVIDIA GeForce RTX 5070 / 5070 Ti Laptop GPU, 16" 2560×1600 OLED 240 Hz —
running a (debloated) Windows 11.

A polished, single-file Windows 11 app (Fluent dark UI) that finds your drivers,
debloats the NVIDIA driver before install, and applies the gaming tweaks that
actually move the needle on input latency and frame consistency — every one
labeled by risk and fully reversible.

## Features

- **Dashboard** — model detection, CPU/GPU/RAM/disk/display/OS summary and a driver health score.
- **Devices & Drivers** — scans every PnP device, surfaces broken/missing drivers, checks the
  NVIDIA driver against NVIDIA's official lookup API and the rest against a curated,
  repo-hosted driver manifest for this exact laptop. Downloads with progress + SHA-256,
  then launches installers — never silently.
- **NVIDIA** — driver debloat helper (NVCleanstall-style component pruning before install),
  telemetry off, MSI mode, HAGS, dynamic P-state control, MPO, and a driver performance
  profile (prefer max performance, low latency, V-Sync off).
- **Windows Tweaks** — the full catalog of gaming tweaks that actually matter, each labeled
  **Safe / Situational / Risky**, with honest descriptions, current-state detection and
  one-click revert.
- **Backup & Profiles** — System Restore point + JSON backup of every original value before
  anything is changed; revert everything; export/import tweak profiles.

## Download

Grab `HeliosToolkit.exe` from the [Releases](../../releases) page.

The EXE is unsigned, so Windows SmartScreen will warn on first run:
click **More info → Run anyway**. Verify the SHA-256 against the
`HeliosToolkit.exe.sha256` file attached to the release if you want to be sure.

The app requires administrator rights (registry, services, power plans and driver work).

## Building from source

```bash
# Windows (or Linux/macOS cross-compile):
dotnet publish src/HeliosToolkit.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Requires the .NET 8 SDK. Unit tests: `dotnet test tests/HeliosToolkit.Core.Tests`.

## Safety model

- Nothing is changed without your click; driver installs are always interactive.
- Before the first tweak of a session the app offers a System Restore point.
- Every original value is snapshotted to `%ProgramData%\HeliosToolkit\backup` before a tweak
  writes anything, and **Revert everything** puts it all back.
- Risky tweaks require an explicit confirmation and explain exactly what they touch.

## License

MIT — see [LICENSE](LICENSE).
