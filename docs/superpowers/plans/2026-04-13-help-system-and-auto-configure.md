# Help System & Smart Auto-Configure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add contextual help tooltips across the app and a smart "Optimize for Me" button that analyzes usage patterns to auto-configure all settings.

**Architecture:** Two independent features sharing a common codebase. Feature 1 adds a reusable `HelpButton` control that shows a flyout popup with setting-specific help text, placed next to key settings across Performance, GPU, Display, and Settings panels. Feature 2 adds a `UsageAnalyzer` service that parses log history and system state to recommend optimal settings, exposed via an "Optimize for Me" card on the Settings panel with a reasoning summary.

**Tech Stack:** WPF (XAML + C#), CommunityToolkit.Mvvm, .NET 8

---

## File Structure

### Feature 1: Help System

| File | Action | Responsibility |
|------|--------|---------------|
| `app/GHelper.WPF/Controls/HelpButton.cs` | Create | Reusable `?` icon control with flyout popup |
| `app/GHelper.WPF/Services/HelpContent.cs` | Create | Static dictionary of all help texts keyed by setting ID |
| `app/GHelper.WPF/Themes/Theme.xaml` | Modify | Add `HelpButtonStyle` and `HelpFlyoutStyle` |
| `app/GHelper.WPF/Views/Panels/PerformancePanel.xaml` | Modify | Add help buttons next to mode selector and fan controls |
| `app/GHelper.WPF/Views/Panels/GpuPanel.xaml` | Modify | Add help button next to GPU mode selector |
| `app/GHelper.WPF/Views/Panels/VisualModePanel.xaml` | Modify | Add help buttons next to refresh rate, color temp, gamut |
| `app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml` | Modify | Add help buttons next to key toggles |

### Feature 2: Smart Auto-Configure

| File | Action | Responsibility |
|------|--------|---------------|
| `app/GHelper.WPF/Services/UsageAnalyzer.cs` | Create | Parses logs + system state, produces a usage profile |
| `app/GHelper.WPF/Services/AutoConfigService.cs` | Create | Maps usage profile to optimal settings, generates recommendations |
| `app/GHelper.WPF/ViewModels/ExtraSettingsViewModel.cs` | Modify | Add properties/commands for the Optimize card |
| `app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml` | Modify | Add "Optimize for Me" card UI |

---

## Feature 1: Help System

### Task 1: Create HelpContent dictionary

**Files:**
- Create: `app/GHelper.WPF/Services/HelpContent.cs`

- [ ] **Step 1: Create the help content service**

This is a static class with a dictionary mapping setting IDs to title + description pairs. All help text lives here so it's easy to update without touching XAML.

```csharp
// app/GHelper.WPF/Services/HelpContent.cs
namespace GHelper.WPF.Services
{
    public record HelpEntry(string Title, string Description);

    public static class HelpContent
    {
        public static readonly Dictionary<string, HelpEntry> Entries = new()
        {
            // Performance modes
            ["perf_mode"] = new("Performance Modes",
                "Silent — Lowest fan noise and power draw. Best for quiet work, browsing, and battery life. " +
                "CPU and GPU run at reduced clocks.\n\n" +
                "Balanced — Default mode. Fans ramp up under load but stay quiet at idle. " +
                "Good all-around for mixed workloads.\n\n" +
                "Turbo — Maximum performance. Fans run aggressively to keep thermals in check. " +
                "Use for gaming, rendering, or heavy multitasking."),

            ["fan_control"] = new("Fan & Power Control",
                "Auto — G-Aether manages fans and power limits automatically based on your performance mode.\n\n" +
                "Manual Fans — Set custom fan curves for CPU and GPU. Drag points on the curve to adjust " +
                "fan speed at different temperatures.\n\n" +
                "Manual Power — Override CPU/GPU power limits (TDP). Higher values = more performance but more heat.\n\n" +
                "Manual Both — Full control over both fan curves and power limits."),

            // GPU modes
            ["gpu_mode"] = new("GPU Modes",
                "Eco — Disables the dedicated GPU entirely. The laptop runs on integrated graphics only. " +
                "Best battery life, but no gaming or GPU-accelerated tasks.\n\n" +
                "Standard — Both iGPU and dGPU are available. The system switches automatically based on demand. " +
                "Normal mode for most users.\n\n" +
                "Ultimate — Routes display directly through the dGPU (MUX switch). " +
                "Eliminates iGPU bottleneck for maximum gaming FPS. Requires restart.\n\n" +
                "Optimized — Like Standard, but automatically switches to Eco when on battery " +
                "and back to Standard when plugged in."),

            // Display
            ["refresh_rate"] = new("Screen Refresh Rate",
                "Higher refresh rates (144Hz, 240Hz) make motion smoother — great for gaming and scrolling. " +
                "Lower rates (60Hz, 120Hz) save significant battery life.\n\n" +
                "Auto — G-Aether picks the best rate based on whether you're plugged in or on battery."),

            ["color_temp"] = new("Color Temperature",
                "Adjusts the warmth of your display. Warmer (yellow-ish) tones reduce eye strain at night. " +
                "Cooler (blue-ish) tones are more accurate for color work.\n\n" +
                "This does not affect color accuracy for design work — use the Gamut setting for that."),

            ["gamut"] = new("Color Gamut",
                "Controls the color space your display uses.\n\n" +
                "Native — Full display capability, widest colors. May look oversaturated for web content.\n\n" +
                "sRGB — Standard web/office color space. Most accurate for general use.\n\n" +
                "DCI-P3 — Wide gamut used in film and HDR content. Good for media consumption."),

            ["overdrive"] = new("Panel Overdrive",
                "Speeds up pixel response time to reduce motion blur and ghosting. " +
                "May cause slight overshoot artifacts on some panels. " +
                "Disable if you notice inverse ghosting (bright trails behind moving objects)."),

            // Settings
            ["fn_lock"] = new("Fn Lock",
                "When enabled, F1-F12 keys act as standard function keys by default " +
                "(you hold Fn to access media/brightness). When disabled, the special functions " +
                "are the default and you hold Fn for F1-F12."),

            ["gpu_fix"] = new("GPU Fix on Shutdown",
                "Forces the dedicated GPU to Standard mode before shutdown or hibernate. " +
                "Prevents a rare issue where some laptops fail to wake from sleep " +
                "when the GPU was in Eco mode. Enable if you experience wake-from-sleep problems."),

            ["boot_sound"] = new("Boot Sound",
                "The ASUS POST beep that plays when you power on the laptop. " +
                "Disable to start up silently."),

            ["asus_services"] = new("ASUS Services",
                "ASUS installs background services (Armoury Crate, optimization agents, telemetry) " +
                "that consume RAM, CPU, and sometimes conflict with G-Aether.\n\n" +
                "G-Aether replaces all of their functionality. Stopping them frees resources " +
                "and prevents conflicts. The orange dot on the Settings icon warns you when they're running."),

            ["charge_limit"] = new("Battery Charge Limit",
                "Limits the maximum battery charge to extend long-term battery health. " +
                "Lithium batteries degrade faster when kept at 100%. " +
                "Set to 80% for daily use, or 100% only when you need full capacity for travel."),
        };

        public static HelpEntry? Get(string key) =>
            Entries.TryGetValue(key, out var entry) ? entry : null;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Services/HelpContent.cs
git commit -m "feat: add HelpContent dictionary with setting descriptions"
```

---

### Task 2: Create HelpButton control

**Files:**
- Create: `app/GHelper.WPF/Controls/HelpButton.cs`
- Modify: `app/GHelper.WPF/Themes/Theme.xaml`

- [ ] **Step 1: Create the HelpButton control**

A small `?` circle that shows a dark flyout popup on click. Uses a WPF `Popup` with a `Border` containing the title and description text. Clicking anywhere outside dismisses it.

```csharp
// app/GHelper.WPF/Controls/HelpButton.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GHelper.WPF.Services;

namespace GHelper.WPF.Controls
{
    public class HelpButton : FrameworkElement
    {
        public static readonly DependencyProperty HelpKeyProperty =
            DependencyProperty.Register(nameof(HelpKey), typeof(string), typeof(HelpButton),
                new PropertyMetadata(null));

        public string HelpKey
        {
            get => (string)GetValue(HelpKeyProperty);
            set => SetValue(HelpKeyProperty, value);
        }

        private Popup? _popup;
        private bool _isOpen;

        public HelpButton()
        {
            Width = 18;
            Height = 18;
            Cursor = Cursors.Hand;
            ToolTip = "Click for help";
            Focusable = true;
        }

        protected override void OnRender(System.Windows.Media.DrawingContext dc)
        {
            base.OnRender(dc);

            double size = Math.Min(ActualWidth, ActualHeight);
            double cx = ActualWidth / 2;
            double cy = ActualHeight / 2;
            double r = size / 2;

            // Circle background
            var bgColor = _isOpen
                ? Color.FromArgb(60, 0x60, 0xCD, 0xFF)
                : IsMouseOver
                    ? Color.FromArgb(40, 255, 255, 255)
                    : Color.FromArgb(20, 255, 255, 255);
            dc.DrawEllipse(new SolidColorBrush(bgColor), null, new System.Windows.Point(cx, cy), r, r);

            // "?" text
            var textColor = _isOpen
                ? Color.FromRgb(0x60, 0xCD, 0xFF)
                : Color.FromRgb(0x90, 0x90, 0x9A);
            var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = new FormattedText("?", System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, 10, new SolidColorBrush(textColor), dpi);
            dc.DrawText(ft, new System.Windows.Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isOpen) InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (_isOpen)
                    ClosePopup();
                else
                    OpenPopup();
            }
        }

        private void OpenPopup()
        {
            var entry = HelpContent.Get(HelpKey);
            if (entry == null) return;

            var title = new TextBlock
            {
                Text = entry.Title,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            };

            var body = new TextBlock
            {
                Text = entry.Description,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB8)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
            };

            var stack = new StackPanel();
            stack.Children.Add(title);
            stack.Children.Add(body);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 20, 20, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0x60, 0xCD, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                MaxWidth = 280,
                Child = stack,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 20, ShadowDepth = 4, Opacity = 0.6
                }
            };

            _popup = new Popup
            {
                Child = border,
                PlacementTarget = this,
                Placement = PlacementMode.Left,
                HorizontalOffset = -8,
                VerticalOffset = -4,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
            };

            _popup.Closed += (s, e) =>
            {
                _isOpen = false;
                InvalidateVisual();
            };

            _popup.IsOpen = true;
            _isOpen = true;
            InvalidateVisual();
        }

        private void ClosePopup()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
                _isOpen = false;
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(18, 18);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Controls/HelpButton.cs
git commit -m "feat: add HelpButton control with flyout popup"
```

---

### Task 3: Add help buttons to Performance panel

**Files:**
- Modify: `app/GHelper.WPF/Views/Panels/PerformancePanel.xaml`

- [ ] **Step 1: Add help button next to the Performance mode header**

In `PerformancePanel.xaml`, find the header section and add a `HelpButton` after the title. The exact insertion depends on current XAML structure. Look for the card title area and the PillSelector for mode selection.

Add `xmlns:controls="clr-namespace:GHelper.WPF.Controls"` to the UserControl if not already present.

Add a `HelpButton` in the header row next to the title text:

```xaml
<controls:HelpButton HelpKey="perf_mode" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Add another next to the Fan & Power Control section header (near the ApplyStrategy PillSelector):

```xaml
<controls:HelpButton HelpKey="fan_control" VerticalAlignment="Center" Margin="8,0,0,0" />
```

The help button should be placed in the same `StackPanel Orientation="Horizontal"` as the title text, or in a Grid column after the title.

- [ ] **Step 2: Verify it compiles and renders**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Views/Panels/PerformancePanel.xaml
git commit -m "feat: add help buttons to Performance panel"
```

---

### Task 4: Add help buttons to GPU panel

**Files:**
- Modify: `app/GHelper.WPF/Views/Panels/GpuPanel.xaml`

- [ ] **Step 1: Add help button next to the GPU Mode header**

In `GpuPanel.xaml`, find the header `StackPanel Orientation="Horizontal"` that contains the GPU icon and `HeaderText` binding. Add:

```xaml
<controls:HelpButton HelpKey="gpu_mode" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Ensure the `controls` xmlns is declared at the top of the UserControl.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Views/Panels/GpuPanel.xaml
git commit -m "feat: add help button to GPU panel"
```

---

### Task 5: Add help buttons to Visual Mode (Display) panel

**Files:**
- Modify: `app/GHelper.WPF/Views/Panels/VisualModePanel.xaml`

- [ ] **Step 1: Add help buttons next to Refresh Rate, Color Temp, Gamut, and Overdrive**

For each setting section in `VisualModePanel.xaml`, add a `HelpButton` in the header/label row:

Next to Refresh Rate header/label:
```xaml
<controls:HelpButton HelpKey="refresh_rate" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to Color Temperature label:
```xaml
<controls:HelpButton HelpKey="color_temp" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to Color Gamut label:
```xaml
<controls:HelpButton HelpKey="gamut" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to Overdrive toggle label:
```xaml
<controls:HelpButton HelpKey="overdrive" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Ensure the `controls` xmlns is declared.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Views/Panels/VisualModePanel.xaml
git commit -m "feat: add help buttons to Display panel"
```

---

### Task 6: Add help buttons to Settings panel

**Files:**
- Modify: `app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml`

- [ ] **Step 1: Add help buttons next to key settings**

Next to Fn Lock label:
```xaml
<controls:HelpButton HelpKey="fn_lock" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to Boot Sound label:
```xaml
<controls:HelpButton HelpKey="boot_sound" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to GPU Fix on Shutdown label:
```xaml
<controls:HelpButton HelpKey="gpu_fix" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Next to the ASUS Services section header ("ASUS Services" text):
```xaml
<controls:HelpButton HelpKey="asus_services" VerticalAlignment="Center" Margin="8,0,0,0" />
```

Ensure the `controls` xmlns is declared.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml
git commit -m "feat: add help buttons to Settings panel"
```

---

## Feature 2: Smart Auto-Configure

### Task 7: Create UsageAnalyzer service

**Files:**
- Create: `app/GHelper.WPF/Services/UsageAnalyzer.cs`

- [ ] **Step 1: Create the usage analysis service**

This service reads the log file and current system state to build a usage profile. It parses GPU usage percentages, battery/AC state history, and mode change patterns.

```csharp
// app/GHelper.WPF/Services/UsageAnalyzer.cs
using System.IO;
using System.Text.RegularExpressions;

namespace GHelper.WPF.Services
{
    public enum UsageProfile
    {
        LightMobile,     // Mostly on battery, low GPU usage (browsing, office)
        HeavyMobile,     // On battery but GPU-active (light gaming on the go)
        DesktopCasual,   // Plugged in, low-moderate GPU (office, media, coding)
        DesktopGaming,   // Plugged in, high GPU usage (gaming, rendering)
        Mixed,           // No clear pattern
    }

    public class UsageStats
    {
        public int TotalLogLines { get; set; }
        public int GpuReadings { get; set; }
        public double AvgGpuUsage { get; set; }
        public double MaxGpuUsage { get; set; }
        public int HighGpuReadings { get; set; }  // > 50%
        public double PercentOnBattery { get; set; }
        public UsageProfile Profile { get; set; }
        public string ProfileReason { get; set; } = "";
    }

    public class Recommendation
    {
        public required string SettingName { get; init; }
        public required string CurrentValue { get; init; }
        public required string RecommendedValue { get; init; }
        public required string Reason { get; init; }
        public required Action Apply { get; init; }
        public bool IsChange => CurrentValue != RecommendedValue;
    }

    public static class UsageAnalyzer
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GHelper", "log.txt");

        private static readonly Regex GpuUsageRegex = new(@"GPU usage:.*?(\d+)%", RegexOptions.Compiled);

        /// <summary>
        /// Analyze the log file and current system state to build a usage profile.
        /// Runs on a background thread — call from Task.Run.
        /// </summary>
        public static UsageStats Analyze()
        {
            var stats = new UsageStats();

            // Parse log file for GPU usage patterns
            if (File.Exists(LogPath))
            {
                try
                {
                    var lines = File.ReadAllLines(LogPath);
                    stats.TotalLogLines = lines.Length;

                    double gpuTotal = 0;
                    foreach (var line in lines)
                    {
                        var match = GpuUsageRegex.Match(line);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int usage))
                        {
                            stats.GpuReadings++;
                            gpuTotal += usage;
                            if (usage > stats.MaxGpuUsage) stats.MaxGpuUsage = usage;
                            if (usage > 50) stats.HighGpuReadings++;
                        }
                    }

                    if (stats.GpuReadings > 0)
                        stats.AvgGpuUsage = gpuTotal / stats.GpuReadings;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("UsageAnalyzer log parse error: " + ex.Message);
                }
            }

            // Current power state
            bool onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus
                != System.Windows.Forms.PowerLineStatus.Online;
            double batteryPercent = System.Windows.Forms.SystemInformation.PowerStatus.BatteryLifePercent * 100;

            // Estimate battery usage ratio from recent behavior
            // If currently on battery, weight toward mobile profile
            stats.PercentOnBattery = onBattery ? 70 : 20; // Simple heuristic for now

            // Determine profile
            stats.Profile = ClassifyProfile(stats, onBattery);
            stats.ProfileReason = ExplainProfile(stats, onBattery);

            return stats;
        }

        private static UsageProfile ClassifyProfile(UsageStats stats, bool onBattery)
        {
            double highGpuPercent = stats.GpuReadings > 0
                ? (double)stats.HighGpuReadings / stats.GpuReadings * 100
                : 0;

            if (onBattery && stats.AvgGpuUsage < 15)
                return UsageProfile.LightMobile;

            if (onBattery && stats.AvgGpuUsage >= 15)
                return UsageProfile.HeavyMobile;

            if (!onBattery && highGpuPercent > 30)
                return UsageProfile.DesktopGaming;

            if (!onBattery && stats.AvgGpuUsage < 20)
                return UsageProfile.DesktopCasual;

            return UsageProfile.Mixed;
        }

        private static string ExplainProfile(UsageStats stats, bool onBattery)
        {
            string power = onBattery ? "on battery" : "plugged in";
            string gpu = stats.GpuReadings > 0
                ? $"avg GPU usage {stats.AvgGpuUsage:F0}%"
                : "no GPU data yet";

            return stats.Profile switch
            {
                UsageProfile.LightMobile =>
                    $"You're {power} with {gpu}. Optimizing for battery life and quiet operation.",
                UsageProfile.HeavyMobile =>
                    $"You're {power} with {gpu}. Balancing performance with battery life.",
                UsageProfile.DesktopCasual =>
                    $"You're {power} with {gpu}. Keeping things quiet with good responsiveness.",
                UsageProfile.DesktopGaming =>
                    $"You're {power} with {gpu}. Maximizing performance for demanding workloads.",
                UsageProfile.Mixed =>
                    $"You're {power} with {gpu}. Applying balanced defaults.",
                _ => $"You're {power}. Applying balanced defaults."
            };
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Services/UsageAnalyzer.cs
git commit -m "feat: add UsageAnalyzer service for usage pattern detection"
```

---

### Task 8: Create AutoConfigService

**Files:**
- Create: `app/GHelper.WPF/Services/AutoConfigService.cs`

- [ ] **Step 1: Create the auto-configuration service**

Maps each `UsageProfile` to a set of concrete setting recommendations and generates `Recommendation` objects that the UI can display and apply.

```csharp
// app/GHelper.WPF/Services/AutoConfigService.cs
using GHelper.Gpu;
using GHelper.Mode;

namespace GHelper.WPF.Services
{
    public class AutoConfigResult
    {
        public UsageProfile Profile { get; init; }
        public string ProfileName { get; init; } = "";
        public string Reason { get; init; } = "";
        public List<Recommendation> Recommendations { get; init; } = new();
        public int ChangeCount => Recommendations.Count(r => r.IsChange);
    }

    public static class AutoConfigService
    {
        /// <summary>
        /// Analyze usage and generate recommendations. Call from Task.Run.
        /// </summary>
        public static AutoConfigResult GenerateRecommendations()
        {
            var stats = UsageAnalyzer.Analyze();

            var (perfMode, perfName) = GetRecommendedPerfMode(stats.Profile);
            var (gpuMode, gpuAuto, gpuName) = GetRecommendedGpuMode(stats.Profile);
            var (screenAuto, screenName) = GetRecommendedScreenMode(stats.Profile);

            // Current values
            int currentPerf = Modes.GetCurrent();
            string currentPerfName = currentPerf switch
            {
                AsusACPI.PerformanceSilent => "Silent",
                AsusACPI.PerformanceTurbo => "Turbo",
                _ => "Balanced"
            };

            int currentGpu = AppConfig.Get("gpu_mode");
            bool currentGpuAuto = AppConfig.Is("gpu_auto");
            string currentGpuName = currentGpu switch
            {
                AsusACPI.GPUModeEco => "Eco",
                AsusACPI.GPUModeUltimate => "Ultimate",
                _ => currentGpuAuto ? "Optimized" : "Standard"
            };

            bool currentScreenAuto = AppConfig.Is("screen_auto");
            string currentScreenName = currentScreenAuto ? "Auto" : "Manual";

            var recommendations = new List<Recommendation>
            {
                new()
                {
                    SettingName = "Performance Mode",
                    CurrentValue = currentPerfName,
                    RecommendedValue = perfName,
                    Reason = GetPerfReason(stats.Profile),
                    Apply = () =>
                    {
                        AppConfig.Set("performance_mode", perfMode);
                        Program.modeControl?.SetPerformanceMode(perfMode);
                    }
                },
                new()
                {
                    SettingName = "GPU Mode",
                    CurrentValue = currentGpuName,
                    RecommendedValue = gpuName,
                    Reason = GetGpuReason(stats.Profile),
                    Apply = () =>
                    {
                        AppConfig.Set("gpu_auto", gpuAuto ? 1 : 0);
                        AppConfig.Set("gpu_mode", gpuMode);
                        Program.gpuControl?.SetGPUMode(gpuMode, gpuAuto ? 1 : 0);
                    }
                },
                new()
                {
                    SettingName = "Screen Refresh",
                    CurrentValue = currentScreenName,
                    RecommendedValue = screenName,
                    Reason = GetScreenReason(stats.Profile),
                    Apply = () =>
                    {
                        AppConfig.Set("screen_auto", screenAuto ? 1 : 0);
                    }
                },
            };

            string profileName = stats.Profile switch
            {
                UsageProfile.LightMobile => "Battery Saver",
                UsageProfile.HeavyMobile => "Mobile Performance",
                UsageProfile.DesktopCasual => "Quiet Desktop",
                UsageProfile.DesktopGaming => "Maximum Performance",
                UsageProfile.Mixed => "Balanced",
                _ => "Balanced"
            };

            return new AutoConfigResult
            {
                Profile = stats.Profile,
                ProfileName = profileName,
                Reason = stats.ProfileReason,
                Recommendations = recommendations,
            };
        }

        /// <summary>
        /// Apply all recommendations that differ from current settings.
        /// </summary>
        public static int ApplyAll(AutoConfigResult result)
        {
            int applied = 0;
            foreach (var rec in result.Recommendations)
            {
                if (rec.IsChange)
                {
                    try
                    {
                        rec.Apply();
                        applied++;
                        Logger.WriteLine($"AutoConfig applied: {rec.SettingName} → {rec.RecommendedValue}");
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"AutoConfig failed: {rec.SettingName} — {ex.Message}");
                    }
                }
            }
            Logger.WriteLine($"AutoConfig complete: {applied} settings changed, profile={result.ProfileName}");
            return applied;
        }

        private static (int mode, string name) GetRecommendedPerfMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (AsusACPI.PerformanceSilent, "Silent"),
            UsageProfile.HeavyMobile => (AsusACPI.PerformanceBalanced, "Balanced"),
            UsageProfile.DesktopCasual => (AsusACPI.PerformanceBalanced, "Balanced"),
            UsageProfile.DesktopGaming => (AsusACPI.PerformanceTurbo, "Turbo"),
            _ => (AsusACPI.PerformanceBalanced, "Balanced"),
        };

        private static (int mode, bool auto, string name) GetRecommendedGpuMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (AsusACPI.GPUModeEco, false, "Eco"),
            UsageProfile.HeavyMobile => (AsusACPI.GPUModeStandard, true, "Optimized"),
            UsageProfile.DesktopCasual => (AsusACPI.GPUModeStandard, true, "Optimized"),
            UsageProfile.DesktopGaming => (AsusACPI.GPUModeStandard, false, "Standard"),
            _ => (AsusACPI.GPUModeStandard, true, "Optimized"),
        };

        private static (bool auto, string name) GetRecommendedScreenMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (true, "Auto"),
            UsageProfile.HeavyMobile => (true, "Auto"),
            UsageProfile.DesktopCasual => (true, "Auto"),
            UsageProfile.DesktopGaming => (false, "Manual"),
            _ => (true, "Auto"),
        };

        private static string GetPerfReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Minimizes fan noise and power draw on battery",
            UsageProfile.HeavyMobile => "Good balance of performance and battery life",
            UsageProfile.DesktopCasual => "Quiet operation with responsive performance",
            UsageProfile.DesktopGaming => "Maximum CPU/GPU clocks for demanding tasks",
            _ => "Good all-around default",
        };

        private static string GetGpuReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Disables dGPU to maximize battery life",
            UsageProfile.HeavyMobile => "Auto-switches GPU based on power source",
            UsageProfile.DesktopCasual => "Auto-switches to Eco on battery, Standard when plugged in",
            UsageProfile.DesktopGaming => "Keeps dGPU always available for gaming",
            _ => "Smart switching balances performance and battery",
        };

        private static string GetScreenReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Drops to 60Hz on battery to save power",
            UsageProfile.HeavyMobile => "Auto-adjusts refresh rate based on power source",
            UsageProfile.DesktopCasual => "Auto-adjusts for best experience",
            UsageProfile.DesktopGaming => "Keep manual control for maximum refresh rate",
            _ => "Auto-adjusts for best experience",
        };
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Services/AutoConfigService.cs
git commit -m "feat: add AutoConfigService with profile-based recommendations"
```

---

### Task 9: Add Optimize UI to ExtraSettingsViewModel

**Files:**
- Modify: `app/GHelper.WPF/ViewModels/ExtraSettingsViewModel.cs`

- [ ] **Step 1: Add properties and command for the Optimize card**

Add these properties after the existing update checker properties:

```csharp
[ObservableProperty]
private string _optimizeProfileName = "";

