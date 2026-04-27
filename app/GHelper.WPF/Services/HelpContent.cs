namespace GHelper.WPF.Services
{
    public record HelpEntry(string Title, string Description);

    public static class HelpContent
    {
        public static readonly Dictionary<string, HelpEntry> Entries = new()
        {
            ["perf_mode"] = new("Performance Modes",
                "Silent: the quietest option. The CPU and GPU run at reduced clocks so your fans barely spin. Great for browsing, reading, and squeezing extra battery life out of a long day.\n\n" +
                "Balanced: the everyday default. Fans stay quiet at idle and only ramp up when something really needs the horsepower. A good all-rounder for mixed work where you don't want to think about it.\n\n" +
                "Turbo: full send. Fans run hard to keep thermals in check while the CPU and GPU push their limits. Pick this for gaming, rendering, or anything that wants every watt."),

            ["fan_control"] = new("Fan & Power Control",
                "Auto: let G-Aether handle fans and power limits based on your performance mode. If you're not sure what to pick, this is it.\n\n" +
                "Manual Fans: shape your own fan curves for the CPU and GPU. Drag the points on the curve to decide how fast the fans spin at each temperature.\n\n" +
                "Manual Power: override the default CPU and GPU power limits (TDP). Higher numbers mean more performance and more heat.\n\n" +
                "Manual Both: take the wheel on fan curves and power limits at once."),

            ["gpu_mode"] = new("GPU Modes",
                "Eco: turns off the dedicated GPU entirely and runs on integrated graphics only. Easiest on the battery, but it rules out gaming and anything GPU-accelerated.\n\n" +
                "Standard: keeps both the iGPU and dGPU available, letting Windows decide which one to use moment to moment. This is the normal mode for most people.\n\n" +
                "Ultimate: sends the display straight through the dGPU via the MUX switch. You get maximum FPS in games because the iGPU is out of the pipeline. Needs a restart to take effect.\n\n" +
                "Optimized: behaves like Standard while you're plugged in, and switches to Eco the moment you unplug. Set it and forget it."),

            ["refresh_rate"] = new("Screen Refresh Rate",
                "Higher refresh rates like 144Hz or 240Hz make motion look smoother. You'll notice it most while gaming and scrolling. Lower rates like 60Hz or 120Hz save a meaningful chunk of battery.\n\n" +
                "Auto: let G-Aether pick for you based on whether you're plugged in or running on battery."),

            ["color_temp"] = new("Color Temperature",
                "Shifts the warmth of your display. Warmer, yellow-ish tones are easier on the eyes at night. Cooler, blue-ish tones feel more neutral during the day.\n\n" +
                "This doesn't affect color accuracy for design work. For that, reach for the Gamut setting below."),

            ["gamut"] = new("Color Gamut",
                "Controls the color space your display uses.\n\n" +
                "Native: the full range your panel can show. Colors pop, though web content can look a bit oversaturated.\n\n" +
                "sRGB: the standard web and office color space. Most accurate for day-to-day use.\n\n" +
                "DCI-P3: a wide gamut used in film and HDR. Great for watching movies and streaming shows."),

            ["overdrive"] = new("Panel Overdrive",
                "Speeds up how quickly pixels change color, cutting down motion blur and ghosting. Some panels can overshoot a little. If you notice bright trails behind moving objects (inverse ghosting), turn this off."),

            ["fn_lock"] = new("Fn Lock",
                "When on, F1-F12 act as standard function keys and you hold Fn to reach the media and brightness shortcuts. When off, the shortcuts are the default and you hold Fn to get plain F1-F12."),

            ["gpu_fix"] = new("GPU Fix on Shutdown",
                "Forces the dedicated GPU back to Standard mode before shutdown or hibernate. A few laptops have trouble waking from sleep when the GPU was last in Eco. If that's happened to you, turn this on."),

            ["boot_sound"] = new("Boot Sound",
                "The ASUS POST beep that plays when you power on the laptop. Turn it off if you'd rather start up silently."),

            ["asus_services"] = new("ASUS Services",
                "ASUS installs background services like Armoury Crate, optimization agents, and telemetry. They eat RAM and CPU, and sometimes fight with G-Aether over hardware control.\n\n" +
                "G-Aether covers everything those services do, so stopping them frees up resources and prevents conflicts. When any of them are running, the Settings icon shows an orange dot so you know."),

            ["charge_limit"] = new("Battery Charge Limit",
                "Caps how much your battery charges, which helps it stay healthy over the long haul. Lithium cells wear faster when they're parked at 100% all the time. 80% is a great everyday setting. Bump it up to 100% for travel when you need every last minute."),

            ["battery_health"] = new("Battery Health",
                "Shows how much of its original design capacity your battery can still hold. A brand new battery reads 100%.\n\n" +
                "Every full charge stores a tiny bit less than the one before it, so this number slowly falls as the battery ages. Once you dip below roughly 80%, you'll feel the difference between a charge now and what the laptop could do when it was new.\n\n" +
                "The single most impactful thing you can do to slow this decline is keep the charge limit below 100% (see above)."),

            ["monitor_telemetry"] = new("G-Scope",
                "Real-time readings straight from the hardware sensors. Values refresh every second; the sparkline charts default to the last 60 seconds and can be extended up to 15 minutes via the gear icon next to this help button — useful for spotting slow trends, not just spikes."),

            ["live_sensors"] = new("Live Sensors",
                "Current CPU and GPU temperatures, plus live fan RPM. These numbers come directly from the hardware, so they're handy for checking that a mode change or a fan curve is doing what you expected."),

            ["fan_stop_on_idle"] = new("Fan Stop on Idle",
                "True passive cooling. When both the CPU and GPU are below 62°C, G-Aether drops the fan curve to 0 RPM so your laptop runs in complete silence. Once either component crosses 72°C, the fans pick up their normal mode-driven behavior again.\n\n" +
                "The 10°C gap (on below 62°C, off above 72°C) keeps the fans from flipping back and forth right at the threshold. The numbers are tuned for how ROG laptops actually idle. Silent mode usually sits in the high 50s, so a lower entry point would basically never fire.\n\n" +
                "Safe by design: the idle curve isn't 0% the whole way. It holds at 0% up to 72°C and then ramps to 100% by 95°C. Even if G-Aether crashes or hangs while the fans are stopped, the hardware curve will spin them back up as the temperature climbs. You won't end up with fans pinned off under a real load.\n\n" +
                "Non-destructive: your custom fan curves are never touched. Fan Stop lays on top as a temporary overlay, and the moment temps rise your saved curves come right back.\n\n" +
                "Off by default. Worth trying on most ROG laptops. You might be surprised how often the fans don't actually need to be spinning."),

            ["scheduled_tasks"] = new("Scheduled Tasks",
                "Windows Task Scheduler boot and logon tasks. This is another popular hiding spot for autostart bloat.\n\n" +
                "OEM helpers from Adobe, Razer, Logitech, NVIDIA, GameBar, Zoom, Creative Cloud, and plenty of others often register Task Scheduler tasks instead of Run keys, specifically because those don't show up in Task Manager's Startup tab. Anything in that category is fair game to review here.\n\n" +
                "What gets filtered out:\n" +
                "• Every task under \\Microsoft\\ (Windows Update, driver housekeeping, telemetry cleanup, system maintenance). Breaking these breaks Windows.\n" +
                "• Tasks explicitly authored by Microsoft, even outside that tree.\n" +
                "• Hidden tasks.\n" +
                "• Tasks without a boot or logon trigger.\n" +
                "• G-Aether's own tasks. Manage those from Settings, under Run on Startup.\n\n" +
                "The toggle flips the task's Enabled flag through the standard TaskScheduler API, the same way taskschd.msc does it. Disabling never deletes the task, so turning it back on restores it exactly. If you want full control like editing triggers or seeing hidden tasks, taskschd.msc is the place for that."),

            ["startup_entries"] = new("Startup Apps",
                "Apps set to launch automatically when you sign in.\n\n" +
                "G-Aether checks four places for these:\n" +
                "• Registry (user): your personal Run key, where most consumer apps register themselves.\n" +
                "• Registry (system): the all-users Run key. Toggling this one requires admin.\n" +
                "• Startup folder (user): shortcuts in your personal Startup folder.\n" +
                "• Startup folder (system): shortcuts in the all-users Startup folder.\n\n" +
                "Disable and Enable use Windows' own StartupApproved mechanism, the same one Task Manager and the Settings app's Startup page rely on. It flips a single byte of state and never deletes your entry, so turning it back on restores the original exactly.\n\n" +
                "Task Scheduler boot and logon tasks, Windows services, and RunOnce entries aren't listed here. Each of those deserves its own dedicated review, so they live on their own surfaces."),

            ["processes"] = new("Processes",
                "Running apps with a visible window, sorted by how much of your machine they're using right now.\n\n" +
                "Click a column header to sort by Name, CPU %, or RAM. Click it again to reverse the order. The X on any row kills that process and its children.\n\n" +
                "G-Aether only lists processes that own a top-level window. Services, background helpers, and hidden workers belong in Task Manager.\n\n" +
                "The list auto-refreshes every 2 seconds while you're on this page. Switching away pauses the refresh, so we don't waste cycles enumerating processes nobody is looking at."),

            ["services"] = new("Services",
                "Non-Microsoft Windows services that launch with the OS.\n\n" +
                "G-Aether reads the service list through WMI and filters out anything whose binary lives under %SystemRoot%\\, which is almost always Windows itself. Services already set to Disabled are hidden too, since re-enabling a disabled service is more of a services.msc power move than a triage action.\n\n" +
                "What you can do here:\n\n" +
                "• Flip a service from Automatic to Manual to stop it launching with Windows. The service still works. It just won't run until you or another app asks for it.\n\n" +
                "• Stop a running service right now, same as the Stop button in services.msc. If the service has dependents or Windows refuses, the attempt fails cleanly and the row stays running.\n\n" +
                "What we won't do:\n\n" +
                "• Set a service to Disabled. That's an easy way to brick a vendor install or lock yourself out of an app that auto-starts its helper on launch. If you really want Disabled, services.msc is one click away.\n\n" +
                "• Delete or reconfigure anything beyond the start mode. Your install stays intact. This is purely a toggle for whether a service runs at boot."),

            ["global_hotkeys"] = new("Global Hotkeys",
                "Keyboard shortcuts that fire from anywhere in Windows, including inside games and full-screen apps.\n\n" +
                "Click Set on an action, then press your chosen combination. At least one modifier (Ctrl, Alt, Shift, or Win) is required. Bare letters aren't allowed, because they'd swallow that key globally.\n\n" +
                "If another app has already claimed the combination you picked, G-Aether shows \"Combo in use by another app\" next to the action. Pick something else, since no two apps can own the same global combo.\n\n" +
                "Your hotkeys are saved across launches and re-registered automatically on startup."),

            ["app_profiles"] = new("App Profiles",
                "Automatically apply a scene when a specific app takes focus.\n\n" +
                "Each rule is a simple pair: a process name like \"blender\" or \"chrome\", plus a scene from the footer strip (Reading, Focus, Present, Night, or Game).\n\n" +
                "How it works:\n\n" +
                "• When you switch TO a matching app, G-Aether snapshots your current Perf, GPU, and Refresh settings, then applies the rule's scene.\n\n" +
                "• When you switch AWAY to an unprofiled app, that snapshot comes back. That way, per-app profiles never leave you stuck in Turbo after closing Blender.\n\n" +
                "• Switching from one profiled app to another swaps in the new scene without touching the original snapshot. The baseline stays as whatever you had before the first trigger fired.\n\n" +
                "Your rules persist across launches. Remove one with the X on its row."),

            ["floating_gadget"] = new("Floating Gadget",
                "A compact, always-on-top window that shows live CPU and dGPU readings while you're doing other things. Drag it anywhere on screen and it stays put across launches.\n\n" +
                "It draws on top of borderless-windowed games (which is most of them in 2026), so you can keep an eye on temps, usage, and fan RPM during a session. True fullscreen-exclusive games will hide it, just like they hide most overlays.\n\n" +
                "Click Configure to open the full settings page: pick which tiles show, change the size or accent color, set a keyboard shortcut to toggle visibility from anywhere in Windows, and snap the gadget back to the corner if it ever ends up off-screen."),

            ["battery_triggers"] = new("Battery Saver Automation",
                "Opt-in, hands-off power saving while you're running on battery.\n\n" +
                "When enabled, two things happen automatically:\n\n" +
                "At 20%: G-Aether applies the Focus scene (Silent performance with an Eco GPU) and shows an OSD. The fans quiet down and the GPU draws less, stretching the rest of your battery as far as it'll go.\n\n" +
                "At 10%: an urgent on-screen warning asks you to save your work.\n\n" +
                "Nothing fires while you're plugged in. Each threshold fires at most once per low-battery cycle and rearms once the battery climbs back above a recovery level. G-Aether won't automatically revert your modes when you plug back in. That's always your call."),
        };

        public static HelpEntry? Get(string key) =>
            Entries.TryGetValue(key, out var entry) ? entry : null;
    }
}
