using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
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
        private static readonly string AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1";
        private const string GitHubApiUrl = "https://api.github.com/repos/bloodner/g-aether/releases/latest";
        private const string ReleasesPageUrl = "https://github.com/bloodner/g-aether/releases";

        [ObservableProperty]
        private string _versionText = $"G-Aether v{AppVersion}";

        [ObservableProperty]
        private string _updateButtonText = "Check for Updates";

        [ObservableProperty]
        private bool _isUpdateAvailable;

        private string? _updateDownloadUrl;

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
                        OptimizationService.StopAsusServices();
                    else
                        OptimizationService.StartAsusServices();
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
                var details = OptimizationService.GetServiceDetails();
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
        private async Task CheckForUpdates()
        {
            if (IsUpdateAvailable && _updateDownloadUrl != null)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _updateDownloadUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Failed to open update URL: " + ex.Message);
                }
                return;
            }

            UpdateButtonText = "Checking...";

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "G-Aether");
                var json = await httpClient.GetStringAsync(GitHubApiUrl);
                var release = JsonSerializer.Deserialize<JsonElement>(json);
                var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
                var assets = release.GetProperty("assets");

                string? downloadUrl = null;
                for (int i = 0; i < assets.GetArrayLength(); i++)
                {
                    var url = assets[i].GetProperty("browser_download_url").GetString();
                    if (url != null && url.Contains(".zip"))
                    {
                        downloadUrl = url;
                        break;
                    }
                }
                downloadUrl ??= ReleasesPageUrl;

                var gitVersion = new Version(tag);
                var appVersion = new Version(AppVersion);

                if (gitVersion.CompareTo(appVersion) > 0)
                {
                    _updateDownloadUrl = downloadUrl;
                    IsUpdateAvailable = true;
                    UpdateButtonText = $"Download v{tag}";
                    VersionText = $"G-Aether v{AppVersion} → v{tag}";
                    ToastService.Show($"Update available: v{tag}", ToastType.Info);
                }
                else
                {
                    UpdateButtonText = "Check for Updates";
                    ToastService.Show("You're up to date!", ToastType.Success);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Update check failed: " + ex.Message);
                UpdateButtonText = "Check for Updates";
                ToastService.Show("Update check failed", ToastType.Error);
            }
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
