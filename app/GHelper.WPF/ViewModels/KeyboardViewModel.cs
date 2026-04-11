using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.AnimeMatrix;
using GHelper.USB;
using System.Windows.Media;

namespace GHelper.WPF.ViewModels
{
    public partial class KeyboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _backlightBrightness = 3; // 0-3

        [ObservableProperty]
        private string _brightnessText = "Medium";

        // Aura mode
        [ObservableProperty]
        private string[] _auraModeLabels = [];

        [ObservableProperty]
        private string[] _auraModeIcons = [];

        [ObservableProperty]
        private int _selectedAuraModeIndex;

        [ObservableProperty]
        private bool _showColorPicker = true; // modes like Rainbow/ColorCycle don't need color

        [ObservableProperty]
        private bool _showSecondColor; // Breathe mode on non-ACPI supports second color

        // Colors
        [ObservableProperty]
        private Color _auraColor1 = Colors.White;

        [ObservableProperty]
        private Color _auraColor2 = Colors.Black;

        [ObservableProperty]
        private int _animationSpeed = 1; // 0=Slow, 1=Medium, 2=Fast

        [ObservableProperty]
        private string _speedText = "Medium";

        // Slash Lighting
        [ObservableProperty]
        private bool _hasSlash;

        [ObservableProperty]
        private int _slashBrightness; // 0=Off, 1=Low, 2=Medium, 3=High

        [ObservableProperty]
        private string _slashBrightnessText = "Off";

        [ObservableProperty]
        private string[] _slashModeLabels = [];

        [ObservableProperty]
        private int _selectedSlashMode;

        [ObservableProperty]
        private bool _slashBatterySaver;

        [ObservableProperty]
        private bool _slashSleepActive = true;

        [ObservableProperty]
        private bool _slashLidAnimation = true;

        private bool _ignoreChange;

        // Map from pill index to AuraMode enum value
        private AuraMode[] _auraModeValues = [];

        private static readonly string[] BrightnessLabels = ["Off", "Low", "Medium", "High"];
        private static readonly string[] SpeedLabels = ["Slow", "Medium", "Fast"];

        // Segoe MDL2 Assets icons for each aura mode
        private static readonly Dictionary<AuraMode, string> ModeIcons = new()
        {
            { AuraMode.AuraStatic,     "\uEA80" },  // StatusCircleInner (solid dot)
            { AuraMode.AuraBreathe,    "\uE010" },  // Waves (breathe/pulse)
            { AuraMode.AuraColorCycle, "\uE8B1" },  // Sync (cycle)
            { AuraMode.AuraRainbow,    "\uE790" },  // Brightness (rainbow/color)
            { AuraMode.Star,           "\uE734" },  // FavoriteStar
            { AuraMode.Rain,           "\uE9C4" },  // Precipitation
            { AuraMode.Highlight,      "\uE7E8" },  // Highlight
            { AuraMode.Laser,          "\uE9D9" },  // Diagnostic (beam)
            { AuraMode.Ripple,         "\uE9CA" },  // Frigid (ripple)
            { AuraMode.AuraStrobe,     "\uE945" },  // Lightning bolt
            { AuraMode.Comet,          "\uE706" },  // Globe/comet
            { AuraMode.Flash,          "\uE7E8" },  // Highlight (flash)
            { AuraMode.HEATMAP,        "\uE9D9" },  // Diagnostic (thermal)
            { AuraMode.GPUMODE,        "\uE7F4" },  // Devices (GPU)
            { AuraMode.AMBIENT,        "\uE799" },  // World (ambient)
            { AuraMode.BATTERY,        "\uEA93" },  // Battery
            { AuraMode.CONTRAST,       "\uE81E" },  // DockLeft (split/contrast)
        };

        // Modes that don't need a color picker (they generate their own colors)
        private static readonly HashSet<AuraMode> NoColorModes =
        [
            AuraMode.AuraColorCycle,
            AuraMode.AuraRainbow,
            AuraMode.HEATMAP,
            AuraMode.GPUMODE,
            AuraMode.AMBIENT,
            AuraMode.BATTERY,
        ];

        partial void OnBacklightBrightnessChanged(int value)
        {
            BrightnessText = value >= 0 && value < BrightnessLabels.Length
                ? BrightnessLabels[value] : $"{value}";

            if (_ignoreChange) return;

            AppConfig.Set("keyboard_brightness", value);
            Task.Run(() =>
            {
                try { Aura.ApplyBrightness(value, "Keyboard"); }
                catch (Exception ex) { Logger.WriteLine("Keyboard brightness error: " + ex.Message); }
            });
        }

        public void CycleAuraMode(int delta = 1)
        {
            if (_auraModeValues.Length == 0) return;
            int count = _auraModeValues.Length;
            int newIndex = (SelectedAuraModeIndex + delta + count) % count;
            SelectedAuraModeIndex = newIndex;
        }

        partial void OnSelectedAuraModeIndexChanged(int value)
        {
            if (_ignoreChange) return;
            if (value < 0 || value >= _auraModeValues.Length) return;

            var mode = _auraModeValues[value];
            AppConfig.Set("aura_mode", (int)mode);
            Aura.Mode = mode;

            ShowColorPicker = !NoColorModes.Contains(mode);
            ShowSecondColor = Aura.HasSecondColor();

            ApplyAura();
        }

