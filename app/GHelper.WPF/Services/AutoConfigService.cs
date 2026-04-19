using System.Windows;
using GHelper.Gpu;
using GHelper.Mode;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Services
{
    public class AutoConfigResult
    {
        public UsageProfile Profile { get; init; }
        public string ProfileName { get; init; } = "";
        public string Reason { get; init; } = "";
        public List<Recommendation> Recommendations { get; init; } = new();
        public int ChangeCount => Recommendations.Count(r => r.IsChange);
    }

    public static class AutoConfigService
    {
        public static AutoConfigResult GenerateRecommendations()
        {
            var stats = UsageAnalyzer.Analyze();

            var (perfMode, perfName) = GetRecommendedPerfMode(stats.Profile);
            var (gpuMode, gpuAuto, gpuName) = GetRecommendedGpuMode(stats.Profile);
            var (screenAuto, screenName) = GetRecommendedScreenMode(stats.Profile);

            int currentPerf = Modes.GetCurrent();
            string currentPerfName = currentPerf switch
            {
                AsusACPI.PerformanceSilent => "Silent",
                AsusACPI.PerformanceTurbo => "Turbo",
                _ => "Balanced"
            };

            int currentGpu = AppConfig.Get("gpu_mode");
            bool currentGpuAuto = AppConfig.Is("gpu_auto");
            string currentGpuName = currentGpu switch
            {
                AsusACPI.GPUModeEco => "Eco",
                AsusACPI.GPUModeUltimate => "Ultimate",
                _ => currentGpuAuto ? "Optimized" : "Standard"
            };

            bool currentScreenAuto = AppConfig.Is("screen_auto");
            string currentScreenName = currentScreenAuto ? "Auto" : "Manual";

            var recommendations = new List<Recommendation>
            {
                new()
                {
                    SettingName = "Performance Mode",
                    CurrentValue = currentPerfName,
                    RecommendedValue = perfName,
                    Reason = GetPerfReason(stats.Profile),
                    Apply = () => RouteToViewModel(vm =>
                    {
                        int idx = Array.IndexOf(vm.Performance.ModeLabels, perfName);
                        if (idx >= 0) vm.Performance.SelectedModeIndex = idx;
                        else
                        {
                            // Fallback: labels not initialized; poke the hardware + config directly.
                            AppConfig.Set("performance_mode", perfMode);
                            Program.modeControl?.SetPerformanceMode(perfMode);
                        }
                    })
                },
                new()
                {
                    SettingName = "GPU Mode",
                    CurrentValue = currentGpuName,
                    RecommendedValue = gpuName,
                    Reason = GetGpuReason(stats.Profile),
                    Apply = () => RouteToViewModel(vm =>
                    {
                        int idx = Array.IndexOf(vm.Gpu.ModeLabels, gpuName);
                        if (idx >= 0) vm.Gpu.SelectedModeIndex = idx;
                        else
                        {
                            AppConfig.Set("gpu_auto", gpuAuto ? 1 : 0);
                            AppConfig.Set("gpu_mode", gpuMode);
                            Program.gpuControl?.SetGPUMode(gpuMode, gpuAuto ? 1 : 0);
                        }
                    })
                },
                new()
                {
                    SettingName = "Screen Refresh",
                    CurrentValue = currentScreenName,
                    RecommendedValue = screenName,
                    Reason = GetScreenReason(stats.Profile),
                    Apply = () => RouteToViewModel(vm =>
                    {
                        // Auto is always index 0 in FrequencyLabels. Setting it triggers
                        // OnSelectedFreqIndexChanged which handles config + ScreenControl.
                        if (screenAuto)
                        {
                            vm.Visual.SelectedFreqIndex = 0;
                        }
                        else if (vm.Visual.FrequencyLabels.Length > 1)
                        {
                            // Manual: pick the highest non-Auto rate (last label).
                            vm.Visual.SelectedFreqIndex = vm.Visual.FrequencyLabels.Length - 1;
                        }
                    })
                },
            };

            string profileName = stats.Profile switch
            {
                UsageProfile.LightMobile => "Battery Saver",
                UsageProfile.HeavyMobile => "Mobile Performance",
                UsageProfile.DesktopCasual => "Productivity",
                UsageProfile.DesktopGaming => "Maximum Performance",
                UsageProfile.Mixed => "Balanced",
                _ => "Balanced"
            };

            return new AutoConfigResult
            {
                Profile = stats.Profile,
                ProfileName = profileName,
                Reason = stats.ProfileReason,
                Recommendations = recommendations,
            };
        }

        /// <summary>
        /// Apply by driving the VM's selected-index property on the UI thread. Setting
        /// that property runs the existing OnChanged handler which does config + hardware
        /// + property-change notification in one place — the same path a real user click
        /// takes, so every downstream listener (status bar, tray icon, schematic) updates.
        /// </summary>
        private static void RouteToViewModel(Action<MainViewModel> action)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (app.MainWindow?.DataContext is MainViewModel vm)
                        action(vm);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("AutoConfig route-to-VM error: " + ex.Message);
                }
            });
        }

        private static (int mode, string name) GetRecommendedPerfMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (AsusACPI.PerformanceSilent, "Silent"),
            UsageProfile.HeavyMobile => (AsusACPI.PerformanceBalanced, "Balanced"),
            UsageProfile.DesktopCasual => (AsusACPI.PerformanceBalanced, "Balanced"),
            UsageProfile.DesktopGaming => (AsusACPI.PerformanceTurbo, "Turbo"),
            _ => (AsusACPI.PerformanceBalanced, "Balanced"),
        };

        private static (int mode, bool auto, string name) GetRecommendedGpuMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (AsusACPI.GPUModeEco, false, "Eco"),
            UsageProfile.HeavyMobile => (AsusACPI.GPUModeStandard, true, "Optimized"),
            UsageProfile.DesktopCasual => (AsusACPI.GPUModeStandard, true, "Optimized"),
            UsageProfile.DesktopGaming => (AsusACPI.GPUModeStandard, false, "Standard"),
            _ => (AsusACPI.GPUModeStandard, true, "Optimized"),
        };

        private static (bool auto, string name) GetRecommendedScreenMode(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => (true, "Auto"),
            UsageProfile.HeavyMobile => (true, "Auto"),
            UsageProfile.DesktopCasual => (true, "Auto"),
            UsageProfile.DesktopGaming => (false, "Manual"),
            _ => (true, "Auto"),
        };

        private static string GetPerfReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Minimizes fan noise and power draw on battery",
            UsageProfile.HeavyMobile => "Good balance of performance and battery life",
            UsageProfile.DesktopCasual => "Quiet operation with responsive performance",
            UsageProfile.DesktopGaming => "Maximum CPU/GPU clocks for demanding tasks",
            _ => "Good all-around default",
        };

        private static string GetGpuReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Disables dGPU to maximize battery life",
            UsageProfile.HeavyMobile => "Auto-switches GPU based on power source",
            UsageProfile.DesktopCasual => "Auto-switches to Eco on battery, Standard when plugged in",
            UsageProfile.DesktopGaming => "Keeps dGPU always available for gaming",
            _ => "Smart switching balances performance and battery",
        };

        private static string GetScreenReason(UsageProfile profile) => profile switch
        {
            UsageProfile.LightMobile => "Drops to 60Hz on battery to save power",
            UsageProfile.HeavyMobile => "Auto-adjusts refresh rate based on power source",
            UsageProfile.DesktopCasual => "Auto-adjusts for best experience",
            UsageProfile.DesktopGaming => "Keep manual control for maximum refresh rate",
            _ => "Auto-adjusts for best experience",
        };
    }
}
