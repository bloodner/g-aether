using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private AppProfilesViewModel _appProfiles = new();

        [ObservableProperty]
        private ProcessesViewModel _processes = new();

        // Mode Badge Strip — owned by ModeStripViewModel; sources from child VMs.
        [ObservableProperty]
        private ModeStripViewModel _modeStrip = null!;

        /// <summary>Built-in Scene buttons shown in the footer strip.</summary>
        public IReadOnlyList<Scene> Scenes => SceneService.BuiltInScenes;

        /// <summary>
        /// Apply a Scene bundle by routing through the child VMs (same path
        /// Smart Optimize uses), then surface a quick OSD to confirm.
        /// </summary>
        [RelayCommand]
        private void ApplyScene(Scene? scene)
        {
            if (scene == null) return;
            try
            {
                scene.Apply(this);
                ToastService.ShowOsdOnly(scene.Name, scene.Icon, ThemeService.AccentColor);
                Logger.WriteLine($"Scene applied: {scene.Name}");
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Scene '{scene.Name}' apply error: " + ex.Message);
            }
        }

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

            // Mode Badge Strip — owned by ModeStripViewModel; sources from child VMs.
            ModeStrip = new ModeStripViewModel(Performance, Gpu, Visual, Extra);

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
        private bool _readInProgress;

        private async void OnSensorTick(object? sender, EventArgs e)
        {
            // Read hardware sensors on a background thread, then update UI.
            // Skip if the previous read is still running so we never stack up.
            if (!_readInProgress)
            {
                _readInProgress = true;
                try
                {
                    await Task.Run(() =>
                    {
                        try { HardwareControl.ReadSensors(); }
                        catch (Exception ex) { Logger.WriteLine("Sensor read error: " + ex.Message); }
                    });
                }
                finally
                {
                    _readInProgress = false;
                }
            }

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

            // Publish the snapshot to any connected pipe clients (Game Bar widget, etc.).
            // No-op when nobody's connected, so the cost is a null check.
            Services.TelemetryPipeServer.PushSnapshot();

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
