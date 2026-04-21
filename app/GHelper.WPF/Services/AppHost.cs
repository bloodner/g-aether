using GHelper.Display;
using GHelper.Gpu;
using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using PawnIO;

namespace GHelper.WPF.Services
{
    public static class AppHost
    {
        private static System.Timers.Timer? _sensorTimer;

        public static void Initialize()
        {
            ProcessHelper.CheckAlreadyRunning();
            ProcessHelper.SetPriority();

            Logger.WriteLine("------------");
            Logger.WriteLine("WPF App launched: " + AppConfig.GetModel());

            // Initialize ACPI connection (fast — creates a COM-style handle)
            Program.acpi = new AsusACPI();

            // Initialize controllers (without SettingsForm — WPF uses ViewModels)
            Program.modeControl = new ModeControl();
            Program.gpuControl = new GPUModeControl();

            // Initialize toast (required by InputDispatcher actions that call Program.toast)
            Program.toast = new ToastForm();

            // Initialize input dispatcher for special key handling
            Program.inputDispatcher = new InputDispatcher();
            Program.inputDispatcher.Init();

            // Heavy GPU driver enumeration (NvAPI/AMD) — push to background so the window
            // can render immediately. MainViewModel re-checks GPU state after this completes.
            Task.Run(() =>
            {
                try
                {
                    HardwareControl.RecreateGpuControl();
                    // PawnIO replaces old RyzenControl — no explicit Init needed
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Background GPU init error: " + ex.Message);
                }
            });

            // Refresh the "Run on Startup" scheduled task if its stored path or version
            // is stale relative to the running exe — covers the in-place update case.
            Task.Run(() =>
            {
                try { Startup.StartupCheck(); }
                catch (Exception ex) { Logger.WriteLine("StartupCheck error: " + ex.Message); }
            });

            // Background update check — throttled internally to run at most once per 24h.
            Task.Run(UpdateBackgroundCheck.RunAsync);

            // Per-app profile automation — subscribes to foreground-window events
            // and applies scenes when rule-matched apps take focus.
            try { AppProfileService.Initialize(); }
            catch (Exception ex) { Logger.WriteLine("AppProfileService init error: " + ex.Message); }

            // Fan Stop on Idle — watchdog that commands a passive-cooling curve
            // while the system is cool. No-op when the user hasn't opted in.
            try { FanStopService.Initialize(); }
            catch (Exception ex) { Logger.WriteLine("FanStopService init error: " + ex.Message); }

            // Wire up WPF callbacks for key actions that need UI
            InputDispatcher.OnCycleAura = (delta) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var vm = (System.Windows.Application.Current?.MainWindow?.DataContext as GHelper.WPF.ViewModels.MainViewModel);
                    vm?.Keyboard.CycleAuraMode(delta);
                    if (vm?.Keyboard.AuraModeLabels != null && vm.Keyboard.AuraModeIcons != null)
                    {
                        ModeCarouselService.ShowAuraModes(
                            vm.Keyboard.AuraModeLabels,
                            vm.Keyboard.AuraModeIcons,
                            vm.Keyboard.SelectedAuraModeIndex);
                    }
                });
            };

            InputDispatcher.OnCycleVisual = (delta) =>
            {
                // Cycle through the device's available Splendid/visual modes
                // (Default, Vivid, Eyecare, etc.), wrapping at both ends. Apply and
                // surface a quick OSD so the user sees what they landed on.
                try
                {
                    var modes = VisualControl.GetVisualModes();
                    if (modes.Count == 0) return;

                    var keys = new List<SplendidCommand>(modes.Keys);
                    int currentCmd = AppConfig.Get("visual");
                    int currentIdx = keys.FindIndex(k => (int)k == currentCmd);
                    if (currentIdx < 0) currentIdx = 0;

                    int nextIdx = ((currentIdx + delta) % keys.Count + keys.Count) % keys.Count;
                    SplendidCommand nextCmd = keys[nextIdx];
                    int temp = AppConfig.Get("color_temp");

                    Task.Run(() =>
                    {
                        try { VisualControl.SetVisual(nextCmd, temp); }
                        catch (Exception ex) { Logger.WriteLine("Cycle visual apply error: " + ex.Message); }
                    });

                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        // Brightness/sun glyph — same icon the Visual Mode nav uses.
                        ToastService.ShowOsdOnly(modes[nextCmd], "\uE793", ThemeService.AccentColor);
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("OnCycleVisual error: " + ex.Message);
                }
            };

            InputDispatcher.OnCyclePerformanceMode = () =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    ModeCarouselService.ShowPerformanceModes();
                });
            };

            InputDispatcher.OnSetGpuMode = (gpuMode) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    ModeCarouselService.ShowGpuModes(gpuMode);
                });
            };

            InputDispatcher.OnToggleApp = () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var win = System.Windows.Application.Current?.MainWindow;
                    if (win != null)
                    {
                        if (win.IsVisible) win.Hide();
                        else { win.Show(); win.Activate(); }
                    }
                });
            };

            // Sensor polling is driven by MainViewModel's DispatcherTimer tick
            // (see OnSensorTick). One timer = one tick point.
        }

        public static void Shutdown()
        {
            Logger.WriteLine("AppHost shutting down");
            _sensorTimer?.Stop();
            _sensorTimer?.Dispose();
            try { AppProfileService.Shutdown(); } catch { }
            try { GlobalHotkeyService.Shutdown(); } catch { }
            try { FanStopService.Shutdown(); } catch { }
            Logger.Shutdown();
        }
    }
}
