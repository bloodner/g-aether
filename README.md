# G-Aether

A lightweight, open-source control center for ASUS laptops. Built as a modern WPF rewrite of [g-helper](https://github.com/seerge/g-helper), G-Aether gives you full control over performance profiles, GPU switching, fan curves, RGB lighting, and display settings — without the bloat of Armoury Crate.

## Features

- **Performance Modes** — Switch between Silent, Balanced, and Turbo profiles. Create custom modes with your own power limits and fan curves.
- **GPU Control** — Toggle between Eco (iGPU only), Standard, Optimized, and Ultimate (dedicated) GPU modes on the fly.
- **Fan Curves** — Fine-tune CPU, GPU, and mid fan profiles per performance mode.
- **Power Limits** — Set custom CPU/GPU power limits, boost behavior, and AMD undervolting.
- **RGB / Aura Lighting** — Control keyboard backlight brightness, animation modes, colors, and speed. Supports Anime Matrix and Slash lighting on compatible models.
- **Display** — Screen refresh rate switching, overdrive control, and auto-rate based on AC/battery.
- **G-Scope** — Dedicated Monitor page with sparkline tiles for CPU and dGPU temps, CPU and dGPU usage, dGPU power draw, fan RPM, and battery state. Updates every second; the chart history covers the last 60 seconds.
- **G-Scope Floating** — Compact, always-on-top mini-window that shows live telemetry over games and other apps. Pick which of 8 tiles to show, choose from four size presets, set transparency down to 20%, swap accent colors (including a multi-color "rainbow" mode that keeps each tile's original hue, plus white and a stealth near-black), enable disappear-on-hover, and assign a global hotkey to toggle visibility.
- **Battery Health** — Set charge limits to extend battery lifespan.
- **System Tray** — Split-circle icon showing performance and GPU mode at a glance. Right-click to switch modes without opening the app.
- **Hotkey Cycling** — Filmstrip OSD shows all available modes when cycling via keyboard shortcuts.
- **Custom Keybinds** — Remap FN keys, M1/M2 buttons, and set up custom hotkey actions.
- **Smart Optimize** — "Optimize for Me" button analyzes your GPU usage history and power state, then auto-configures performance, GPU, and display settings for your usage pattern.
- **Contextual Help** — Hover the `?` icons next to any setting for a plain-English explanation of what it does and when to use it.
- **At-a-Glance Status Strip** — Persistent badge strip under the title bar shows Performance / GPU / Display / Services state on every panel; click a badge to jump to that panel.
- **Live Changelog** — "View Changelog" pulls the full release history from GitHub. When an update is available, the release notes preview in-app before you choose to download.

## Performance

G-Aether is engineered to stay out of your way:

- **6.6 MB framework-dependent single-file** exe (vs. ~164 MB self-contained)
- **Fast startup** — hardware enumeration (GPU driver, display modes, keyboard firmware, battery WMI) runs on background threads so the window appears immediately
- **One unified 1 Hz sensor tick** drives all UI updates; no duplicate timers
- **Batched async logger** writes in 3-second groups instead of per-event disk hits, so the log never stalls the UI

Most hardware reads happen off the UI thread; the main window renders in well under a second on a modern laptop.

## For developers

G-Aether streams a JSON telemetry snapshot per sensor tick over the named pipe `\\.\pipe\g-aether-telemetry`. The pipe is AppContainer-accessible, so sandboxed clients (Game Bar widgets, UWP apps, etc.) can read it. One newline-delimited record looks like:

```json
{"schema_version":1,"timestamp":"2026-04-25T03:14:15Z","cpu_temp_c":74,"dgpu_temp_c":61,"dgpu_usage_pct":58,"dgpu_power_w":81,"cpu_fan_rpm":5100,"gpu_fan_rpm":4900,"battery_pct":100,"battery_rate_w":0.0}
```

Connect with any pipe client (PowerShell, .NET, Node, Python via `pywin32`) and read line-by-line.

## Supported Hardware

G-Aether works with most ASUS laptops that use ASUS ACPI/WMI interfaces, including:

- **ROG Zephyrus** — G14, G15, G16
- **ROG Flow** — X13, X16, Z13
- **ROG Strix / Scar** — Full-size gaming models
- **ROG Ally / Ally X** — Handheld mode support
- **ROG Duo** — Dual-screen models
- **TUF Gaming** — FX, FA, and TX series
- **ProArt** — StudioBook and creator laptops
- **Vivobook / Zenbook** — Consumer models with ASUS ACPI support

The app auto-detects your model and enables the relevant features.

## Installation

1. Download `G-Aether.exe` from the latest [release](https://github.com/bloodner/g-aether/releases)
2. Double-click to run (you'll see a UAC prompt — admin rights are required for hardware access)
3. Pin the tray icon from the Windows overflow menu if you want it always visible

**Requirements:** Windows 10/11 with the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime). If you don't have it, Windows will prompt you to install it on first launch.

> **Note:** G-Aether replaces Armoury Crate. You can stop ASUS services from within the app under Settings → ASUS Services.

## Changelog

Full version history lives on the [Releases](https://github.com/bloodner/g-aether/releases) page and is also viewable in-app: **Settings → View Changelog**. When an update is available, the in-app update flow previews the new release notes before you download.

## Building from Source

```bash
git clone https://github.com/bloodner/g-aether.git
cd g-aether

# Build
dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release

# Or use the dev script
./dev.sh build

# Run
./dev.sh start
```

**Requirements:** .NET 8.0 SDK, Windows (WPF/WinForms targets `net8.0-windows`).

## Relationship to G-Helper

G-Aether is a fork of [g-helper](https://github.com/seerge/g-helper) by seerge. The original project uses Windows Forms — G-Aether is a ground-up UI rewrite in WPF with an MVVM architecture, while sharing the same battle-tested hardware control layer. This means the same broad device compatibility, with a modernized interface.

## License

[GPL-3.0](LICENSE)
