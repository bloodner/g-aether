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

        private const int HistorySize = 60; // 60 seconds of history

        private readonly double[] _cpuTempHistory = new double[HistorySize];
        private readonly double[] _gpuTempHistory = new double[HistorySize];
        private readonly double[] _cpuFanHistory = new double[HistorySize];
        private readonly double[] _gpuFanHistory = new double[HistorySize];
        private readonly double[] _gpuUseHistory = new double[HistorySize];
        private readonly double[] _gpuPowerHistory = new double[HistorySize];
        private readonly double[] _batteryRateHistory = new double[HistorySize];

        [ObservableProperty]
        private double[] _cpuTempValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _gpuTempValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _cpuFanValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _gpuFanValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _gpuUseValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _gpuPowerValues = new double[HistorySize];

        [ObservableProperty]
        private double[] _batteryRateValues = new double[HistorySize];

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
        private bool _isGpuActive = true;

        [ObservableProperty]
        private string _batteryRateText = "--";

        [ObservableProperty]
        private string _batteryPercentText = "--";

        [ObservableProperty]
        private string _powerStatusText = "";

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
            CpuTempValues = (double[])_cpuTempHistory.Clone();
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
            GpuTempValues = (double[])_gpuTempHistory.Clone();
            GpuTempText = gpuT > 0 ? $"{gpuT:F0}°C" : (ecoMode ? "Off" : "--");
            GpuTempLabel = "dGPU Temp";

            // Fan speeds
            double cpuFanRpm = ParseFanValue(HardwareControl.cpuFan);
            ShiftAndPush(_cpuFanHistory, cpuFanRpm);
            CpuFanValues = (double[])_cpuFanHistory.Clone();
            CpuFanCurrentText = FormatFanText(HardwareControl.cpuFan);

            double gpuFanRpm = ParseFanValue(HardwareControl.gpuFan);
            ShiftAndPush(_gpuFanHistory, gpuFanRpm);
            GpuFanValues = (double[])_gpuFanHistory.Clone();
            GpuFanCurrentText = FormatFanText(HardwareControl.gpuFan);

            // GPU Usage
            double gpuUseVal = gpuUse >= 0 ? gpuUse : 0;
            ShiftAndPush(_gpuUseHistory, gpuUseVal);
            GpuUseValues = (double[])_gpuUseHistory.Clone();
            GpuUseText = gpuUse >= 0 ? $"{gpuUse}%" : (ecoMode ? "Off" : "--");
            GpuUseLabel = "dGPU Usage";

            // GPU Power Draw (watts)
            int? gpuPower = HardwareControl.gpuPower;
            double gpuPowerVal = gpuPower is > 0 ? (double)gpuPower : 0;
            ShiftAndPush(_gpuPowerHistory, gpuPowerVal);
            GpuPowerValues = (double[])_gpuPowerHistory.Clone();
            GpuPowerText = gpuPower is > 0 ? $"{gpuPower}W" : (ecoMode ? "Off" : "--");

            // Battery Rate
            decimal rate = HardwareControl.batteryRate ?? 0;
            double absRate = Math.Abs((double)rate);
            ShiftAndPush(_batteryRateHistory, absRate);
            BatteryRateValues = (double[])_batteryRateHistory.Clone();
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
    }
}
