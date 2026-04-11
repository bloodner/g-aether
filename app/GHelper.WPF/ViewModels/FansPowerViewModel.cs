using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Fan;
using GHelper.Mode;

namespace GHelper.WPF.ViewModels
{
    public partial class FansPowerViewModel : ObservableObject
    {
        // Fan curve data (8 temp points + 8 speed points per fan)
        [ObservableProperty] private byte[] _cpuFanTemps = new byte[8];
        [ObservableProperty] private byte[] _cpuFanSpeeds = new byte[8];
        [ObservableProperty] private byte[] _gpuFanTemps = new byte[8];
        [ObservableProperty] private byte[] _gpuFanSpeeds = new byte[8];

        // Power limits
        [ObservableProperty] private int _totalPowerLimit = 45;
        [ObservableProperty] private int _slowBoostLimit = 65;
        [ObservableProperty] private int _fastBoostLimit = 80;
        [ObservableProperty] private int _cpuPowerLimit = 45;

        [ObservableProperty] private int _minPower = 5;
        [ObservableProperty] private int _maxPower = 150;

        [ObservableProperty] private string _totalPowerText = "45W";
        [ObservableProperty] private string _slowBoostText = "65W";
        [ObservableProperty] private string _fastBoostText = "80W";
        [ObservableProperty] private string _cpuPowerText = "45W";

        // GPU clocks
        [ObservableProperty] private int _gpuCoreOffset = 0;
        [ObservableProperty] private int _gpuMemoryOffset = 0;
        [ObservableProperty] private int _gpuBoostPower = 0;
        [ObservableProperty] private int _gpuTempTarget = 87;
        [ObservableProperty] private string _gpuName = "GPU";

        [ObservableProperty] private string _gpuCoreText = "0 MHz";
        [ObservableProperty] private string _gpuMemoryText = "0 MHz";
        [ObservableProperty] private string _gpuBoostText = "0W";
        [ObservableProperty] private string _gpuTempText = "87°C";

        [ObservableProperty] private int _minCoreOffset = -250;
        [ObservableProperty] private int _maxCoreOffset = 250;
        [ObservableProperty] private int _minMemoryOffset = -500;
        [ObservableProperty] private int _maxMemoryOffset = 500;

        [ObservableProperty] private bool _hasNvidiaGpu;

        // Auto-apply
        [ObservableProperty] private bool _autoApplyPower;

        private bool _ignoreChange;

        partial void OnTotalPowerLimitChanged(int value)
        {
            TotalPowerText = $"{value}W";
            if (!_ignoreChange) SavePowerLimits();
        }

        partial void OnSlowBoostLimitChanged(int value)
        {
            SlowBoostText = $"{value}W";
            if (!_ignoreChange) SavePowerLimits();
        }

        partial void OnFastBoostLimitChanged(int value)
        {
            FastBoostText = $"{value}W";
            if (!_ignoreChange) SavePowerLimits();
        }

        partial void OnCpuPowerLimitChanged(int value)
        {
            CpuPowerText = $"{value}W";
            if (!_ignoreChange) SavePowerLimits();
        }

        partial void OnGpuCoreOffsetChanged(int value)
        {
            GpuCoreText = $"{(value >= 0 ? "+" : "")}{value} MHz";
            if (!_ignoreChange) SaveGpuClocks();
        }

        partial void OnGpuMemoryOffsetChanged(int value)
        {
            GpuMemoryText = $"{(value >= 0 ? "+" : "")}{value} MHz";
            if (!_ignoreChange) SaveGpuClocks();
        }

        partial void OnGpuBoostPowerChanged(int value)
        {
            GpuBoostText = $"{value}W";
            if (!_ignoreChange) SaveGpuClocks();
        }

        partial void OnGpuTempTargetChanged(int value)
        {
            GpuTempText = $"{value}°C";
            if (!_ignoreChange) SaveGpuClocks();
        }

        partial void OnAutoApplyPowerChanged(bool value)
        {
            if (!_ignoreChange)
                AppConfig.Set("auto_apply_power", value ? 1 : 0);
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                LoadFanCurves();
                LoadPowerLimits();
                LoadGpuSettings();

                AutoApplyPower = AppConfig.Is("auto_apply_power");
            }
            finally
            {
                _ignoreChange = false;
            }
        }

        private void LoadFanCurves()
        {
            // Default curve: gentle ramp
            byte[] defaultTemps = [30, 40, 50, 60, 70, 75, 80, 90];
            byte[] defaultSpeeds = [0, 5, 10, 20, 35, 55, 75, 100];

            var cpuCurve = AppConfig.GetFanConfig(AsusFan.CPU);
            var gpuCurve = AppConfig.GetFanConfig(AsusFan.GPU);

            if (cpuCurve != null && cpuCurve.Length >= 16)
            {
                CpuFanTemps = cpuCurve[..8];
                CpuFanSpeeds = cpuCurve[8..16];
            }
            else
            {
                CpuFanTemps = (byte[])defaultTemps.Clone();
                CpuFanSpeeds = (byte[])defaultSpeeds.Clone();
            }

            if (gpuCurve != null && gpuCurve.Length >= 16)
            {
                GpuFanTemps = gpuCurve[..8];
                GpuFanSpeeds = gpuCurve[8..16];
            }
            else
            {
                GpuFanTemps = (byte[])defaultTemps.Clone();
                GpuFanSpeeds = (byte[])defaultSpeeds.Clone();
            }
        }

