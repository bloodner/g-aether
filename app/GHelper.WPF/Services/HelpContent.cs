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

            ["battery_health"] = new("Battery Health",
                "The ratio of your battery's current maximum capacity to its original design capacity. A new battery is 100%.\n\n" +
                "As the battery ages, this percentage drops — each full charge holds slightly less energy than the previous one. Below about 80%, you'll notice battery life per charge is meaningfully shorter than when the laptop was new.\n\n" +
                "Keeping the charge limit under 100% (see above) is the single biggest thing you can do to slow this decline."),

            ["monitor_telemetry"] = new("Live Telemetry",
                "Real-time readings straight from the hardware sensors. Values refresh every 2 seconds; sparkline charts show the last ~30 seconds of history so you can spot spikes and trends at a glance."),

            ["live_sensors"] = new("Live Sensors",
                "Current CPU and GPU temperatures and fan RPM. These reflect the hardware's actual running state — useful for checking that a mode change or fan curve is producing the behavior you expect."),

            ["fan_stop_on_idle"] = new("Fan Stop on Idle",
                "Genuine passive cooling. When both CPU and GPU are below 62°C, G-Aether applies a 0-RPM fan curve so your laptop runs in complete silence. When either component heats up past 72°C, the fans resume normal mode-driven behavior.\n\n" +
                "The 10°C hysteresis gap (on at <62°C, off at >72°C) prevents fans from flapping on/off at the threshold. Thresholds are tuned for realistic ROG idle temps — Silent-mode idle typically sits in the high 50s, so a lower entry point would effectively never fire.\n\n" +
                "Safety: the \"idle\" curve isn't 0% everywhere — it's 0% up to 72°C, then ramps to 100% by 95°C. Even if G-Aether crashes or hangs while fans are stopped, the hardware curve will spin them up automatically as temperatures climb. Your laptop can't end up with fans pinned off under load.\n\n" +
                "Non-destructive: your customized fan curves are never edited. Fan Stop applies as a temporary overlay; when temps rise, your saved curves come right back.\n\n" +
                "Default off. Worth trying on most ROG laptops — you'd be surprised how often the fans don't actually need to be spinning."),

            ["scheduled_tasks"] = new("Scheduled Tasks",
                "Windows Task Scheduler boot and logon tasks — another popular hiding spot for autostart bloat.\n\n" +
                "OEM-bundled helpers from Adobe, Razer, Logitech, NVIDIA, GameBar, Zoom, Creative Cloud, and many others often register Task Scheduler tasks instead of Run keys specifically because those don't appear in Task Manager's Startup tab. Everything in that category is fair game to review here.\n\n" +
                "What's filtered out:\n" +
                "• Every task under \\Microsoft\\ (Windows Update, driver housekeeping, telemetry cleanup, system maintenance — breaking these breaks Windows)\n" +
                "• Tasks explicitly authored by Microsoft, even outside that tree\n" +
                "• Hidden tasks\n" +
                "• Tasks without a boot or logon trigger\n" +
                "• G-Aether's own tasks (manage those from Settings → Run on Startup)\n\n" +
                "Toggle flips the task's Enabled flag via the standard TaskScheduler API — same mechanism taskschd.msc uses. Disabling never deletes the task; re-enabling restores it exactly. If you want full control (see hidden tasks, edit triggers, etc.), use taskschd.msc."),

            ["startup_entries"] = new("Startup Apps",
                "Apps configured to launch automatically when you sign in.\n\n" +
                "G-Aether scans four sources:\n" +
                "• Registry (user) — your personal Run key, where most consumer apps register\n" +
                "• Registry (system) — the all-users Run key (toggling requires admin)\n" +
                "• Startup folder (user) — shortcuts in your personal Startup folder\n" +
                "• Startup folder (system) — shortcuts in the all-users Startup folder\n\n" +
                "Disable / Enable uses Windows' own StartupApproved mechanism — the same one Task Manager and Settings → Apps → Startup use. It flips a single byte of state, never deletes your entry, so re-enabling restores the original exactly.\n\n" +
                "Task Scheduler boot/logon tasks, Windows services, and RunOnce entries are not listed — those need their own surfaces and deserve dedicated review."),

            ["processes"] = new("Processes",
                "Running apps with a visible window, sorted by how much they're using right now.\n\n" +
                "Click a column header to sort by Name, CPU %, or RAM. Click again to reverse. The X on any row kills that process and its children.\n\n" +
                "G-Aether only shows processes that own a top-level window — services, background helpers, and hidden workers live in Task Manager.\n\n" +
                "Auto-refreshes every 2 seconds while you're on this page. Switching away pauses the refresh so we don't waste cycles enumerating when you're not looking."),

            ["services"] = new("Services",
                "Non-Microsoft Windows services that launch with the OS.\n\n" +
                "G-Aether reads the service list via WMI and filters out anything whose binary lives under %SystemRoot%\\ (almost always Windows itself). Services with Start=Disabled are hidden too — re-enabling a disabled service is a services.msc power move, not a triage action.\n\n" +
                "What you can do:\n\n" +
                "• Flip a service from Automatic to Manual to stop it launching with Windows. The service still works — it just won't run until something (you or another app) starts it on demand.\n\n" +
                "• Stop a running service now. Same as services.msc's Stop button. If the service has dependents or Windows refuses, the attempt fails cleanly and the row stays running.\n\n" +
                "What we never do:\n\n" +
                "• Set a service to Disabled. Too easy to brick a vendor install or lock yourself out of an app that auto-starts its helper on launch. If you really want Disabled, services.msc is one click away.\n\n" +
                "• Delete or reconfigure beyond start mode. Your install stays intact — this is purely a Run-at-boot toggle."),

            ["global_hotkeys"] = new("Global Hotkeys",
                "System-wide keyboard shortcuts that fire from anywhere in Windows — including inside games and full-screen apps.\n\n" +
                "Click Set on an action, then press your chosen combination. You must include at least one modifier (Ctrl, Alt, Shift, or Win) — bare letters aren't allowed (they'd swallow that key globally).\n\n" +
                "If another app has already claimed your combination, G-Aether will surface \"Combo in use by another app\" next to the action. Pick something else — no two apps can own the same global combo.\n\n" +
                "Hotkeys are saved across launches and registered again automatically on startup."),

            ["app_profiles"] = new("App Profiles",
                "Automatically apply a scene when a specific app takes focus.\n\n" +
                "Each rule is a simple pair: a process name (like \"blender\" or \"chrome\") and a scene from the footer strip (Reading, Focus, Present, Night, or Game).\n\n" +
                "How it works:\n\n" +
                "• When you switch TO a matching app, G-Aether snapshots your current Perf / GPU / Refresh settings, then applies the rule's scene.\n\n" +
                "• When you switch AWAY to an unprofiled app, your snapshot is restored — so per-app profiles never leave you stuck in Turbo after closing Blender.\n\n" +
                "• Switching from one profiled app to another applies the new scene without touching the original snapshot — the baseline is whatever you had before the first trigger.\n\n" +
                "Rules persist across launches. Remove a rule with the X on its row."),

            ["battery_triggers"] = new("Battery Saver Automation",
                "Opt-in, hands-off power saving while on battery.\n\n" +
                "When enabled, two things happen:\n\n" +
                "At 20% — G-Aether applies the Focus scene (Silent performance + Eco GPU) and shows an OSD. This quiets the fans and cuts GPU power draw so your remaining battery lasts longer.\n\n" +
                "At 10% — an urgent on-screen warning prompts you to save your work.\n\n" +
                "Nothing happens while the laptop is plugged in. Each threshold fires at most once per low-battery cycle — once the battery rises back above a recovery level, the trigger rearms. G-Aether does NOT automatically revert your modes when you plug back in; that's always your call."),
        };

        public static HelpEntry? Get(string key) =>
            Entries.TryGetValue(key, out var entry) ? entry : null;
    }
}
