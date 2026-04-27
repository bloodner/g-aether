# G-Scope: mode strip on the gadget + customizable telemetry panel

**Date:** 2026-04-27
**Status:** Design — ready for implementation plan

## Summary

Two related improvements to G-Aether's telemetry surfaces:

1. The Floating Gadget grows a mode strip showing the current Performance / GPU / Display / Services mode, mirroring the persistent strip in the main window.
2. The in-app "Live Telemetry" panel gets the same family of customization knobs that the gadget already has — Visible Tiles, Accent color, Size — plus a panel-only "history window" control.
3. Both surfaces are unified under a new family name, **G-Scope**. The gadget becomes "G-Scope Floating"; the panel becomes "G-Scope".

The gadget and the panel keep **independent** settings — the two surfaces serve different jobs (peripheral glance vs. focused investigation), and a user who curates the gadget for minimalism will typically want everything visible when the panel is open.

## Naming

| Where | Today | After |
|---|---|---|
| Panel header text | "Live Telemetry" | **"G-Scope"** |
| Gadget settings window title | "Floating Gadget Settings" | **"G-Scope Floating Settings"** |
| Help, toasts, README copy | "Live Telemetry", "Floating Gadget" | **"G-Scope"** is the family name; the gadget is **"G-Scope Floating"**; "Floating Gadget" stays as a casual synonym in dialog copy where users already know the phrase |
| Sidebar nav glyph | (sparkline icon) | unchanged |
| Internal class names | `GadgetWindow`, `GadgetService`, `MonitorViewModel`, `MonitorPanel` | unchanged — renaming code costs more than it pays back |

The README's "Live Telemetry, Floating Gadget, and developer pipe" section is rewritten to use the new names.

## G-Scope Floating: mode strip

### Layout

A new row sits between the existing header (`G-Aether [×]`) and the tile grid. Full gadget width, four badges evenly spaced, hairline divider lines top and bottom — the same visual treatment the main window's strip uses.

When the strip is on, the gadget grows from 300 px tall to ~325 px (≈22 px content + 1 px dividers + 2 px breathing room). `GadgetService` already recalculates gadget size from content; the existing logic handles this.

### Typography

Each badge uses `ModeBadgeValueStyle` and `ModeBadgeIconStyle` directly from `Themes/Theme.xaml` via `StaticResource`. No copies, no overrides. If those styles ever change in the main strip, the gadget tracks automatically.

For reference, the styles resolve to:

- Value: `Segoe UI Variable`, FontSize 11, FontWeight SemiBold
- Icon: `Segoe MDL2 Assets`, FontSize 12, 7 px right margin
- Padding: 8,3,10,3

### Bindings

The gadget badges bind to the same four name/brush/warning property triples the main strip uses:

- `PerfBadgeName` / `PerfBadgeBrush` / `PerfBadgeIsWarning`
- `GpuBadgeName` / `GpuBadgeBrush` / `GpuBadgeIsWarning`
- `DisplayBadgeName` / `DisplayBadgeBrush`
- `ServicesBadgeName` / `ServicesBadgeBrush` / `ServicesBadgeIsWarning`

These properties move out of `MainViewModel` into a new `ModeStripViewModel`. `MainViewModel` exposes a single `ModeStrip` property; the gadget's `DataContext` exposes the same instance alongside its existing `MonitorViewModel`. Single source of truth, two consumers.

### Behavior

- **Display only.** Clicks on a gadget badge do nothing. The gadget is a peripheral display, not a control surface, and there is nowhere on the gadget to navigate to.
- **Tooltips.** Hover shows the full mode label, e.g., `"Performance: Turbo"` — same format the main strip uses today.
- **Pulse.** Warning-state icons pulse via `ModeBadgePulseIconStyle`, identical to the main strip. Users see warnings consistently across both surfaces.

### Visibility toggle

A new "Show mode strip" toggle in the **G-Scope Floating Settings** window, defaulting to **on**. Persisted as `gadget_show_modestrip` in `AppConfig`. Lets users who want the most compact gadget turn it off.

## G-Scope panel: customization

### Entry point

A small gear icon (`Segoe MDL2 &#xE713;`) sits in the panel header next to the existing `HelpButton`. Clicking opens a new **G-Scope Settings** window, styled identically to `GadgetSettingsWindow` (same chrome, same card patterns, same close-button treatment).

### Knobs

**1. Visible Tiles** — eight toggles, mirroring the gadget's: CPU Temp, dGPU Temp, CPU Use, dGPU Use, dGPU Power, Battery, CPU Fan, GPU Fan. Each binds to a `ScopeShow*` boolean on `ScopeSettingsViewModel`. Persisted as `scope_show_*` keys in `AppConfig`.

**2. Accent color** — same eight swatches as the gadget: multi / blue / purple / green / orange / red / white / dark. On `multi`, sparklines keep their current per-tile colors (`#FF6B6B`, `#FFB347`, `#A78BFA`, `#60CDFF`, `#6BCB77`, `#C084FC`, `#F472B6`). On any single accent, all sparkline strokes and halos tint to that color, including the battery row's accent halo. Persisted as `scope_accent`.

**3. Size** — pill selector with three values:

| Preset | Sparkline height | Value font size |
|---|---|---|
| Compact | 80 | 22 |
| Regular *(default)* | 106 | 28 |
| Large | 130 | 32 |

Persisted as `scope_size`. The "X-Small" preset from the gadget is dropped — at panel scale it would crush the sparklines below readable density.