        partial void OnAnimationSpeedChanged(int value)
        {
            SpeedText = value >= 0 && value < SpeedLabels.Length
                ? SpeedLabels[value] : $"{value}";

            if (_ignoreChange) return;
            AppConfig.Set("aura_speed", value);
            Aura.Speed = (AuraSpeed)value;
            ApplyAura();
        }

        public void OnColor1Changed()
        {
            if (_ignoreChange) return;
            var c = AuraColor1;
            Aura.Color1 = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
            AppConfig.Set("aura_color", Aura.Color1.ToArgb());
            ApplyAura();
        }

        public void OnColor2Changed()
        {
            if (_ignoreChange) return;
            var c = AuraColor2;
            Aura.Color2 = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
            AppConfig.Set("aura_color2", Aura.Color2.ToArgb());
            ApplyAura();
        }

        private void ApplyAura()
        {
            Task.Run(() =>
            {
                try { Aura.ApplyAura(); }
                catch (Exception ex) { Logger.WriteLine("Aura apply error: " + ex.Message); }
            });
        }

        partial void OnSlashBrightnessChanged(int value)
        {
            SlashBrightnessText = value >= 0 && value < BrightnessLabels.Length
                ? BrightnessLabels[value] : $"{value}";

            if (_ignoreChange) return;
            AppConfig.Set("matrix_brightness", value);
            ApplySlash();
        }

        partial void OnSelectedSlashModeChanged(int value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("matrix_running", value);
            ApplySlash();
        }

        partial void OnSlashBatterySaverChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("matrix_auto", value ? 1 : 0);
            Task.Run(() =>
            {
                try { Program.settingsForm?.matrixControl?.SetBatteryAuto(); }
                catch (Exception ex) { Logger.WriteLine("Slash battery saver error: " + ex.Message); }
            });
        }

        partial void OnSlashSleepActiveChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("slash_sleep", value ? 1 : 0);
            ApplySlash();
        }

        partial void OnSlashLidAnimationChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("matrix_lid", value ? 0 : 1);
            Task.Run(() =>
            {
                try { Program.settingsForm?.matrixControl?.SetLidMode(true); }
                catch (Exception ex) { Logger.WriteLine("Slash lid error: " + ex.Message); }
            });
        }

        private void ApplySlash()
        {
            Task.Run(() =>
            {
                try { Program.settingsForm?.matrixControl?.SetDevice(); }
                catch (Exception ex) { Logger.WriteLine("Slash apply error: " + ex.Message); }
            });
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                // Brightness
                int brightness = AppConfig.Get("keyboard_brightness", 1);
                if (brightness < 0 || brightness > 3) brightness = 1;
                BacklightBrightness = brightness;

                // Apply brightness to hardware so backlight flag is set
                // (needed for software-driven modes like Heatmap to work)
                Aura.ApplyBrightness(brightness, "Init");

                // Aura modes (device-dependent)
                var modes = Aura.GetModes();
                _auraModeValues = modes.Keys.ToArray();
                AuraModeLabels = modes.Values.ToArray();
                AuraModeIcons = _auraModeValues
                    .Select(m => ModeIcons.TryGetValue(m, out var icon) ? icon : "")
                    .ToArray();

                // Current mode
                int savedMode = AppConfig.Get("aura_mode");
                Aura.Mode = (AuraMode)savedMode;
                int modeIdx = Array.IndexOf(_auraModeValues, Aura.Mode);
                SelectedAuraModeIndex = modeIdx >= 0 ? modeIdx : 0;

                ShowColorPicker = !NoColorModes.Contains(Aura.Mode);
                ShowSecondColor = Aura.HasSecondColor();

                // Colors
                int colorArgb = AppConfig.Get("aura_color");
                if (colorArgb != -9999 && colorArgb != 0)
                {
                    var dc = System.Drawing.Color.FromArgb(colorArgb);
                    AuraColor1 = Color.FromRgb(dc.R, dc.G, dc.B);
                }

                int color2Argb = AppConfig.Get("aura_color2");
                if (color2Argb != -9999 && color2Argb != 0)
                {
                    var dc = System.Drawing.Color.FromArgb(color2Argb);
                    AuraColor2 = Color.FromRgb(dc.R, dc.G, dc.B);
                }

                // Speed
                int speed = AppConfig.Get("aura_speed");
                if (speed < 0 || speed > 2) speed = 1;
                AnimationSpeed = speed;
                Aura.Speed = (AuraSpeed)speed;

                // Slash lighting
                HasSlash = AppConfig.IsSlash();
                if (HasSlash)
                {
                    var modeNames = SlashDevice.Modes.Values.ToArray();
                    SlashModeLabels = modeNames;

                    int slashBr = AppConfig.Get("matrix_brightness", 0);
                    if (slashBr < 0 || slashBr > 3) slashBr = 0;
                    SlashBrightness = slashBr;

                    int slashRun = AppConfig.Get("matrix_running", 0);
                    if (slashRun < 0 || slashRun >= modeNames.Length) slashRun = 0;
                    SelectedSlashMode = slashRun;

                    SlashBatterySaver = AppConfig.Is("matrix_auto");
                    SlashSleepActive = AppConfig.IsNotFalse("slash_sleep");
                    SlashLidAnimation = !AppConfig.Is("matrix_lid");
                }
            }
            finally
            {
                _ignoreChange = false;
            }
        }
    }
}
