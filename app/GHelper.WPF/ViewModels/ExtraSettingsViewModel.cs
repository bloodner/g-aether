using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Helpers;
using GHelper.USB;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class ExtraSettingsViewModel : ObservableObject
    {
        public string VersionText { get; } = $"G-Aether v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1"}";

        [ObservableProperty]
        private bool _runOnStartup;

        [ObservableProperty]
        private bool _fnLockEnabled;

        [ObservableProperty]
        private bool _gpuFixEnabled;

        [ObservableProperty]
        private bool _noOverdrive;

        [ObservableProperty]
        private bool _windowTopmost;

        [ObservableProperty]
        private bool _bootSound;

        [ObservableProperty]
        private bool _bwTrayIcon;

        [ObservableProperty]
        private bool _advancedRgb;

        [ObservableProperty]
        private int _asusServicesCount;

        [ObservableProperty]
        private string _asusServicesText = "Checking...";

        [ObservableProperty]
        private string _asusServicesButtonText = "Stop";

        [ObservableProperty]
        private bool _asusServicesButtonEnabled = true;

        [ObservableProperty]
        private bool _stopArmoryCrate;

        [ObservableProperty]
        private bool _servicesExpanded;

        [ObservableProperty]
        private List<ServiceInfo> _serviceDetails = new();

        private bool _ignoreChange;

        partial void OnRunOnStartupChanged(bool value)
        {
            if (_ignoreChange) return;
            Task.Run(() =>
            {
                try
                {
                    if (value)
                        Startup.Schedule();
                    else
                        Startup.UnSchedule();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Startup toggle error: " + ex.Message);
                }
            });
        }

        partial void OnFnLockEnabledChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("fn_lock", value ? 1 : 0);
            Task.Run(() =>
            {
                try
                {
                    Program.acpi?.DeviceSet(AsusACPI.FnLock, value ? 1 : 0, "FnLock");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("FnLock error: " + ex.Message);
                }
            });
        }

        partial void OnGpuFixEnabledChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("gpu_fix", value ? 1 : 0);
        }

        partial void OnNoOverdriveChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("no_overdrive", value ? 1 : 0);
        }

        partial void OnWindowTopmostChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("topmost", value ? 1 : 0);
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Application.Current?.MainWindow != null)
                    Application.Current.MainWindow.Topmost = value;
            });
        }

        partial void OnBootSoundChanged(bool value)
        {
            if (_ignoreChange) return;
            Task.Run(() =>
            {
                try
                {
                    Program.acpi?.DeviceSet(AsusACPI.BootSound, value ? 1 : 0, "BootSound");
                    AppConfig.Set("boot_sound", value ? 1 : 0);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Boot sound error: " + ex.Message);
                }
            });
        }

        partial void OnBwTrayIconChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("bw_icon", value ? 1 : 0);
        }

        partial void OnAdvancedRgbChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("advanced_rgb", value ? 1 : 0);
            Aura.RefreshRGBFlags();
        }

        partial void OnStopArmoryCrateChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("stop_ac", value ? 1 : 0);
            RefreshAsusServices();
        }

        [RelayCommand]
        private async Task ToggleAsusServices()
        {
            AsusServicesButtonEnabled = false;
            bool isRunning = AsusServicesCount > 0;

            AsusServicesText = isRunning ? "Stopping services..." : "Starting services...";

            await Task.Run(() =>
            {
                try
                {
                    if (isRunning)
                        AsusService.StopAsusServices();
                    else
                        AsusService.StartAsusServices();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Service toggle error: " + ex.Message);
                }
            });

            RefreshAsusServices();
            AsusServicesButtonEnabled = true;

            ToastService.Show(isRunning ? "ASUS services stopped" : "ASUS services started", ToastType.Info);
        }

        [RelayCommand]
        private void ToggleServicesExpanded()
        {
            ServicesExpanded = !ServicesExpanded;
        }

        public void RefreshAsusServices()
        {
            Task.Run(() =>
            {
                var details = AsusService.GetServiceDetails();
                int count = details.Count(s => s.IsRunning);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    AsusServicesCount = count;
                    ServiceDetails = details;
                    AsusServicesText = count > 0
                        ? $"{count} of {details.Count} services running"
                        : "All services stopped";
                    AsusServicesButtonText = count > 0 ? "Stop All" : "Start All";
                });
            });
        }

        [RelayCommand]
        private void OpenUpdates()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/seerge/g-helper/releases",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                RunOnStartup = Startup.IsScheduled();
                FnLockEnabled = AppConfig.Is("fn_lock");
                GpuFixEnabled = AppConfig.Is("gpu_fix");
                NoOverdrive = AppConfig.Is("no_overdrive");
                WindowTopmost = AppConfig.Is("topmost");
                BootSound = AppConfig.Is("boot_sound");
                BwTrayIcon = AppConfig.Is("bw_icon");
                AdvancedRgb = AppConfig.Is("advanced_rgb");
                StopArmoryCrate = AppConfig.IsStopAC();
            }
            finally
            {
                _ignoreChange = false;
            }

            RefreshAsusServices();
        }
    }
}
