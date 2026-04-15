namespace GHelper.WPF.Services
{
    public record HelpEntry(string Title, string Description);

    public static class HelpContent
    {
        public static readonly Dictionary<string, HelpEntry> Entries = new()
        {
            ["perf_mode"] = new("Performance Modes",
                "Silent — Lowest fan noise and power draw. Best for quiet work, browsing, and battery life. CPU and GPU run at reduced clocks.\n\n" +
                "Balanced — Default mode. Fans ramp up under load but stay quiet at idle. Good all-around for mixed workloads.\n\n" +
                "Turbo — Maximum performance. Fans run aggressively to keep thermals in check. Use for gaming, rendering, or heavy multitasking."),

            ["fan_control"] = new("Fan & Power Control",
                "Auto — G-Aether manages fans and power limits automatically based on your performance mode.\n\n" +
                "Manual Fans — Set custom fan curves for CPU and GPU. Drag points on the curve to adjust fan speed at different temperatures.\n\n" +
                "Manual Power — Override CPU/GPU power limits (TDP). Higher values = more performance but more heat.\n\n" +
                "Manual Both — Full control over both fan curves and power limits."),

            ["gpu_mode"] = new("GPU Modes",
                "Eco — Disables the dedicated GPU entirely. The laptop runs on integrated graphics only. Best battery life, but no gaming or GPU-accelerated tasks.\n\n" +
                "Standard — Both iGPU and dGPU are available. The system switches automatically based on demand. Normal mode for most users.\n\n" +
                "Ultimate — Routes display directly through the dGPU (MUX switch). Eliminates iGPU bottleneck for maximum gaming FPS. Requires restart.\n\n" +
                "Optimized — Like Standard, but automatically switches to Eco when on battery and back to Standard when plugged in."),

            ["refresh_rate"] = new("Screen Refresh Rate",
                "Higher refresh rates (144Hz, 240Hz) make motion smoother — great for gaming and scrolling. Lower rates (60Hz, 120Hz) save significant battery life.\n\n" +
                "Auto — G-Aether picks the best rate based on whether you're plugged in or on battery."),

            ["color_temp"] = new("Color Temperature",
                "Adjusts the warmth of your display. Warmer (yellow-ish) tones reduce eye strain at night. Cooler (blue-ish) tones are more accurate for color work.\n\n" +
                "This does not affect color accuracy for design work — use the Gamut setting for that."),

            ["gamut"] = new("Color Gamut",
                "Controls the color space your display uses.\n\n" +
                "Native — Full display capability, widest colors. May look oversaturated for web content.\n\n" +
                "sRGB — Standard web/office color space. Most accurate for general use.\n\n" +
                "DCI-P3 — Wide gamut used in film and HDR content. Good for media consumption."),

            ["overdrive"] = new("Panel Overdrive",
                "Speeds up pixel response time to reduce motion blur and ghosting. May cause slight overshoot artifacts on some panels. Disable if you notice inverse ghosting (bright trails behind moving objects)."),

            ["fn_lock"] = new("Fn Lock",
                "When enabled, F1-F12 keys act as standard function keys by default (you hold Fn to access media/brightness). When disabled, the special functions are the default and you hold Fn for F1-F12."),

            ["gpu_fix"] = new("GPU Fix on Shutdown",
                "Forces the dedicated GPU to Standard mode before shutdown or hibernate. Prevents a rare issue where some laptops fail to wake from sleep when the GPU was in Eco mode. Enable if you experience wake-from-sleep problems."),

            ["boot_sound"] = new("Boot Sound",
                "The ASUS POST beep that plays when you power on the laptop. Disable to start up silently."),

            ["asus_services"] = new("ASUS Services",
                "ASUS installs background services (Armoury Crate, optimization agents, telemetry) that consume RAM, CPU, and sometimes conflict with G-Aether.\n\n" +
                "G-Aether replaces all of their functionality. Stopping them frees resources and prevents conflicts. The orange dot on the Settings icon warns you when they're running."),

            ["charge_limit"] = new("Battery Charge Limit",
                "Limits the maximum battery charge to extend long-term battery health. Lithium batteries degrade faster when kept at 100%. Set to 80% for daily use, or 100% only when you need full capacity for travel."),
        };

        public static HelpEntry? Get(string key) =>
            Entries.TryGetValue(key, out var entry) ? entry : null;
    }
}
