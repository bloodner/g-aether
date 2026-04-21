using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Battery;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class BatteryViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _chargePercentage = 100;

        [ObservableProperty]
        private string _healthText = "";

        [ObservableProperty]
        private string _cyclesText = "";

        [ObservableProperty]
        private int _chargeLimit = 100;

        [ObservableProperty]
        private string _chargeLimitText = "100%";

        [ObservableProperty]
        private string _powerStatusText = "";

        [ObservableProperty]
        private bool _triggersEnabled;

        partial void OnTriggersEnabledChanged(bool value)
        {
            if (_ignoreChange) return;
            BatteryTriggerService.IsEnabled = value;
        }

        private bool _ignoreChange;
        private DateTime _lastHealthRefresh = DateTime.MinValue;
        private static readonly TimeSpan HealthRefreshInterval = TimeSpan.FromMinutes(15);

        partial void OnChargeLimitChanged(int value)
        {
            ChargeLimitText = $"{value}%";

            if (_ignoreChange) return;

            Task.Run(() =>
            {
                try
                {
                    BatteryControl.SetBatteryChargeLimit(value);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Battery limit error: " + ex.Message);
                }
            });
        }

        [RelayCommand]
        private void BatteryReport()
        {
            BatteryControl.BatteryReport();
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                int limit = AppConfig.Get("charge_limit");
                if (limit >= 40 && limit <= 100) ChargeLimit = limit;
                else ChargeLimit = 100;

                TriggersEnabled = BatteryTriggerService.IsEnabled;

                UpdateBatteryStatus();
            }
            finally
            {
                _ignoreChange = false;
            }
        }

        public void UpdateBatteryStatus()
        {
            var power = SystemInformation.PowerStatus;
            ChargePercentage = (int)(power.BatteryLifePercent * 100);

            bool plugged = power.PowerLineStatus == PowerLineStatus.Online;
            PowerStatusText = plugged ? "Charging" : "On Battery";

            // Refresh battery health/cycles at most every 15 minutes (WMI queries are expensive).
            // Run on background thread so we never block the UI — even the first call.
            if (DateTime.UtcNow - _lastHealthRefresh > HealthRefreshInterval)
            {
                _lastHealthRefresh = DateTime.UtcNow;
                Task.Run(RefreshHealthBackground);
            }
        }

        private void RefreshHealthBackground()
        {
            string healthText = "--";
            string cyclesText = "--";

            try
            {
                HardwareControl.RefreshBatteryHealth();
                if (HardwareControl.batteryHealth >= 0)
                    healthText = Math.Round(HardwareControl.batteryHealth, 1) + "%";
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Battery health error: " + ex.Message);
            }

            try
            {
                int cycles = GetBatteryCycleCount();
                if (cycles >= 0) cyclesText = cycles.ToString();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Battery cycles error: " + ex.Message);
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                HealthText = healthText;
                CyclesText = cyclesText;
            });
        }

        private static int GetBatteryCycleCount()
        {
            try
            {
                var scope = new System.Management.ManagementScope("root\\WMI");
                var query = new System.Management.ObjectQuery("SELECT * FROM BatteryCycleCount");

                using var searcher = new System.Management.ManagementObjectSearcher(scope, query);
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        return Convert.ToInt32(obj["CycleCount"]);
                    }
                }
            }
            catch
            {
                // BatteryCycleCount WMI class may not be available on all systems
            }
            return -1;
        }

        public void SetChargeLimit(int limit)
        {
            _ignoreChange = true;
            try
            {
                ChargeLimit = limit;
            }
            finally
            {
                _ignoreChange = false;
            }
        }
    }
}
