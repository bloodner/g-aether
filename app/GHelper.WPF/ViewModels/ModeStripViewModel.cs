// app/GHelper.WPF/ViewModels/ModeStripViewModel.cs
using System.Linq;
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

        // ---- Active Scene (for the compact mode-strip variant) ----
        // Records the most recently applied scene so the gadget's compact strip
        // can display "🎮 Game · Turbo ●" style. Survives drift — we don't try
        // to detect when manual tweaks have invalidated the scene name.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasActiveScene))]
        private string _activeSceneName = "";

        [ObservableProperty]
        private string _activeSceneIcon = "";

        public bool HasActiveScene => !string.IsNullOrEmpty(ActiveSceneName);

        // ---- Health summary brush (for the compact strip's right-side dot) ----
        [ObservableProperty] private Brush _healthBrush =
            new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));  // green by default

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
            // Watch both — AsusServicesText changes mid-stop ("Stopping...") even when
            // the count hasn't decremented yet, and we want the badge to refresh in
            // case the count is updated atomically with the text on a future change.
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
            UpdateHealthBrush();

            // Rehydrate active scene from last session.
            string? savedSceneName = AppConfig.GetString("active_scene");
            if (!string.IsNullOrEmpty(savedSceneName))
            {
                var saved = SceneService.BuiltInScenes.FirstOrDefault(s => s.Name == savedSceneName);
                if (saved != null) SetActiveScene(saved);
            }
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
            UpdateHealthBrush();
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
            UpdateHealthBrush();
        }

        private void UpdateDisplayBadge()
        {
            int idx = _visual.SelectedFreqIndex;
            var labels = _visual.FrequencyLabels;
            if (idx < 0 || idx >= labels.Length)
            {
                DisplayBadgeName = "Auto";
            }
            else
            {
                DisplayBadgeName = labels[idx];
            }
            // Auto refresh rate means "system decides" — same semantic as GPU Optimized,
            // so it uses the same magenta. Any fixed rate uses the plain accent.
            DisplayBadgeBrush = _visual.IsAutoFreqMode
                ? ModeBrush(ThemeService.ColorOptimized)
                : ModeBrush(ThemeService.AccentColor);
        }

        private void UpdateServicesBadge()
        {
            int count = _extra.AsusServicesCount;
            if (count == 0)
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
            UpdateHealthBrush();
        }

        private void UpdateHealthBrush()
        {
            bool anyWarning = PerfBadgeIsWarning || GpuBadgeIsWarning || ServicesBadgeIsWarning;
            HealthBrush = anyWarning
                ? ModeBrush(ThemeService.ColorTurbo)   // amber/orange for warnings
                : ModeBrush(ThemeService.ColorEco);    // green for healthy
        }

        /// <summary>
        /// Records the most recently applied scene. Called by MainViewModel.ApplyScene.
        /// </summary>
        public void SetActiveScene(Scene scene)
        {
            ActiveSceneName = scene.Name;
            ActiveSceneIcon = scene.Icon;
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