[ObservableProperty]
private string _optimizeReason = "";

[ObservableProperty]
private string _optimizeButtonText = "Optimize for Me";

[ObservableProperty]
private bool _optimizeButtonEnabled = true;

[ObservableProperty]
private bool _optimizeResultVisible;

[ObservableProperty]
private List<Recommendation> _optimizeRecommendations = new();

[ObservableProperty]
private int _optimizeChangeCount;
```

Add the command:

```csharp
[RelayCommand]
private async Task OptimizeForMe()
{
    OptimizeButtonEnabled = false;
    OptimizeButtonText = "Analyzing...";
    OptimizeResultVisible = false;

    var result = await Task.Run(() => AutoConfigService.GenerateRecommendations());

    OptimizeProfileName = result.ProfileName;
    OptimizeReason = result.Reason;
    OptimizeRecommendations = result.Recommendations;
    OptimizeChangeCount = result.ChangeCount;
    OptimizeResultVisible = true;

    if (result.ChangeCount > 0)
    {
        OptimizeButtonText = $"Apply {result.ChangeCount} Change{(result.ChangeCount == 1 ? "" : "s")}";
        OptimizeButtonEnabled = true;
    }
    else
    {
        OptimizeButtonText = "Already Optimal";
        OptimizeButtonEnabled = false;

        // Re-enable after 3 seconds
        await Task.Delay(3000);
        OptimizeButtonText = "Optimize for Me";
        OptimizeButtonEnabled = true;
        OptimizeResultVisible = false;
    }
}

