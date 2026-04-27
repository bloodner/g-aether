using CommunityToolkit.Mvvm.ComponentModel;

namespace GHelper.WPF.ViewModels
{
    public partial class MonitorViewModel : ObservableObject
    {
        private static readonly System.Windows.Media.SolidColorBrush GreenBrush;
        private static readonly System.Windows.Media.SolidColorBrush AmberBrush;
        private static readonly System.Windows.Media.SolidColorBrush OrangeBrush;
        private static readonly System.Windows.Media.SolidColorBrush RedBrush;

        static MonitorViewModel()
        {
            GreenBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xC9, 0x5E));
            GreenBrush.Freeze();
            AmberBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x47));
            AmberBrush.Freeze();
            OrangeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x42));
            OrangeBrush.Freeze();
            RedBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x44, 0x44));
            RedBrush.Freeze();
        }

        // Buffers now hold the longest possible window (15 min @ 1s tick = 900).
        // Shorter windows are rendered by slicing the tail.
        private const int BufferSize = 900;

        private readonly double[] _cpuTempHistory = new double[BufferSize];
        private readonly double[] _gpuTempHistory = new double[BufferSize];
        private readonly double[] _cpuFanHistory = new double[BufferSize];
        private readonly double[] _gpuFanHistory = new double[BufferSize];
        private readonly double[] _gpuUseHistory = new double[BufferSize];
        private readonly double[] _gpuPowerHistory = new double[BufferSize];
        private readonly double[] _cpuUseHistory = new double[BufferSize];
        private readonly double[] _batteryRateHistory = new double[BufferSize];

        [ObservableProperty]
        private double[] _cpuTempValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _gpuTempValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _cpuFanValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _gpuFanValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _gpuUseValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _gpuPowerValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _cpuUseValues = new double[BufferSize];

        [ObservableProperty]
        private double[] _batteryRateValues = new double[BufferSize];

        [ObservableProperty]
        private string _cpuTempText = "--";

        [ObservableProperty]
        private string _gpuTempText = "--";

        [ObservableProperty]
        private string _gpuTempLabel = "dGPU Temp";

        [ObservableProperty]
        private string _cpuFanCurrentText = "--";

        [ObservableProperty]
        private string _gpuFanCurrentText = "--";

        [ObservableProperty]
        private string _gpuUseText = "--";

        [ObservableProperty]
        private string _gpuUseLabel = "dGPU Usage";

        [ObservableProperty]
        private string _gpuPowerText = "--";

        [ObservableProperty]
        private string _cpuUseText = "--";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShouldShowDgpuTemp), nameof(ShouldShowDgpuUse))]
        private bool _isGpuActive = true;

        [ObservableProperty]
        private string _batteryRateText = "--";

        [ObservableProperty]
        private string _batteryPercentText = "--";

        [ObservableProperty]
        private string _powerStatusText = "";

        // ---- Scope settings (driven by ScopeService.ApplySettings) ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShouldShowDgpuTemp))]
        private bool _scopeShowDgpuTemp = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShouldShowDgpuUse))]
        private bool _scopeShowDgpuUse = true;

        [ObservableProperty] private bool _scopeShowCpuTemp = true;
        [ObservableProperty] private bool _scopeShowCpuUse = true;
        [ObservableProperty] private bool _scopeShowPower = true;
        [ObservableProperty] private bool _scopeShowBattery = true;
        [ObservableProperty] private bool _scopeShowCpuFan = true;
        [ObservableProperty] private bool _scopeShowGpuFan = true;

        // 80 / 106 / 130 — derived from "compact" / "regular" / "large".
        [ObservableProperty] private double _sparklineHeight = 106;
        // 22 / 28 / 32 — value text size matching the size preset.
        [ObservableProperty] private double _valueFontSize = 28;
        // Smaller value font for fan tiles (RPM digits) — keep proportional.
        [ObservableProperty] private double _valueFontSizeFan = 22;

        // 60 / 300 / 900 — number of trailing samples to render.
        [ObservableProperty] private int _historyWindowSeconds = 60;

        // Per-tile stroke colors. On "multi" these match the existing hardcoded
        // sparkline colors. On a single accent, all eight tint to that color.
        [ObservableProperty] private System.Windows.Media.Color _cpuTempStroke =
            System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B);
        [ObservableProperty] private System.Windows.Media.Color _gpuTempStroke =
            System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x47);
        [ObservableProperty] private System.Windows.Media.Color _cpuUseStroke =
            System.Windows.Media.Color.FromRgb(0xA7, 0x8B, 0xFA);
        [ObservableProperty] private System.Windows.Media.Color _gpuUseStroke =
            System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF);
        [ObservableProperty] private System.Windows.Media.Color _gpuPowerStroke =
            System.Windows.Media.Color.FromRgb(0x6B, 0xCB, 0x77);
        [ObservableProperty] private System.Windows.Media.Color _cpuFanStroke =
            System.Windows.Media.Color.FromRgb(0xC0, 0x84, 0xFC);
        [ObservableProperty] private System.Windows.Media.Color _gpuFanStroke =
            System.Windows.Media.Color.FromRgb(0xF4, 0x72, 0xB6);

        // Composite visibility — both scope toggle AND IsGpuActive must be true.
        public bool ShouldShowDgpuTemp => ScopeShowDgpuTemp && IsGpuActive;
        public bool ShouldShowDgpuUse => ScopeShowDgpuUse && IsGpuActive;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BatteryAccentColor))]
        private System.Windows.Media.SolidColorBrush _batteryBrush = GreenBrush;

        /// <summary>Underlying Color of BatteryBrush — used by the halo on the Monitor battery tile.</summary>
        public System.Windows.Media.Color BatteryAccentColor => BatteryBrush?.Color ?? GreenBrush.Color;

        public void UpdateSensors()
        {
            // CPU Temp
            float cpuT = HardwareControl.cpuTemp ?? -1;
            ShiftAndPush(_cpuTempHistory, cpuT > 0 ? cpuT : 0);
            CpuTempValues = TailOf(_cpuTempHistory);
            CpuTempText = cpuT > 0 ? $"{cpuT:F0}°C" : "--";

            // GPU Temp
            float gpuT = HardwareControl.gpuTemp ?? -1;
            int gpuUse = HardwareControl.gpuUse ?? -1;

            // dGPU is only truly off in Eco mode; Standard/Optimized/Ultimate = available
            bool ecoMode = AppConfig.Get("gpu_mode") == AsusACPI.GPUModeEco;
            bool hasSensorData = gpuT > 0 || gpuUse >= 0;
            bool gpuActive = !ecoMode || hasSensorData;
            IsGpuActive = gpuActive;

            ShiftAndPush(_gpuTempHistory, gpuT > 0 ? gpuT : 0);
            GpuTempValues = TailOf(_gpuTempHistory);
            GpuTempText = gpuT > 0 ? $"{gpuT:F0}°C" : (ecoMode ? "Off" : "--");
            GpuTempLabel = "dGPU Temp";

            // Fan speeds
            double cpuFanRpm = ParseFanValue(HardwareControl.cpuFan);
            ShiftAndPush(_cpuFanHistory, cpuFanRpm);
            CpuFanValues = TailOf(_cpuFanHistory);
            CpuFanCurrentText = FormatFanText(HardwareControl.cpuFan);

            double gpuFanRpm = ParseFanValue(HardwareControl.gpuFan);
            ShiftAndPush(_gpuFanHistory, gpuFanRpm);
            GpuFanValues = TailOf(_gpuFanHistory);
            GpuFanCurrentText = FormatFanText(HardwareControl.gpuFan);

            // GPU Usage
            double gpuUseVal = gpuUse >= 0 ? gpuUse : 0;
            ShiftAndPush(_gpuUseHistory, gpuUseVal);
            GpuUseValues = TailOf(_gpuUseHistory);
            GpuUseText = gpuUse >= 0 ? $"{gpuUse}%" : (ecoMode ? "Off" : "--");
            GpuUseLabel = "dGPU Usage";

            // CPU Usage (%)
            int? cpuUseRaw = HardwareControl.cpuUse;
            double cpuUseVal = cpuUseRaw is > 0 ? (double)cpuUseRaw : 0;
            ShiftAndPush(_cpuUseHistory, cpuUseVal);
            CpuUseValues = TailOf(_cpuUseHistory);
            CpuUseText = cpuUseRaw is null or < 0 ? "--" : $"{cpuUseRaw}%";

            // GPU Power Draw (watts)
            int? gpuPower = HardwareControl.gpuPower;
            double gpuPowerVal = gpuPower is > 0 ? (double)gpuPower : 0;
            ShiftAndPush(_gpuPowerHistory, gpuPowerVal);
            GpuPowerValues = TailOf(_gpuPowerHistory);
            GpuPowerText = gpuPower is > 0 ? $"{gpuPower}W" : (ecoMode ? "Off" : "--");

            // Battery Rate
            decimal rate = HardwareControl.batteryRate ?? 0;
            double absRate = Math.Abs((double)rate);
            ShiftAndPush(_batteryRateHistory, absRate);
            BatteryRateValues = TailOf(_batteryRateHistory);
            if (rate != 0)
                BatteryRateText = rate > 0 ? $"+{absRate:F1}W" : $"-{absRate:F1}W";
            else
                BatteryRateText = "0W";

            // Battery percent & status
            var power = SystemInformation.PowerStatus;
            int pct = (int)(power.BatteryLifePercent * 100);
            BatteryPercentText = $"{pct}%";

            // Color-code battery by level
            var newBrush = pct > 60 ? GreenBrush : pct > 30 ? AmberBrush : pct > 15 ? OrangeBrush : RedBrush;
            if (BatteryBrush != newBrush)
                BatteryBrush = newBrush;

            bool plugged = power.PowerLineStatus == PowerLineStatus.Online;
            PowerStatusText = plugged ? "Charging" : "On Battery";

            // Opt-in battery automation: fires Focus scene at 20%, urgent toast at 10%.
            // No-op when disabled or when plugged in.
            Services.BatteryTriggerService.Check(pct, !plugged);
        }

        private double[] TailOf(double[] buffer)
        {
            int n = Math.Clamp(HistoryWindowSeconds, 1, buffer.Length);
            var result = new double[n];
            Array.Copy(buffer, buffer.Length - n, result, 0, n);
            return result;
        }

        private static string FormatFanText(string? fanStr)
        {
            if (string.IsNullOrEmpty(fanStr)) return "--";
            // Strip "Fan: " prefix if present
            var text = fanStr.Replace("Fan: ", "").Replace("Fan:", "").Trim();
            return text;
        }

        private static double ParseFanValue(string? fanStr)
        {
            if (string.IsNullOrEmpty(fanStr)) return 0;
            // Fan strings can be like "2400 RPM" or "45%"
            var digits = new string(fanStr.Where(c => char.IsDigit(c)).ToArray());
            return double.TryParse(digits, out double val) ? val : 0;
        }

        private static void ShiftAndPush(double[] buffer, double value)
        {
            Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
            buffer[buffer.Length - 1] = value;
        }

        /// <summary>
        /// Re-reads every scope_* AppConfig key and updates the corresponding
        /// observable properties. Called by ScopeService.ApplySettings on any
        /// settings change. Bindings in MonitorPanel.xaml react automatically.
        /// </summary>
        public void ReloadScopeSettings()
        {
            ScopeShowCpuTemp = AppConfig.Get("scope_show_cpu_temp", 1) == 1;
            ScopeShowDgpuTemp = AppConfig.Get("scope_show_dgpu_temp", 1) == 1;
            ScopeShowCpuUse = AppConfig.Get("scope_show_cpu_use", 1) == 1;
            ScopeShowDgpuUse = AppConfig.Get("scope_show_dgpu_use", 1) == 1;
            ScopeShowPower = AppConfig.Get("scope_show_power", 1) == 1;
            ScopeShowBattery = AppConfig.Get("scope_show_battery", 1) == 1;
            ScopeShowCpuFan = AppConfig.Get("scope_show_cpu_fan", 1) == 1;
            ScopeShowGpuFan = AppConfig.Get("scope_show_gpu_fan", 1) == 1;

            // Size preset → sparkline height + value font size.
            string size = AppConfig.GetString("scope_size") ?? "regular";
            (SparklineHeight, ValueFontSize, ValueFontSizeFan) = size switch
            {
                "compact" => (80.0, 22.0, 18.0),
                "large"   => (130.0, 32.0, 26.0),
                _         => (106.0, 28.0, 22.0),
            };

            // History window → number of samples to render.
            string window = AppConfig.GetString("scope_history_window") ?? "60s";
            HistoryWindowSeconds = window switch
            {
                "5min"  => 300,
                "15min" => 900,
                _       => 60,
            };

            // Accent → per-tile stroke colors.
            // Battery tile uses the charge-level BatteryBrush (green/amber/orange/red) and
            // is intentionally excluded from accent theming.
            string accent = AppConfig.GetString("scope_accent") ?? "multi";
            if (accent == "multi")
            {
                // Default per-tile palette.
                CpuTempStroke = System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B);
                GpuTempStroke = System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x47);
                CpuUseStroke = System.Windows.Media.Color.FromRgb(0xA7, 0x8B, 0xFA);
                GpuUseStroke = System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF);
                GpuPowerStroke = System.Windows.Media.Color.FromRgb(0x6B, 0xCB, 0x77);
                CpuFanStroke = System.Windows.Media.Color.FromRgb(0xC0, 0x84, 0xFC);
                GpuFanStroke = System.Windows.Media.Color.FromRgb(0xF4, 0x72, 0xB6);
            }
            else
            {
                var c = AccentColor(accent);
                CpuTempStroke = c;
                GpuTempStroke = c;
                CpuUseStroke = c;
                GpuUseStroke = c;
                GpuPowerStroke = c;
                CpuFanStroke = c;
                GpuFanStroke = c;
            }

            // Republish the *Values arrays so the new HistoryWindowSeconds takes
            // effect on the next render — without waiting for the next sensor tick.
            CpuTempValues = TailOf(_cpuTempHistory);
            GpuTempValues = TailOf(_gpuTempHistory);
            CpuUseValues = TailOf(_cpuUseHistory);
            GpuUseValues = TailOf(_gpuUseHistory);
            GpuPowerValues = TailOf(_gpuPowerHistory);
            CpuFanValues = TailOf(_cpuFanHistory);
            GpuFanValues = TailOf(_gpuFanHistory);
            BatteryRateValues = TailOf(_batteryRateHistory);
        }

        private static System.Windows.Media.Color AccentColor(string key) => key switch
        {
            "blue"   => System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF),
            "purple" => System.Windows.Media.Color.FromRgb(0xC0, 0x84, 0xFC),
            "green"  => System.Windows.Media.Color.FromRgb(0x6B, 0xCB, 0x77),
            "orange" => System.Windows.Media.Color.FromRgb(0xFF, 0xB3, 0x47),
            "red"    => System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B),
            "white"  => System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF2),
            "dark"   => System.Windows.Media.Color.FromRgb(0x36, 0x36, 0x3B),
            _        => System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF),
        };
    }
}
