using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.Gpu;
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
            Logger.Shutdown();
        }
    }
}