[RelayCommand]
private async Task ApplyOptimization()
{
    OptimizeButtonEnabled = false;
    OptimizeButtonText = "Applying...";

    await Task.Run(() => AutoConfigService.ApplyAll(
        new AutoConfigResult
        {
            Profile = UsageProfile.Mixed,
            ProfileName = OptimizeProfileName,
            Reason = OptimizeReason,
            Recommendations = OptimizeRecommendations,
        }));

    OptimizeButtonText = "Done!";
    ToastService.Show($"Settings optimized: {OptimizeProfileName}", ToastType.Success);

    // Reset after delay
    await Task.Delay(2000);
    OptimizeButtonText = "Optimize for Me";
    OptimizeButtonEnabled = true;
    OptimizeResultVisible = false;

    // Refresh other settings that may have changed
    _ignoreChange = true;
    try
    {
        RunOnStartup = Startup.IsScheduled();
    }
    finally { _ignoreChange = false; }
}
```

Note: The `OptimizeForMe` command first analyzes, then changes the button to "Apply N Changes". A second click calls `ApplyOptimization`. To handle this two-phase flow, modify `OptimizeForMe` to check if recommendations are already showing:

Replace the `OptimizeForMe` command with this logic:

```csharp
[RelayCommand]
private async Task OptimizeForMe()
{
    // If recommendations are showing, apply them
    if (OptimizeResultVisible && OptimizeChangeCount > 0)
    {
        OptimizeButtonEnabled = false;
        OptimizeButtonText = "Applying...";

        await Task.Run(() =>
        {
            foreach (var rec in OptimizeRecommendations.Where(r => r.IsChange))
            {
                try
                {
                    rec.Apply();
                    Logger.WriteLine($"AutoConfig applied: {rec.SettingName} → {rec.RecommendedValue}");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"AutoConfig failed: {rec.SettingName} — {ex.Message}");
                }
            }
        });

        OptimizeButtonText = "Done!";
        ToastService.Show($"Settings optimized: {OptimizeProfileName}", ToastType.Success);

        await Task.Delay(2000);
        OptimizeButtonText = "Optimize for Me";
        OptimizeButtonEnabled = true;
        OptimizeResultVisible = false;
        return;
    }

    // First click: analyze
    OptimizeButtonEnabled = false;
    OptimizeButtonText = "Analyzing...";
    OptimizeResultVisible = false;

    var result = await Task.Run(() => AutoConfigService.GenerateRecommendations());

    OptimizeProfileName = result.ProfileName;
    OptimizeReason = result.Reason;
    OptimizeRecommendations = result.Recommendations;
    OptimizeChangeCount = result.ChangeCount;
    OptimizeResultVisible = true;

    if (result.ChangeCount > 0)
    {
        OptimizeButtonText = $"Apply {result.ChangeCount} Change{(result.ChangeCount == 1 ? "" : "s")}";
        OptimizeButtonEnabled = true;
    }
    else
    {
        OptimizeButtonText = "Already Optimal";
        await Task.Delay(3000);
        OptimizeButtonText = "Optimize for Me";
        OptimizeButtonEnabled = true;
        OptimizeResultVisible = false;
    }
}
```

You can remove the separate `ApplyOptimization` command — the single `OptimizeForMe` handles both phases.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/ViewModels/ExtraSettingsViewModel.cs
git commit -m "feat: add Optimize for Me command and properties to ExtraSettingsViewModel"
```

