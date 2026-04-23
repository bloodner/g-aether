using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.Gpu;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    public partial class GpuViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _selectedModeIndex = 1;

        [ObservableProperty]
        private int _schematicMode = 1;

        [ObservableProperty]
        private bool _showRestart;

        [ObservableProperty]
        private bool _isRestartPending;

        [ObservableProperty]
        private bool _hasEco = true;

        [ObservableProperty]
        private bool _hasMux;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private string _headerText = "GPU Mode";

        [ObservableProperty]
        private string[] _modeLabels = ["Eco", "Standard", "Ultimate", "Optimized"];

        [ObservableProperty]
        private string[] _modeIcons = ["\uE8BE", "\uE7F4", "\uE945", "\uE895"];

        [ObservableProperty]
        private Color[] _modeColors = [
            ThemeService.ColorEco,
            ThemeService.ColorStandard,
            ThemeService.ColorUltimate,
            ThemeService.ColorOptimized,
        ];

        private bool _ignoreChange;
        private int _currentHardwareMode = -1;

        private int LabelToMode(int index)
        {
            if (index < 0 || index >= ModeLabels.Length) return 1;
            return ModeLabels[index] switch
            {
                "Eco" => 0,
                "Standard" => 1,
                "Ultimate" => 2,
                "Optimized" => 3,
                _ => 1
            };
        }

        partial void OnSelectedModeIndexChanged(int value)
        {
            if (_ignoreChange) return;

            int mode = LabelToMode(value);

            int gpuMode = mode switch
            {
                0 => AsusACPI.GPUModeEco,
                2 => AsusACPI.GPUModeUltimate,
                _ => AsusACPI.GPUModeStandard,
            };

            SchematicMode = mode;

            // Show restart warning when mode requires reboot (Ultimate or switching away from Ultimate)
            int currentMode = _currentHardwareMode;
            bool needsRestart = (mode == 2) || (currentMode == AsusACPI.GPUModeUltimate && mode != 2);
            ShowRestart = needsRestart;

            // If the user picked a mode that no longer needs restart, abort any pending shutdown.
            if (!needsRestart && IsRestartPending) CancelRestart();

            UpdateHeaderText(mode);

            AppConfig.Set("gpu_auto", mode == 3 ? 1 : 0);
            AppConfig.Set("gpu_mode", gpuMode);
        }

        public void SetFromGpuMode(int gpuMode)
        {
            _ignoreChange = true;
            try
            {
                string targetLabel = gpuMode switch
                {
                    AsusACPI.GPUModeEco => "Eco",
                    AsusACPI.GPUModeUltimate => "Ultimate",
                    _ => AppConfig.Is("gpu_auto") ? "Optimized" : "Standard"
                };

                int index = Array.IndexOf(ModeLabels, targetLabel);
                if (index < 0) index = Array.IndexOf(ModeLabels, "Standard");
                if (index < 0) index = 0;

                int mode = LabelToMode(index);
                SelectedModeIndex = index;
                SchematicMode = mode;
                ShowRestart = false;
                if (IsRestartPending) CancelRestart();
                UpdateHeaderText(mode);
            }
            finally
            {
                _ignoreChange = false;
            }
        }

        private void UpdateHeaderText(int mode)
        {
            HeaderText = mode switch
            {
                0 => "GPU Mode: iGPU Only (Eco)",
                1 => "GPU Mode: iGPU + dGPU",
                2 => "GPU Mode: dGPU Direct",
                3 => "GPU Mode: iGPU + dGPU (Auto)",
                _ => "GPU Mode"
            };
        }

        public void SetGpuButtons(bool hasEco, bool hasMux)
        {
            HasEco = hasEco;
            HasMux = hasMux;

            Color eco = ThemeService.ColorEco;
            Color std = ThemeService.ColorStandard;
            Color ult = ThemeService.ColorUltimate;
            Color opt = ThemeService.ColorOptimized;

            if (!hasEco && !hasMux)
            {
                ModeLabels = ["Standard"];
                ModeIcons = ["\uE7F4"];
                ModeColors = [std];
            }
            else if (!hasMux)
            {
                ModeLabels = ["Eco", "Standard", "Optimized"];
                ModeIcons = ["\uE8BE", "\uE7F4", "\uE895"];
                ModeColors = [eco, std, opt];
            }
            else
            {
                ModeLabels = ["Eco", "Standard", "Ultimate", "Optimized"];
                ModeIcons = ["\uE8BE", "\uE7F4", "\uE945", "\uE895"];
                ModeColors = [eco, std, ult, opt];
            }
        }

        public void SetVisibility(bool gpuExists)
        {
            IsVisible = gpuExists;
        }

        public void InitFromCurrent()
        {
            int gpuMode = AppConfig.Get("gpu_mode");
            _currentHardwareMode = gpuMode;
            if (gpuMode >= 0) SetFromGpuMode(gpuMode);
        }

        [RelayCommand]
        private void RestartNow()
        {
            // /t 30 leaves Windows' own "you're about to be signed out" notification
            // visible long enough for the user to cancel via our button or `shutdown /a`.
            if (TryRunShutdown("/r /t 30 /c \"G-Aether: applying GPU mode change\""))
                IsRestartPending = true;
        }

        [RelayCommand]
        private void CancelRestart()
        {
            if (TryRunShutdown("/a"))
                IsRestartPending = false;
        }

        private static bool TryRunShutdown(string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                });
                p?.WaitForExit(2000);
                return (p?.ExitCode ?? 1) == 0;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("shutdown " + args + " failed: " + ex.Message);
                return false;
            }
        }
    }
}
