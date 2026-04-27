// app/GHelper.WPF/ViewModels/ModeStripViewModel.cs
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    /// <summary>
    /// Single source of truth for the four mode badges (Performance, GPU,
    /// Display, Services). Both the main window's strip and the gadget's
    /// strip bind to these properties so any change applies in lockstep.
    /// Owned by MainViewModel; subscribes to its child VMs to recompute.
    /// </summary>
    public partial class ModeStripViewModel : ObservableObject
    {
        private readonly PerformanceViewModel _performance;
        private readonly GpuViewModel _gpu;
        private readonly VisualModeViewModel _visual;
        private readonly ExtraSettingsViewModel _extra;

        [ObservableProperty] private string _perfBadgeName = "Balanced";
        [ObservableProperty] private Brush _perfBadgeBrush =
            new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
        [ObservableProperty] private bool _perfBadgeIsWarning;

        [ObservableProperty] private string _gpuBadgeName = "Standard";
        [ObservableProperty] private Brush _gpuBadgeBrush =
            new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
        [ObservableProperty] private bool _gpuBadgeIsWarning;

        [ObservableProperty] private string _displayBadgeName = "Auto";
        [ObservableProperty] private Brush _displayBadgeBrush =
            new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));

        [ObservableProperty] private string _servicesBadgeName = "Healthy";
        [ObservableProperty] private Brush _servicesBadgeBrush =
            new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));
        [ObservableProperty] private bool _servicesBadgeIsWarning;

        // Header accent hairline — green when healthy, orange/red when any badge is warning.
        [ObservableProperty] private Brush _headerAccentBrush =
            new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));

        private static Brush ModeBrush(Color c) => new SolidColorBrush(c);

        public ModeStripViewModel(
            PerformanceViewModel performance,
            GpuViewModel gpu,
            VisualModeViewModel visual,
            ExtraSettingsViewModel extra)
        {
            _performance = performance;
            _gpu = gpu;
            _visual = visual;
            _extra = extra;

            _performance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PerformanceViewModel.SelectedModeIndex) ||
                    e.PropertyName == nameof(PerformanceViewModel.ModeLabels))
                    UpdatePerfBadge();
            };
            _gpu.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GpuViewModel.SelectedModeIndex) ||
                    e.PropertyName == nameof(GpuViewModel.ModeLabels) ||
                    e.PropertyName == nameof(GpuViewModel.ModeColors))
                    UpdateGpuBadge();
            };
            _visual.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(VisualModeViewModel.SelectedFreqIndex) ||
                    e.PropertyName == nameof(VisualModeViewModel.FrequencyLabels) ||
                    e.PropertyName == nameof(VisualModeViewModel.IsAutoFreqMode))
                    UpdateDisplayBadge();
            };
            _extra.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExtraSettingsViewModel.AsusServicesCount) ||
                    e.PropertyName == nameof(ExtraSettingsViewModel.AsusServicesText))
                    UpdateServicesBadge();
            };

            UpdatePerfBadge();
            UpdateGpuBadge();
            UpdateDisplayBadge();
            UpdateServicesBadge();
        }

        private void UpdatePerfBadge()
        {
            int idx = _performance.SelectedModeIndex;
            var labels = _performance.ModeLabels;
            if (idx < 0 || idx >= labels.Length) return;

            string name = labels[idx];
            PerfBadgeName = name;
            PerfBadgeBrush = name switch
            {
                "Silent" => ModeBrush(ThemeService.ColorSilent),
                "Turbo" => ModeBrush(ThemeService.ColorTurbo),
                _ => ModeBrush(ThemeService.ColorBalanced),
            };
            PerfBadgeIsWarning = name == "Turbo";
            UpdateHeaderAccent();
        }

        private void UpdateGpuBadge()
        {
            int idx = _gpu.SelectedModeIndex;
            var labels = _gpu.ModeLabels;
            if (idx < 0 || idx >= labels.Length) return;

            string name = labels[idx];
            GpuBadgeName = name;
            GpuBadgeBrush = name switch
            {
                "Eco" => ModeBrush(ThemeService.ColorEco),
                "Ultimate" => ModeBrush(ThemeService.ColorUltimate),
                "Optimized" => ModeBrush(ThemeService.ColorOptimized),
                _ => ModeBrush(ThemeService.ColorStandard),
            };
            GpuBadgeIsWarning = name == "Ultimate";
            UpdateHeaderAccent();
        }

        private void UpdateDisplayBadge()
        {
            int idx = _visual.SelectedFreqIndex;
            var labels = _visual.FrequencyLabels;
            if (_visual.IsAutoFreqMode)
                DisplayBadgeName = "Auto";
            else if (idx >= 0 && idx < labels.Length)
                DisplayBadgeName = labels[idx];

            DisplayBadgeBrush = _visual.IsAutoFreqMode
                ? ModeBrush(ThemeService.ColorBalanced)
                : ModeBrush(ThemeService.ColorOptimized);
            UpdateHeaderAccent();
        }

        private void UpdateServicesBadge()
        {
            int count = _extra.AsusServicesCount;
            if (count <= 0)
            {
                ServicesBadgeName = "Healthy";
                ServicesBadgeBrush = ModeBrush(ThemeService.ColorEco);
                ServicesBadgeIsWarning = false;
            }
            else
            {
                ServicesBadgeName = count == 1 ? "1 Running" : $"{count} Running";
                ServicesBadgeBrush = ModeBrush(ThemeService.ColorTurbo);
                ServicesBadgeIsWarning = true;
            }
            UpdateHeaderAccent();
        }

        private void UpdateHeaderAccent()
        {
            bool anyWarning = ServicesBadgeIsWarning || PerfBadgeIsWarning || GpuBadgeIsWarning;
            HeaderAccentBrush = anyWarning
                ? ModeBrush(ThemeService.ColorTurbo)
                : ModeBrush(ThemeService.ColorEco);
        }

        /// <summary>
        /// Allows MainViewModel's sensor tick to ask for a forced refresh
        /// (e.g., after a hotkey-triggered mode change) without exposing
        /// every Update*Badge method individually.
        /// </summary>
        public void Refresh()
        {
            UpdatePerfBadge();
            UpdateGpuBadge();
            UpdateDisplayBadge();
            UpdateServicesBadge();
        }
    }
}
