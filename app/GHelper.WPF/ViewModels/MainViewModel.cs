using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.Gpu;
using GHelper.Mode;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private PerformanceViewModel _performance = new();

        [ObservableProperty]
        private GpuViewModel _gpu = new();

        [ObservableProperty]
        private BatteryViewModel _battery = new();

        [ObservableProperty]
        private VisualModeViewModel _visual = new();

        [ObservableProperty]
        private KeyboardViewModel _keyboard = new();

        [ObservableProperty]
        private KeyBindingsViewModel _keyBindings = new();

        [ObservableProperty]
        private ExtraSettingsViewModel _extra = new();

        [ObservableProperty]
        private MonitorViewModel _monitor = new();

        [ObservableProperty]
        private FansPowerViewModel _fansPower = new();

        private readonly DispatcherTimer _sensorTimer;

        public MainViewModel()
        {
            // Initialize current mode
            int currentMode = AppConfig.Get("performance_mode");
            if (currentMode >= 0) Performance.SetFromMode(currentMode);

            Performance.FansPower = FansPower;
            Performance.LoadApplyStrategy();

            // Initialize GPU
            Gpu.InitFromCurrent();

            // Initialize Battery
            Battery.Initialize();

            // Initialize Visual
            Visual.Initialize();

            // Initialize Keyboard
            Keyboard.Initialize();

            // Initialize Key Bindings
            KeyBindings.Initialize();

            // Initialize Extra Settings
            Extra.Initialize();

            // Initialize Fans & Power
            FansPower.Initialize();

            // Sensor refresh timer on UI thread
            _sensorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _sensorTimer.Tick += OnSensorTick;
            _sensorTimer.Start();

            // Initial mode from backend
            Task.Run(() =>
            {
                try
                {
                    Program.modeControl?.AutoPerformance();
                    Program.gpuControl?.InitGPUMode();

                    // Read hardware capabilities and update GPU buttons on UI thread
                    int eco = Program.acpi.DeviceGet(AsusACPI.GPUEco);
                    int mux = Program.acpi.DeviceGet(AsusACPI.GPUMux);
                    int gpuMode = GPUModeControl.gpuMode;

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Gpu.SetGpuButtons(eco >= 0, mux >= 0);
                        Gpu.SetFromGpuMode(gpuMode);
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("WPF init error: " + ex.Message);
                }
            });
        }

        private int _tickCount;
        private int _lastMode = -1;

        private void OnSensorTick(object? sender, EventArgs e)
        {
            try
            {
                Performance.UpdateSensors();
                Battery.UpdateBatteryStatus();
                Monitor.UpdateSensors();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Sensor tick error: " + ex.Message);
            }

            // Sync performance mode if changed externally (e.g., via hotkey)
            try
            {
                int currentMode = Modes.GetCurrent();
                if (currentMode != _lastMode)
                {
                    bool isFirstSync = _lastMode == -1;
                    _lastMode = currentMode;
                    Performance.SetFromMode(currentMode);

                    // Show carousel for hotkey-triggered mode changes (skip initial sync)
                    if (!isFirstSync && !ModeCarouselService.IsVisible)
                    {
                        ModeCarouselService.ShowPerformanceModes();
                    }
                }

                // Update tray icon to reflect current performance + GPU mode
                int gpuMode = AppConfig.Get("gpu_mode");
                TrayIconService.Instance?.UpdateIcon(currentMode, gpuMode);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Mode sync error: " + ex.Message);
            }

            // Refresh current screen rate every 5 seconds (lightweight Win32 API call)
            if (++_tickCount % 5 == 0)
                Visual.RefreshCurrentRate();
        }
    }
}