---

### Task 10: Add Optimize for Me card to Settings panel

**Files:**
- Modify: `app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml`

- [ ] **Step 1: Add the Optimize card above the ASUS Services section**

Insert this XAML block before the `<!-- Stop Armory Crate toggle -->` section. The card has: a header with lightning icon, profile badge, reasoning text, recommendation list, and the action button.

```xaml
                    <!-- Optimize for Me -->
                    <Border Background="#10FFFFFF" CornerRadius="8" Padding="14,12" Margin="0,0,0,12">
                        <StackPanel>
                            <!-- Header -->
                            <Grid Margin="0,0,0,8">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text="&#xE945;" FontFamily="Segoe MDL2 Assets" FontSize="14"
                                               Foreground="{DynamicResource AccentBrush}"
                                               VerticalAlignment="Center" Margin="0,0,8,0" />
                                    <TextBlock Text="Smart Optimize"
                                               Style="{StaticResource BodyTextStyle}"
                                               FontWeight="SemiBold" VerticalAlignment="Center" />
                                </StackPanel>
                                <Button Grid.Column="1"
                                        Content="{Binding OptimizeButtonText}"
                                        Command="{Binding OptimizeForMeCommand}"
                                        IsEnabled="{Binding OptimizeButtonEnabled}"
                                        Background="#20FFFFFF" Foreground="White"
                                        BorderThickness="0" Padding="16,5"
                                        FontSize="11" Cursor="Hand">
                                    <Button.Resources>
                                        <Style TargetType="Border">
                                            <Setter Property="CornerRadius" Value="4" />
                                        </Style>
                                    </Button.Resources>
                                </Button>
                            </Grid>

                            <TextBlock Text="Analyzes your usage patterns and sets optimal performance, GPU, and display settings."
                                       Style="{StaticResource DimTextStyle}" FontSize="10"
                                       TextWrapping="Wrap" Margin="0,0,0,4" />

                            <!-- Results (shown after analysis) -->
                            <StackPanel Margin="0,6,0,0">
                                <StackPanel.Style>
                                    <Style TargetType="StackPanel">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding OptimizeResultVisible}" Value="True">
                                                <Setter Property="Visibility" Value="Visible" />
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </StackPanel.Style>

                                <!-- Profile badge -->
                                <Border Background="#18FFFFFF" CornerRadius="4" Padding="8,4" Margin="0,0,0,6"
                                        HorizontalAlignment="Left">
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock Text="&#xE771;" FontFamily="Segoe MDL2 Assets" FontSize="10"
                                                   Foreground="{DynamicResource AccentBrush}"
                                                   VerticalAlignment="Center" Margin="0,0,6,0" />
                                        <TextBlock Text="{Binding OptimizeProfileName}"
                                                   FontSize="11" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource AccentBrush}"
                                                   VerticalAlignment="Center" />
                                    </StackPanel>
                                </Border>

                                <!-- Reasoning -->
                                <TextBlock Text="{Binding OptimizeReason}"
                                           FontSize="10" Foreground="#80FFFFFF"
                                           TextWrapping="Wrap" Margin="0,0,0,8" />

                                <!-- Recommendation list -->
                                <ItemsControl ItemsSource="{Binding OptimizeRecommendations}">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Grid Margin="0,2">
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition Width="Auto" />
                                                    <ColumnDefinition Width="*" />
                                                    <ColumnDefinition Width="Auto" />
                                                </Grid.ColumnDefinitions>
                                                <TextBlock FontFamily="Segoe MDL2 Assets" FontSize="8"
                                                           VerticalAlignment="Center" Margin="0,0,8,0">
                                                    <TextBlock.Style>
                                                        <Style TargetType="TextBlock">
                                                            <Setter Property="Text" Value="&#xE73E;" />
                                                            <Setter Property="Foreground" Value="#4CC95E" />
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding IsChange}" Value="True">
                                                                    <Setter Property="Text" Value="&#xE72C;" />
                                                                    <Setter Property="Foreground" Value="#60CDFF" />
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </TextBlock.Style>
                                                </TextBlock>
                                                <StackPanel Grid.Column="1">
                                                    <TextBlock Text="{Binding SettingName}"
                                                               FontSize="11" Foreground="#DDFFFFFF" />
                                                    <TextBlock Text="{Binding Reason}"
                                                               FontSize="9" Foreground="#60FFFFFF" />
                                                </StackPanel>
                                                <TextBlock Grid.Column="2" FontSize="10"
                                                           VerticalAlignment="Center" Margin="8,0,0,0">
                                                    <TextBlock.Style>
                                                        <Style TargetType="TextBlock">
                                                            <Setter Property="Text" Value="{Binding RecommendedValue}" />
                                                            <Setter Property="Foreground" Value="#4CC95E" />
                                                            <Style.Triggers>
                                                                <DataTrigger Binding="{Binding IsChange}" Value="True">
                                                                    <Setter Property="Foreground" Value="#60CDFF" />
                                                                </DataTrigger>
                                                            </Style.Triggers>
                                                        </Style>
                                                    </TextBlock.Style>
                                                </TextBlock>
                                            </Grid>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build app/GHelper.WPF/GHelper.WPF.csproj -c Release`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add app/GHelper.WPF/Views/Panels/ExtraSettingsPanel.xaml
git commit -m "feat: add Optimize for Me card to Settings panel"
```

---

## Self-Review Checklist

1. **Spec coverage:**
   - Help system: HelpContent dictionary, HelpButton control, placed on Performance, GPU, Display, Settings panels
   - Auto-configure: UsageAnalyzer (log parsing, profile classification), AutoConfigService (recommendations, apply), ViewModel integration, XAML card with results display
   - Both features covered

2. **Placeholder scan:** All code blocks are complete. No TBD/TODO. All methods have implementations.

3. **Type consistency:**
   - `HelpEntry` used in `HelpContent.cs` and consumed in `HelpButton.cs`
   - `UsageStats`, `UsageProfile`, `Recommendation` used consistently across `UsageAnalyzer` → `AutoConfigService` → `ExtraSettingsViewModel`
   - `HelpKey` string property matches dictionary keys in `HelpContent.Entries`

---
