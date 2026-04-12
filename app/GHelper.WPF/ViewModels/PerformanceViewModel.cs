using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.Mode;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class PerformanceViewModel : ObservableObject
    {
        // Mode arc: Silent(0), Balanced(1), Turbo(2)
        [ObservableProperty]
        private int _selectedModeIndex;

        [ObservableProperty]
        private string _modeSummaryLabel = "Balanced";

        [ObservableProperty]
        private string[] _modeLabels = ["Silent", "Balanced", "Turbo"];

        // Apply strategy: Auto(0), Manual Fans(1), Manual Power(2), Manual Both(3)
        [ObservableProperty]
        private int _applyStrategyIndex;

        [ObservableProperty]
        private string[] _applyStrategyLabels = ["Auto", "Manual Fans", "Manual Power", "Manual Both"];

        // Visibility flags for expandable sections
        [ObservableProperty]
        private bool _showFanCurves;

        [ObservableProperty]
        private bool _showPowerLimits;

        [ObservableProperty]
        private bool _isExpanded;

        // CPU sensor card properties
        [ObservableProperty]
        private double _cpuTemperature;

        [ObservableProperty]
        private string _cpuFanSpeed = "--";

        [ObservableProperty]
        private string _cpuTargetSpeed = "";

        // GPU sensor card properties
        [ObservableProperty]
        private double _gpuTemperature;

        [ObservableProperty]
        private string _gpuFanSpeed = "--";

        [ObservableProperty]
        private string _gpuTargetSpeed = "";

        // Reference to FansPower VM for embedded controls
        public FansPowerViewModel FansPower { get; set; } = null!;

        private bool _ignoreIndexChange;

        partial void OnSelectedModeIndexChanged(int value)
        {
            if (_ignoreIndexChange) return;

            ModeCarouselService.ShowPerformanceModes();

            switch (value)
            {
                case 0:
                    Task.Run(() =>
                    {
                        try { Program.modeControl?.SetPerformanceMode(AsusACPI.PerformanceSilent); }
                        catch (Exception ex) { Logger.WriteLine("Performance mode error: " + ex.Message); }
                    });
                    break;
                case 1:
                    Task.Run(() =>
                    {
                        try { Program.modeControl?.SetPerformanceMode(AsusACPI.PerformanceBalanced); }
                        catch (Exception ex) { Logger.WriteLine("Performance mode error: " + ex.Message); }
                    });
                    break;
                case 2:
                    Task.Run(() =>
                    {
                        try { Program.modeControl?.SetPerformanceMode(AsusACPI.PerformanceTurbo); }
                        catch (Exception ex) { Logger.WriteLine("Performance mode error: " + ex.Message); }
                    });
                    break;
            }
        }

        partial void OnApplyStrategyIndexChanged(int value)
        {
            // Auto(0): both off, Manual Fans(1), Manual Power(2), Manual Both(3)
            bool applyFans = value == 1 || value == 3;
            bool applyPower = value == 2 || value == 3;

            AppConfig.SetMode("auto_apply", applyFans ? 1 : 0);
            AppConfig.SetMode("auto_apply_power", applyPower ? 1 : 0);

            ShowFanCurves = applyFans;
            ShowPowerLimits = applyPower;
        }

        public void SetFromMode(int mode)
        {
            _ignoreIndexChange = true;
            try
            {
                switch (mode)
                {
                    case AsusACPI.PerformanceSilent:
                        SelectedModeIndex = 0;
                        ModeSummaryLabel = "Silent";
                        break;
                    case AsusACPI.PerformanceBalanced:
                        SelectedModeIndex = 1;
                        ModeSummaryLabel = "Balanced";
                        break;
                    case AsusACPI.PerformanceTurbo:
                    default:
                        SelectedModeIndex = 2;
                        ModeSummaryLabel = mode == AsusACPI.PerformanceTurbo ? "Turbo" : Modes.GetName(mode);
                        break;
                }
            }
            finally
            {
                _ignoreIndexChange = false;
            }
        }

        public void LoadApplyStrategy()
        {
            bool applyFans = AppConfig.IsMode("auto_apply");
            bool applyPower = AppConfig.IsMode("auto_apply_power");

            if (applyFans && applyPower) ApplyStrategyIndex = 3;
            else if (applyPower) ApplyStrategyIndex = 2;
            else if (applyFans) ApplyStrategyIndex = 1;
            else ApplyStrategyIndex = 0;

            ShowFanCurves = applyFans;
            ShowPowerLimits = applyPower;
        }

        public void UpdateSensors()
        {
            CpuTemperature = HardwareControl.cpuTemp > 0 ? (double)HardwareControl.cpuTemp : 0;
            GpuTemperature = HardwareControl.gpuTemp > 0 ? (double)HardwareControl.gpuTemp : 0;

            CpuFanSpeed = !string.IsNullOrEmpty(HardwareControl.cpuFan) ? HardwareControl.cpuFan : "--";
            GpuFanSpeed = !string.IsNullOrEmpty(HardwareControl.gpuFan) ? HardwareControl.gpuFan : "--";
        }
    }
}
