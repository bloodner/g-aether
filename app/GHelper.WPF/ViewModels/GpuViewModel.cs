using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
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
            Color.FromRgb(0x4C, 0xC9, 0x5E), // Eco: green
            Color.FromRgb(0x60, 0xCD, 0xFF), // Standard: blue (accent)
            Color.FromRgb(0xFF, 0x6B, 0x35), // Ultimate: orange
            Color.FromRgb(0xAB, 0x7C, 0xFF), // Optimized: purple
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

            string modeName = value < ModeLabels.Length ? ModeLabels[value] : "Unknown";
            ToastService.Show($"GPU Mode: {modeName}", ToastType.Success);

            // Show restart warning when mode requires reboot (Ultimate or switching away from Ultimate)
            int currentMode = _currentHardwareMode;
            bool needsRestart = (mode == 2) || (currentMode == AsusACPI.GPUModeUltimate && mode != 2);
            ShowRestart = needsRestart;

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
                3 => "GPU Mode: Auto Optimized",
                _ => "GPU Mode"
            };
        }

        public void SetGpuButtons(bool hasEco, bool hasMux)
        {
            HasEco = hasEco;
            HasMux = hasMux;

            Color eco = Color.FromRgb(0x4C, 0xC9, 0x5E);
            Color std = Color.FromRgb(0x60, 0xCD, 0xFF);
            Color ult = Color.FromRgb(0xFF, 0x6B, 0x35);
            Color opt = Color.FromRgb(0xAB, 0x7C, 0xFF);

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
    }
}