**4. History window** — pill selector: **60 s / 5 min / 15 min**. `MonitorViewModel` keeps one rolling buffer per series, each sized for the longest window (15 min). Shorter windows render the tail of that buffer. Sample cadence stays at the existing 2 s tick — that's 30 / 150 / 450 points respectively, well within what `SparklineChart` can draw without downsampling. Persisted as `scope_history_window`.

### Live application

Settings apply immediately as the user toggles them — no "Apply" button. `ScopeSettingsViewModel` writes to `AppConfig` and calls a new static `ScopeService.ApplySettings()` (parallel to `GadgetService.ApplySettings()`), which raises a notification that `MonitorPanel` listens for. Sparklines re-style without a panel re-mount.

### Battery row

The current panel has a special non-sparkline battery row at the bottom with charge percent, status text, and an accent halo. It stays as the bottom row, but the new "Battery" Visible-Tiles toggle now controls its visibility. This keeps parity with the gadget's tile list and lets users hide it on desktops or wall-powered machines.

### dGPU-off banner

The existing "dGPU Off (Eco Mode)" banner that replaces dGPU tiles when the GPU is parked is preserved unchanged.

## Settings architecture

### Independent namespaces

Gadget and panel each own their own `AppConfig` keys. No shared state.

```
gadget_show_modestrip    bool   (new)
gadget_show_*            bool   (existing, 8 tile toggles)
gadget_accent            string
gadget_size              string
gadget_opacity           double
gadget_hide_close        bool
gadget_hide_logo         bool
gadget_hover_fade        bool
gadget_hotkey            string
gadget_x, gadget_y       int

scope_show_cpu_temp      bool
scope_show_dgpu_temp     bool
scope_show_cpu_use       bool
scope_show_dgpu_use      bool
scope_show_power         bool
scope_show_battery       bool
scope_show_cpu_fan       bool
scope_show_gpu_fan       bool
scope_accent             string  ("multi"|"blue"|"purple"|"green"|"orange"|"red"|"white"|"dark")
scope_size               string  ("compact"|"regular"|"large")
scope_history_window     string  ("60s"|"5min"|"15min")
```

Defaults match today's behavior: all eight `scope_show_*` = true, accent = `multi`, size = `regular`, history = `60s`. Existing users see no change until they open settings.

### View models

- **`ModeStripViewModel`** *(new)* — owns the four badge name/brush/warning property triples. Lifted from `MainViewModel`. Subscribes to the existing 2 s sensor tick.
- **`ScopeSettingsViewModel`** *(new)* — owns all `scope_*` properties. Two-way bound to `ScopeSettingsWindow`. On any change: writes to `AppConfig`, then calls `ScopeService.ApplySettings()`.
- **`MonitorViewModel`** *(extended)* — rolling buffers grow to hold 15 min of samples (450 points × 8 series ≈ 14 KB, trivial). New computed properties `HistoryWindowSeconds`, `SparklineHeight`, `ValueFontSize`, plus an `AccentBrushFor(tile)` helper drive the sparklines through bindings. Observes `scope_*` keys.
- **`MainViewModel`** *(reduced)* — delegates badge logic to `ModeStripViewModel`. Exposes a single `ModeStrip` property.

### Services

- **`ScopeService`** *(new)* — static class, parallel to `GadgetService`. Methods: `Configure(MonitorViewModel)`, `ApplySettings()`. No window lifecycle (the panel is part of the main window), so much smaller than `GadgetService`.
- **`GadgetService`** *(unchanged surface)* — `ApplySettings()` now also re-evaluates mode-strip visibility.

### Runtime data flow

```
2 s sensor tick
  → MonitorViewModel.OnSensorTick (existing, on background task)
  → updates rolling buffers + scalar text properties
  → SparklineChart.Values bindings re-render
  → ModeStripViewModel.Refresh()
  → both main strip + gadget strip update in lockstep
```

No new timers, no new background work. Everything piggybacks on the existing tick.

### Files

**New:**

- `Views/ScopeSettingsWindow.xaml` + `.cs`
- `ViewModels/ScopeSettingsViewModel.cs`
- `ViewModels/ModeStripViewModel.cs`
- `Services/ScopeService.cs`

**Edited:**

- `Views/GadgetWindow.xaml` — add mode strip row + visibility binding
- `Views/GadgetSettingsWindow.xaml` — add "Show mode strip" toggle, retitle to "G-Scope Floating Settings"
- `Views/Panels/MonitorPanel.xaml` — rename header to "G-Scope", add gear button, bind size/accent/visibility/history
- `ViewModels/MonitorViewModel.cs` — extend buffers, add accent/size/history-window helpers
- `ViewModels/MainViewModel.cs` — delegate badge logic to `ModeStripViewModel`
- `App.xaml.cs` (or wherever `GadgetService.Configure` is called) — also call `ScopeService.Configure`
- `README.md` — rename references in the telemetry/gadget section

## Out of scope

- **Tile reorder.** Drag-to-reorder is the kind of thing that sounds nice but few users will actually use, and it complicates saved layout state. Skipped.
- **Click-to-navigate on gadget badges.** Considered and rejected: there is nowhere on the gadget to navigate to, and bringing the main window forward on a peripheral display click is intrusive.
- **Renaming internal classes** (`GadgetWindow`, `MonitorViewModel`, etc.). Cost outweighs the benefit; only user-facing strings change.
- **Sample-rate changes.** History window only changes how much of the buffer is rendered, not how often samples are taken. The 2 s tick stays.
- **Gadget shrinking when the strip is hidden.** When `gadget_show_modestrip` is off, the strip row collapses to zero height — same `SizeToContent` recompute path the existing tile-toggle logic uses.
