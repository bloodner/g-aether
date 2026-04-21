using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// A one-shot "scene" — a named bundle of settings that apply in one click.
    /// Scenes aren't persistent modes; once applied the individual controls can
    /// drift, so we don't track a "currently active" scene.
    /// </summary>
    public class Scene
    {
        public required string Name { get; init; }
        public required string Icon { get; init; }      // Segoe MDL2 Assets glyph
        public required string Tooltip { get; init; }
        public required Action<MainViewModel> Apply { get; init; }
    }

    public static class SceneService
    {
        /// <summary>Built-in scenes shown in the footer strip.</summary>
        public static IReadOnlyList<Scene> BuiltInScenes { get; } = new List<Scene>
        {
            new()
            {
                Name = "Reading",
                Icon = "\uE736",  // Library / book
                Tooltip = "Warm colors, 60Hz, quiet fans, low-power GPU — kind to your eyes during long reading sessions.",
                Apply = vm =>
                {
                    SetColorTemp(vm, warmIndex: 1);      // Warmer (index 1 of 0..6)
                    SelectFreq(vm, "60");
                    SelectPerf(vm, "Silent");
                    SelectGpu(vm, "Eco");
                },
            },
            new()
            {
                Name = "Focus",
                Icon = "\uE81B",  // Target
                Tooltip = "Silent + Eco so nothing distracts — no fan noise, no GPU noise.",
                Apply = vm =>
                {
                    SelectPerf(vm, "Silent");
                    SelectGpu(vm, "Eco");
                },
            },
            new()
            {
                Name = "Work",
                Icon = "\uE8FC",  // Briefcase
                Tooltip = "Daily driver: Balanced + Standard GPU + Auto refresh + neutral colors — responsive workspace without any special-occasion overrides.",
                Apply = vm =>
                {
                    SelectPerf(vm, "Balanced");
                    SelectGpuPreferred(vm, "Standard", "Optimized");
                    SelectFreq(vm, "Auto");
                    SetColorTemp(vm, warmIndex: 3);        // Neutral
                    vm.Keyboard.BacklightBrightness = 2;   // Medium
                },
            },
            new()
            {
                Name = "Present",
                Icon = "\uE953",  // Projector / presentation
                Tooltip = "Presentation mode: Balanced performance with a stable 60Hz — no fan surprises while you're on stage.",
                Apply = vm =>
                {
                    SelectPerf(vm, "Balanced");
                    SelectGpu(vm, "Standard");
                    SelectFreq(vm, "60");
                },
            },
            new()
            {
                Name = "Night",
                Icon = "\uE706",  // Brightness (placeholder — moon glyph isn't in Segoe MDL2)
                Tooltip = "Night Silent: Silent perf, Eco GPU, keyboard backlight off — quiet dark-hours setup.",
                Apply = vm =>
                {
                    SelectPerf(vm, "Silent");
                    SelectGpu(vm, "Eco");
                    vm.Keyboard.BacklightBrightness = 0;
                },
            },
            new()
            {
                Name = "Game",
                Icon = "\uE7FC",  // Game controller
                Tooltip = "Go fast: Turbo perf, Ultimate GPU (or Standard), max refresh, neutral colors, full keyboard backlight.",
                Apply = vm =>
                {
                    SelectPerf(vm, "Turbo");
                    SelectGpuPreferred(vm, "Ultimate", "Standard");
                    SelectMaxFreq(vm);
                    SetColorTemp(vm, warmIndex: 3);        // Neutral (middle of 0..6)
                    vm.Keyboard.BacklightBrightness = 3;   // Max
                },
            },
        };

        // --- Helpers -------------------------------------------------------

        /// <summary>
        /// Fuzzy-match a frequency label (e.g. "60Hz", "60", "60.00Hz"). Skips if
        /// the device doesn't expose a matching rate — scenes tolerate hardware
        /// differences.
        /// </summary>
        private static void SelectFreq(MainViewModel vm, string prefix)
        {
            var labels = vm.Visual.FrequencyLabels;
            int idx = Array.FindIndex(labels, l => l != null && l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) vm.Visual.SelectedFreqIndex = idx;
        }

        private static void SelectPerf(MainViewModel vm, string name)
        {
            int idx = Array.IndexOf(vm.Performance.ModeLabels, name);
            if (idx >= 0) vm.Performance.SelectedModeIndex = idx;
        }

        private static void SelectGpu(MainViewModel vm, string name)
        {
            int idx = Array.IndexOf(vm.Gpu.ModeLabels, name);
            if (idx >= 0) vm.Gpu.SelectedModeIndex = idx;
        }

        /// <summary>
        /// Try each GPU mode name in order, apply the first one that exists.
        /// Used for scenes like Game where Ultimate is preferred but devices
        /// without a MUX fall back to Standard.
        /// </summary>
        private static void SelectGpuPreferred(MainViewModel vm, params string[] names)
        {
            foreach (var name in names)
            {
                int idx = Array.IndexOf(vm.Gpu.ModeLabels, name);
                if (idx >= 0)
                {
                    vm.Gpu.SelectedModeIndex = idx;
                    return;
                }
            }
        }

        /// <summary>
        /// Pick the highest numeric refresh rate the device supports. Skips
        /// "Auto" (index 0) and picks whichever later entry parses to the
        /// largest value.
        /// </summary>
        private static void SelectMaxFreq(MainViewModel vm)
        {
            var labels = vm.Visual.FrequencyLabels;
            int best = -1;
            int bestRate = 0;
            for (int i = 1; i < labels.Length; i++)
            {
                string clean = labels[i].Replace("Hz", "").Replace("+OD", "").Trim();
                if (int.TryParse(clean, out int rate) && rate > bestRate)
                {
                    bestRate = rate;
                    best = i;
                }
            }
            if (best >= 0) vm.Visual.SelectedFreqIndex = best;
        }

        private static void SetColorTemp(MainViewModel vm, int warmIndex)
        {
            // Color temp runs 0..6 (Warmest .. Coldest). Clamp so an unusual device
            // with fewer steps doesn't throw.
            int clamped = Math.Clamp(warmIndex, 0, 6);
            vm.Visual.ColorTempIndex = clamped;
        }
    }
}
