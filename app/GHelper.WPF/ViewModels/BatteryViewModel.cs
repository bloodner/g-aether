using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Battery;

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

            // Refresh battery health/cycles at most every 15 minutes (WMI queries are expensive)
            if (DateTime.UtcNow - _lastHealthRefresh > HealthRefreshInterval)
            {
                _lastHealthRefresh = DateTime.UtcNow;

                try
                {
                    HardwareControl.RefreshBatteryHealth();
                    HealthText = HardwareControl.batteryHealth >= 0
                        ? Math.Round(HardwareControl.batteryHealth, 1) + "%"
                        : "--";
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Battery health error: " + ex.Message);
                    HealthText = "--";
                }

                try
                {
                    int cycles = GetBatteryCycleCount();
                    CyclesText = cycles >= 0 ? cycles.ToString() : "--";
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Battery cycles error: " + ex.Message);
                    CyclesText = "--";
                }
            }
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