        private void LoadPowerLimits()
        {
            int total = AppConfig.GetMode("limit_total");
            int slow = AppConfig.GetMode("limit_slow");
            int fast = AppConfig.GetMode("limit_fast");
            int cpu = AppConfig.GetMode("limit_cpu");

            if (total > 0) TotalPowerLimit = total;
            if (slow > 0) SlowBoostLimit = slow;
            if (fast > 0) FastBoostLimit = fast;
            if (cpu > 0) CpuPowerLimit = cpu;

            // Update text
            TotalPowerText = $"{TotalPowerLimit}W";
            SlowBoostText = $"{SlowBoostLimit}W";
            FastBoostText = $"{FastBoostLimit}W";
            CpuPowerText = $"{CpuPowerLimit}W";
        }

        private void LoadGpuSettings()
        {
            HasNvidiaGpu = AppConfig.IsNVPlatform();
            GpuName = HasNvidiaGpu ? "NVIDIA GPU" : "GPU";

            int core = AppConfig.Get("gpu_core");
            int memory = AppConfig.Get("gpu_memory");
            int boost = AppConfig.Get("gpu_boost");
            int temp = AppConfig.Get("gpu_temp");

            if (core != -9999) GpuCoreOffset = core;
            if (memory != -9999) GpuMemoryOffset = memory;
            if (boost > 0) GpuBoostPower = boost;
            if (temp > 0) GpuTempTarget = temp;

            // Update text
            GpuCoreText = $"{(GpuCoreOffset >= 0 ? "+" : "")}{GpuCoreOffset} MHz";
            GpuMemoryText = $"{(GpuMemoryOffset >= 0 ? "+" : "")}{GpuMemoryOffset} MHz";
            GpuBoostText = $"{GpuBoostPower}W";
            GpuTempText = $"{GpuTempTarget}°C";
        }

        public void OnCpuCurveChanged()
        {
            SaveFanCurve(AsusFan.CPU, CpuFanTemps, CpuFanSpeeds);
        }

        public void OnGpuCurveChanged()
        {
            SaveFanCurve(AsusFan.GPU, GpuFanTemps, GpuFanSpeeds);
        }

        private void SaveFanCurve(AsusFan fan, byte[] temps, byte[] speeds)
        {
            if (temps == null || speeds == null || temps.Length != 8 || speeds.Length != 8) return;

            byte[] curve = new byte[16];
            Array.Copy(temps, 0, curve, 0, 8);
            Array.Copy(speeds, 0, curve, 8, 8);
            AppConfig.SetFanConfig(fan, curve);

            Task.Run(() =>
            {
                try { Program.modeControl?.AutoFans(); }
                catch (Exception ex) { Logger.WriteLine("Fan apply error: " + ex.Message); }
            });
        }

        private void SavePowerLimits()
        {
            AppConfig.SetMode("limit_total", TotalPowerLimit);
            AppConfig.SetMode("limit_slow", SlowBoostLimit);
            AppConfig.SetMode("limit_fast", FastBoostLimit);
            AppConfig.SetMode("limit_cpu", CpuPowerLimit);

            Task.Run(() =>
            {
                try { Program.modeControl?.AutoPower(true); }
                catch (Exception ex) { Logger.WriteLine("Power apply error: " + ex.Message); }
            });
        }

        private void SaveGpuClocks()
        {
            AppConfig.Set("gpu_core", GpuCoreOffset);
            AppConfig.Set("gpu_memory", GpuMemoryOffset);
            AppConfig.Set("gpu_boost", GpuBoostPower);
            AppConfig.Set("gpu_temp", GpuTempTarget);

            Task.Run(() =>
            {
                try
                {
                    Program.modeControl?.SetGPUClocks();
                    Program.modeControl?.SetGPUPower();
                }
                catch (Exception ex) { Logger.WriteLine("GPU clock error: " + ex.Message); }
            });
        }

        [RelayCommand]
        private void ResetFanCurves()
        {
            byte[] defaultTemps = [30, 40, 50, 60, 70, 75, 80, 90];
            byte[] defaultSpeeds = [0, 5, 10, 20, 35, 55, 75, 100];

            CpuFanTemps = (byte[])defaultTemps.Clone();
            CpuFanSpeeds = (byte[])defaultSpeeds.Clone();
            GpuFanTemps = (byte[])defaultTemps.Clone();
            GpuFanSpeeds = (byte[])defaultSpeeds.Clone();

            SaveFanCurve(AsusFan.CPU, CpuFanTemps, CpuFanSpeeds);
            SaveFanCurve(AsusFan.GPU, GpuFanTemps, GpuFanSpeeds);
        }

        [RelayCommand]
        private void ResetGpuClocks()
        {
            _ignoreChange = true;
            GpuCoreOffset = 0;
            GpuMemoryOffset = 0;
            GpuBoostPower = 0;
            GpuTempTarget = 87;
            _ignoreChange = false;

            SaveGpuClocks();
        }
    }
}
