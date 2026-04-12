# G-Aether

A lightweight, open-source control center for ASUS laptops. Built as a modern WPF rewrite of [g-helper](https://github.com/seerge/g-helper), G-Aether gives you full control over performance profiles, GPU switching, fan curves, RGB lighting, and display settings — without the bloat of Armoury Crate.

## Features

- **Performance Modes** — Switch between Silent, Balanced, and Turbo profiles. Create custom modes with your own power limits and fan curves.
- **GPU Control** — Toggle between Eco (iGPU only), Standard, Optimized, and Ultimate (dedicated) GPU modes on the fly.
- **Fan Curves** — Fine-tune CPU, GPU, and mid fan profiles per performance mode.
- **Power Limits** — Set custom CPU/GPU power limits, boost behavior, and AMD undervolting.
- **RGB / Aura Lighting** — Control keyboard backlight brightness, animation modes, colors, and speed. Supports Anime Matrix and Slash lighting on compatible models.
- **Display** — Screen refresh rate switching, overdrive control, and auto-rate based on AC/battery.
- **Battery Health** — Set charge limits to extend battery lifespan.
- **System Tray** — Split-circle icon showing performance and GPU mode at a glance. Right-click to switch modes without opening the app.
- **Hotkey Cycling** — Filmstrip OSD shows all available modes when cycling via keyboard shortcuts.
- **Custom Keybinds** — Remap FN keys, M1/M2 buttons, and set up custom hotkey actions.

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

Download the latest release from the [Releases](https://github.com/bloodner/g-aether/releases) page.

1. Download the `.zip` file from the latest release
2. Extract to a folder of your choice
3. Run `G-Aether.exe`

**Requirements:** Windows 10/11 with .NET 8.0 Desktop Runtime. If you don't have it, Windows will prompt you to install it on first launch.

> **Note:** G-Aether replaces Armoury Crate. You can stop ASUS services from within the app under Extra Settings.

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
