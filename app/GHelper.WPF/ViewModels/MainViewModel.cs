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

        // Mode Badge Strip — live-updated badges shown above content on every panel
        [ObservableProperty] private string _perfBadgeName = "Balanced";
        [ObservableProperty] private Brush _perfBadgeBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
        [ObservableProperty] private bool _perfBadgeIsWarning;

        [ObservableProperty] private string _gpuBadgeName = "Standard";
        [ObservableProperty] private Brush _gpuBadgeBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
        [ObservableProperty] private bool _gpuBadgeIsWarning;

        [ObservableProperty] private string _displayBadgeName = "Auto";
        [ObservableProperty] private Brush _displayBadgeBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));

        [ObservableProperty] private string _servicesBadgeName = "Healthy";
        [ObservableProperty] private Brush _servicesBadgeBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));
        [ObservableProperty] private bool _servicesBadgeIsWarning;

        // Header accent hairline — reflects overall system health
        [ObservableProperty] private Brush _headerAccentBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));

        // Mode-color brushes all source from ThemeService so the palette is defined
        // in one place. Recreating on each Update*Badge() call is cheap and lets
        // Balanced/Standard track the live Windows accent.
        private static Brush ModeBrush(Color c) => new SolidColorBrush(c);

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

            // Wire up badge strip updates from child ViewModels
            Performance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Performance.SelectedModeIndex) ||
                    e.PropertyName == nameof(Performance.ModeLabels))
                    UpdatePerfBadge();
            };
            Gpu.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Gpu.SelectedModeIndex) ||
                    e.PropertyName == nameof(Gpu.ModeLabels) ||
                    e.PropertyName == nameof(Gpu.ModeColors))
                    UpdateGpuBadge();
            };
            Visual.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Visual.SelectedFreqIndex) ||
                    e.PropertyName == nameof(Visual.FrequencyLabels) ||
                    e.PropertyName == nameof(Visual.IsAutoFreqMode))
                    UpdateDisplayBadge();
            };
            Extra.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Extra.AsusServicesCount) ||
                    e.PropertyName == nameof(Extra.AsusServicesText))
                    UpdateServicesBadge();
            };

            UpdatePerfBadge();
            UpdateGpuBadge();
            UpdateDisplayBadge();
            UpdateServicesBadge();

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

        private void UpdatePerfBadge()
        {
            int idx = Performance.SelectedModeIndex;
            var labels = Performance.ModeLabels;
            if (idx < 0 || idx >= labels.Length) return;

            string name = labels[idx];
            PerfBadgeName = name;
            PerfBadgeBrush = name switch
            {
                "Silent" => ModeBrush(ThemeService.ColorSilent),
                "Turbo" => ModeBrush(ThemeService.ColorTurbo),
                _ => ModeBrush(ThemeService.ColorBalanced),
            };
            PerfBadgeIsWarning = name == "Turbo";
            UpdateHeaderAccent();
        }

        private void UpdateGpuBadge()
        {
            int idx = Gpu.SelectedModeIndex;
            var labels = Gpu.ModeLabels;
            if (idx < 0 || idx >= labels.Length) return;

            string name = labels[idx];
            GpuBadgeName = name;
            GpuBadgeBrush = name switch
            {
                "Eco" => ModeBrush(ThemeService.ColorEco),
                "Ultimate" => ModeBrush(ThemeService.ColorUltimate),
                "Optimized" => ModeBrush(ThemeService.ColorOptimized),
                _ => ModeBrush(ThemeService.ColorStandard),
            };
            GpuBadgeIsWarning = name == "Ultimate";
            UpdateHeaderAccent();
        }

        private void UpdateDisplayBadge()
        {
            int idx = Visual.SelectedFreqIndex;
            var labels = Visual.FrequencyLabels;
            if (idx < 0 || idx >= labels.Length)
            {
                DisplayBadgeName = "Auto";
            }
            else
            {
                DisplayBadgeName = labels[idx];
            }
            // Auto refresh rate means "system decides" — same semantic as GPU Optimized,
            // so it uses the same magenta. Any fixed rate uses the plain accent.
            DisplayBadgeBrush = Visual.IsAutoFreqMode
                ? ModeBrush(ThemeService.ColorOptimized)
                : ModeBrush(ThemeService.AccentColor);
        }

        private void UpdateServicesBadge()
        {
            int count = Extra.AsusServicesCount;
            if (count == 0)
            {
                ServicesBadgeName = "Healthy";
                ServicesBadgeBrush = ModeBrush(ThemeService.ColorEco);
                ServicesBadgeIsWarning = false;
            }
            else
            {
                ServicesBadgeName = count == 1 ? "1 Running" : $"{count} Running";
                ServicesBadgeBrush = ModeBrush(ThemeService.ColorTurbo);
                ServicesBadgeIsWarning = true;
            }
            UpdateHeaderAccent();
        }

        private void UpdateHeaderAccent()
        {
            // Orange if any badge is in a warning state; else green.
            bool anyWarning = ServicesBadgeIsWarning || PerfBadgeIsWarning || GpuBadgeIsWarning;
            HeaderAccentBrush = anyWarning ? ModeBrush(ThemeService.ColorTurbo) : ModeBrush(ThemeService.ColorEco);
        }
    }
}
