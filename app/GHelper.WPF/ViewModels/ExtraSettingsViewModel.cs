using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Helpers;
using GHelper.Mode;
using GHelper.USB;
using GHelper.WPF.Services;
using GHelper.WPF.Views;

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
        private double _windowOpacity = 0.92;

        [ObservableProperty]
        private bool _servicesExpanded;

        [ObservableProperty]
        private List<ServiceInfo> _serviceDetails = new();

        [ObservableProperty]
        private string _updateStatusText = "";

        [ObservableProperty]
        private string _updateButtonText = "Check for Updates";

        [ObservableProperty]
        private bool _updateButtonEnabled = true;

        [ObservableProperty]
        private bool _updateAvailable;

        [ObservableProperty]
        private string _optimizeProfileName = "";

        [ObservableProperty]
        private string _optimizeReason = "";

        [ObservableProperty]
        private string _optimizeButtonText = "Optimize for Me";

        [ObservableProperty]
        private bool _optimizeButtonEnabled = true;

        [ObservableProperty]
        private bool _optimizeResultVisible;

        [ObservableProperty]
        private List<Recommendation> _optimizeRecommendations = new();

        [ObservableProperty]
        private int _optimizeChangeCount;

        [ObservableProperty]
        private bool _optimizeApplyVisible;

        [ObservableProperty]
        private string _optimizeApplyButtonText = "Apply Changes";

        [ObservableProperty]
        private bool _optimizeApplyEnabled = true;

        private UpdateCheckResult? _pendingUpdate;

        private bool _ignoreChange;

        public ExtraSettingsViewModel()
        {
            // Pick up background-discovered updates, even if discovery happened before
            // the user opened this panel.
            if (UpdateNotifier.Latest is { Status: UpdateStatus.UpdateAvailable } cached)
                ApplyDiscoveredUpdate(cached);

            UpdateNotifier.UpdateDiscovered += OnUpdateDiscovered;
        }

        private void OnUpdateDiscovered(UpdateCheckResult result)
        {
            Application.Current?.Dispatcher.Invoke(() => ApplyDiscoveredUpdate(result));
        }

        private void ApplyDiscoveredUpdate(UpdateCheckResult result)
        {
            _pendingUpdate = result;
            UpdateAvailable = true;
            UpdateStatusText = $"Update available: {result.LatestVersion}";
            UpdateButtonText = $"Install {result.LatestVersion}";
        }

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
            // Force tray icon to repaint with new color mode
            int perf = Modes.GetCurrent();
            int gpu = AppConfig.Get("gpu_mode");
            TrayIconService.Instance?.UpdateIcon(perf, gpu);
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

        partial void OnWindowOpacityChanged(double value)
        {
            if (_ignoreChange) return;
            // Clamp + persist (stored as 0–100 for readability in config)
            double clamped = Math.Clamp(value, 0.60, 1.00);
            AppConfig.Set("window_opacity", (int)(clamped * 100));
            ApplyWindowOpacity(clamped);
        }

        private static void ApplyWindowOpacity(double opacity)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app == null) return;

                    byte alpha = (byte)Math.Clamp((int)(opacity * 255), 0x60, 0xFF);
                    // Neutral near-OLED black — matches WindowBackgroundBrush in Theme.xaml.
                    // If you change the color there, change it here too.
                    var color = System.Windows.Media.Color.FromArgb(alpha, 0x08, 0x08, 0x08);

                    // Replace the whole brush — XAML resources may be frozen, so we can't
                    // mutate .Color in place. Replacing the dictionary entry forces
                    // DynamicResource subscribers to re-resolve.
                    app.Resources["WindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(color);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Window opacity apply error: " + ex.Message);
                }
            });
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
        private async Task CheckForUpdates()
        {
            // Known update (background or previous click): go straight to the dialog.
            if (_pendingUpdate != null)
            {
                ShowPendingUpdateDialog(_pendingUpdate);
                return;
            }

            UpdateButtonEnabled = false;
            UpdateButtonText = "Checking...";
            UpdateStatusText = "";

            var result = await UpdateService.CheckAsync();
            UpdateNotifier.MarkChecked();

            switch (result.Status)
            {
                case UpdateStatus.UpToDate:
                    UpdateStatusText = $"Up to date (v{result.CurrentVersion})";
                    UpdateButtonText = "Check for Updates";
                    UpdateAvailable = false;
                    break;

                case UpdateStatus.UpdateAvailable:
                    // User asked, so we surface immediately — ignore any "skipped" preference.
                    ApplyDiscoveredUpdate(result);
                    ShowPendingUpdateDialog(result);
                    break;

                case UpdateStatus.CheckFailed:
                    UpdateStatusText = "Check failed — try again later";
                    UpdateButtonText = "Check for Updates";
                    UpdateAvailable = false;
                    break;
            }

            UpdateButtonEnabled = true;
        }

        [RelayCommand]
        private void ShowChangelog()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Views.ChangelogWindow.ShowHistory(Application.Current?.MainWindow);
            });
        }

        private void ShowPendingUpdateDialog(UpdateCheckResult update)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var outcome = Views.ChangelogWindow.ShowPendingUpdate(Application.Current?.MainWindow, update);
                if (outcome == UpdateDialogOutcome.Skipped)
                {
                    UpdateNotifier.SetSkippedVersion(update.LatestVersion);
                    _pendingUpdate = null;
                    UpdateAvailable = false;
                    UpdateStatusText = $"Skipped {update.LatestVersion}";
                    UpdateButtonText = "Check for Updates";
                }
            });
        }

        [RelayCommand]
        private async Task OptimizeForMe()
        {
            // Always just runs analysis — apply is a separate action inside the card
            OptimizeButtonEnabled = false;
            OptimizeButtonText = "Analyzing...";
            OptimizeResultVisible = false;

            var result = await Task.Run(() => AutoConfigService.GenerateRecommendations());

            OptimizeProfileName = result.ProfileName;
            OptimizeReason = result.Reason;
            OptimizeRecommendations = result.Recommendations;
            OptimizeChangeCount = result.ChangeCount;
            OptimizeResultVisible = true;

            if (result.ChangeCount > 0)
            {
                OptimizeButtonText = "Re-analyze";
                OptimizeApplyButtonText = $"Apply {result.ChangeCount} Change{(result.ChangeCount == 1 ? "" : "s")}";
                OptimizeApplyVisible = true;
            }
            else
            {
                OptimizeButtonText = "Re-analyze";
                OptimizeApplyButtonText = "Already Optimal";
                OptimizeApplyVisible = false;
            }

            OptimizeButtonEnabled = true;
        }

        [RelayCommand]
        private async Task ApplyOptimize()
        {
            if (OptimizeChangeCount <= 0) return;

            OptimizeApplyEnabled = false;
            OptimizeApplyButtonText = "Applying...";

            await Task.Run(() =>
            {
                foreach (var rec in OptimizeRecommendations.Where(r => r.IsChange))
                {
                    try
                    {
                        rec.Apply();
                        Logger.WriteLine($"AutoConfig applied: {rec.SettingName} → {rec.RecommendedValue}");
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine($"AutoConfig failed: {rec.SettingName} — {ex.Message}");
                    }
                }
            });

            ToastService.Show($"{OptimizeProfileName} preset applied", ToastType.Success);

            OptimizeApplyButtonText = "Applied!";
            await Task.Delay(1500);

            // Reset card
            OptimizeResultVisible = false;
            OptimizeApplyVisible = false;
            OptimizeApplyEnabled = true;
            OptimizeButtonText = "Optimize for Me";
        }

        [RelayCommand]
        private void DismissOptimize()
        {
            OptimizeResultVisible = false;
            OptimizeApplyVisible = false;
            OptimizeButtonText = "Optimize for Me";
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to open URL: " + ex.Message);
            }
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                // Config reads are instant (in-memory)
                FnLockEnabled = AppConfig.Is("fn_lock");
                GpuFixEnabled = AppConfig.Is("gpu_fix");
                NoOverdrive = AppConfig.Is("no_overdrive");
                WindowTopmost = AppConfig.Is("topmost");
                BootSound = AppConfig.Is("boot_sound");
                BwTrayIcon = AppConfig.Is("bw_icon");
                AdvancedRgb = AppConfig.Is("advanced_rgb");
                StopArmoryCrate = AppConfig.IsStopAC();

                int savedOpacity = AppConfig.Get("window_opacity", 92);
                if (savedOpacity < 60 || savedOpacity > 100) savedOpacity = 92;
                WindowOpacity = savedOpacity / 100.0;
                ApplyWindowOpacity(WindowOpacity);
            }
            finally
            {
                _ignoreChange = false;
            }

            // Task Scheduler COM query — slow, push to background.
            Task.Run(() =>
            {
                bool scheduled = false;
                try { scheduled = Startup.IsScheduled(); }
                catch (Exception ex) { Logger.WriteLine("Startup scheduled check error: " + ex.Message); }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _ignoreChange = true;
                    try { RunOnStartup = scheduled; }
                    finally { _ignoreChange = false; }
                });
            });

            RefreshAsusServices();
        }
    }
}
