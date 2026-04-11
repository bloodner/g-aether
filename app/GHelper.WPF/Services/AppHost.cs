using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.Gpu;
using Ryzen;

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

            // Initialize ACPI connection
            Program.acpi = new AsusACPI();

            // Initialize controllers (without SettingsForm — WPF uses ViewModels)
            Program.modeControl = new ModeControl();
            Program.gpuControl = new GPUModeControl();

            HardwareControl.RecreateGpuControl();
            RyzenControl.Init();

            // Initialize toast (required by InputDispatcher actions that call Program.toast)
            Program.toast = new ToastForm();

            // Initialize input dispatcher for special key handling
            Program.inputDispatcher = new InputDispatcher();
            Program.inputDispatcher.Init();

            // Wire up WPF callbacks for key actions that need UI
            InputDispatcher.OnCycleAura = (delta) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var vm = (System.Windows.Application.Current?.MainWindow?.DataContext as GHelper.WPF.ViewModels.MainViewModel);
                    vm?.Keyboard.CycleAuraMode(delta);
                    string modeName = vm?.Keyboard.AuraModeLabels.ElementAtOrDefault(vm.Keyboard.SelectedAuraModeIndex) ?? "Aura";
                    // Lighting icon \uE781 with default accent blue
                    ToastService.ShowOsdOnly(modeName, "\uE781", System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF));
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

            // Start sensor polling
            _sensorTimer = new System.Timers.Timer(1000);
            _sensorTimer.Elapsed += (s, e) =>
            {
                try
                {
                    HardwareControl.ReadSensors();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Sensor read error: " + ex.Message);
                }
            };
            _sensorTimer.Enabled = true;
        }

        public static void Shutdown()
        {
            Logger.WriteLine("AppHost shutting down");
            _sensorTimer?.Stop();
            _sensorTimer?.Dispose();
        }
    }
}
