using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace GHelper.WPF.Views
{
    /// <summary>
    /// Compact always-on-top window showing live telemetry. Draws over any
    /// borderless-windowed app (most modern games). All visual settings
    /// (size, transparency, accent color, logo visibility, close-button
    /// visibility, disappear-on-hover) are config-driven and applied via
    /// ApplySettings() so changes are live.
    /// </summary>
    public partial class GadgetWindow : Window
    {
        public GadgetWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            LocationChanged += OnLocationChanged;
        }

        // Tile keys, parallel to the order of XAML children in TileGrid.
        private static readonly string[] TileKeys =
        {
            "cpu_temp", "dgpu_temp", "cpu_use", "dgpu_use",
            "power", "battery", "cpu_fan", "gpu_fan",
        };

        // Cached references to all tile borders (in original XAML order). We
        // hand these in/out of TileGrid.Children when reflowing for visibility.
        private List<Border>? _allTiles;

        // ---- Size presets -----------------------------------------------------
        // Tuple: (window W, H @ 4 rows, per-row H, value font, label font, header font)
        // PerRowHeight is what we add/remove per visible row pair when tiles are hidden.
        private static readonly (double W, double H4, double PerRow, double Value, double Label, double Header) XSmallSize =
            (180, 220, 42, 12, 8, 9);
        private static readonly (double W, double H4, double PerRow, double Value, double Label, double Header) SmallSize =
            (220, 260, 52, 14, 9, 10);
        private static readonly (double W, double H4, double PerRow, double Value, double Label, double Header) MediumSize =
            (260, 300, 62, 18, 10, 11);
        private static readonly (double W, double H4, double PerRow, double Value, double Label, double Header) LargeSize =
            (320, 360, 77, 22, 11, 12);

        // ---- Accent color presets --------------------------------------------
        private static Color AccentFor(string key) => key switch
        {
            "purple" => Color.FromRgb(0xC0, 0x84, 0xFC),
            "green"  => Color.FromRgb(0x6B, 0xCB, 0x77),
            "orange" => Color.FromRgb(0xFF, 0xB3, 0x47),
            "red"    => Color.FromRgb(0xFF, 0x6B, 0x6B),
            "white"  => Color.FromRgb(0xF0, 0xF0, 0xF2),  // soft off-white (avoid harsh #FFFFFF)
            "dark"   => Color.FromRgb(0x36, 0x36, 0x3B),  // near-black gray, "stealth" look
            _        => Color.FromRgb(0x60, 0xCD, 0xFF),  // blue (single-color fallback)
        };

        // Per-tile default colors when the user picks the "multi" accent (the
        // ship default). Order matches the XAML tile order:
        // CPU Temp, dGPU Temp, CPU Use, dGPU Use, Power, Battery, CPU Fan, GPU Fan.
        private static readonly Color[] MultiTileColors =
        {
            Color.FromRgb(0xFF, 0x6B, 0x6B),
            Color.FromRgb(0xFF, 0xB3, 0x47),
            Color.FromRgb(0xA7, 0x8B, 0xFA),
            Color.FromRgb(0x60, 0xCD, 0xFF),
            Color.FromRgb(0x6B, 0xCB, 0x77),
            Color.FromRgb(0x4C, 0xC9, 0x5E),
            Color.FromRgb(0xC0, 0x84, 0xFC),
            Color.FromRgb(0xF4, 0x72, 0xB6),
        };

        // ---- Transparency cache for hover restore ----------------------------
        private double _baseOpacity = 1.0;
        private bool _hoverFadeEnabled;

        // Win32 interop for click-through. WS_EX_TRANSPARENT lets mouse events
        // pass through this window to whatever is underneath. Requires WS_EX_LAYERED,
        // which AllowsTransparency=True already gives us.
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public void ApplySettings()
        {
            // Close button
            CloseButton.Visibility = AppConfig.Is("gadget_hide_close")
                ? Visibility.Collapsed : Visibility.Visible;

            // Logo / title
            TitleText.Visibility = AppConfig.Is("gadget_hide_logo")
                ? Visibility.Collapsed : Visibility.Visible;

            // Size preset
            string size = AppConfig.GetString("gadget_size") ?? "medium";
            var s = size switch
            {
                "xsmall" => XSmallSize,
                "small"  => SmallSize,
                "large"  => LargeSize,
                _        => MediumSize,
            };

            // Mode strip — single source of truth in gadget_modestrip_style.
            //   "off"     = both containers hidden; header reverts to "G-Aether"
            //   "badges"  = full PERF/GPU/DISPLAY/SERVICES row below the header (Small+)
            //   "compact" = Scene · PerfMode + health dot inline in the header, replaces the title
            string stripStyle = AppConfig.GetString("gadget_modestrip_style") ?? "badges";

            bool showBadges = stripStyle == "badges" && size != "xsmall";
            bool showCompact = stripStyle == "compact";

            ModeStripContainer.Visibility = showBadges ? Visibility.Visible : Visibility.Collapsed;

            // Compact takes over the header: hide the "G-Aether" title text and show
            // the inline scene/mode + dot instead. Hide-logo respects this — when the
            // user has hidden the logo, the compact content still shows.
            CompactStripInline.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;
            CompactHealthDot.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;

            // When compact is active, suppress TitleText regardless of hide_logo
            // (the compact content IS the title in that mode). When compact is off,
            // TitleText follows the existing hide_logo rule.
            if (showCompact)
            {
                TitleText.Visibility = Visibility.Collapsed;
            }
            // (When showCompact is false, TitleText.Visibility was already set
            // earlier in this method by the hide_logo block.)

            Width = s.W;
            ApplyTileFontSizes(s.Value, s.Label, s.Header);

            // Tile visibility + reflow. Hidden tiles are removed from the grid
            // entirely (not just collapsed) so cells don't sit empty.
            int visibleCount = ApplyTileVisibility();
            int rows = Math.Max(1, (visibleCount + 1) / 2);
            TileGrid.Rows = rows;
            // Window height = chrome + rows * PerRow + (mode strip row when visible).
            // The strip row is a fixed ~28px (11pt text + 4+4 padding + 1+1 dividers + bottom margin 8).
            double chrome = s.H4 - 4 * s.PerRow;
            // Only the badges variant adds a row; compact is inline in the header.
            double stripContribution = ModeStripContainer.Visibility == Visibility.Visible ? 28 : 0;
            Height = chrome + rows * s.PerRow + stripContribution;

            // Accent color. "multi" (default) keeps each tile's original color
            // and lets the header use the app-level accent. Anything else
            // overrides every tile value with that single color.
            string accentKey = AppConfig.GetString("gadget_accent") ?? "multi";
            ApplyTileAccent(accentKey);

            // Transparency (60–100)
            int pctRaw = AppConfig.Get("gadget_opacity");
            int pct = pctRaw == 0 ? 100 : Math.Clamp(pctRaw, 20, 100);
            _baseOpacity = pct / 100.0;
            if (!_hoverFadeEnabled || !IsMouseOver) Opacity = _baseOpacity;

            // Mouse interaction — unified successor of gadget_hover_fade + gadget_click_through.
            //   "normal" = gadget catches all clicks
            //   "hover"  = passes clicks through ONLY while hovered (fade is the visual cue)
            //   "always" = WS_EX_TRANSPARENT permanently — to drag, switch back to normal
            string interaction = AppConfig.GetString("gadget_mouse_interaction") ?? "normal";
            _hoverFadeEnabled = interaction == "hover";
            if (!_hoverFadeEnabled) Opacity = _baseOpacity;
            ApplyClickThrough(interaction == "always");
        }

        private void ApplyClickThrough(bool enabled)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;  // not yet initialized; OnLoaded will retry via ApplySettings
            int extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            int updated = enabled
                ? extStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED
                : (extStyle & ~WS_EX_TRANSPARENT) | WS_EX_LAYERED;
            if (updated != extStyle)
                SetWindowLong(hwnd, GWL_EXSTYLE, updated);
        }

        private void ApplyTileFontSizes(double valueSize, double labelSize, double headerSize)
        {
            TitleText.FontSize = headerSize;
            foreach (var border in TileGrid.Children.OfType<Border>())
            {
                if (border.Child is StackPanel sp && sp.Children.Count >= 2)
                {
                    if (sp.Children[0] is TextBlock label) label.FontSize = labelSize;
                    if (sp.Children[1] is TextBlock value) value.FontSize = valueSize;
                }
            }
        }

        private int ApplyTileVisibility()
        {
            // Snapshot tile order on first run so we can re-add them later.
            _allTiles ??= TileGrid.Children.OfType<Border>().ToList();

            TileGrid.Children.Clear();
            int visible = 0;
            for (int i = 0; i < _allTiles.Count && i < TileKeys.Length; i++)
            {
                bool show = AppConfig.Get("gadget_show_" + TileKeys[i], 1) == 1;
                if (show)
                {
                    TileGrid.Children.Add(_allTiles[i]);
                    visible++;
                }
            }
            return visible;
        }

        private void ApplyTileAccent(string accentKey)
        {
            bool isMulti = accentKey == "multi";

            if (isMulti)
            {
                // Restore the app's primary accent on the header.
                if (Application.Current?.TryFindResource("AccentBrush") is Brush appAccent)
                    TitleText.Foreground = appAccent;
            }
            else
            {
                TitleText.Foreground = new SolidColorBrush(AccentFor(accentKey));
            }

            // Iterate the FULL tile list (cached), not just visible ones, so
            // tiles regain their colors if shown again later.
            var tiles = _allTiles ?? TileGrid.Children.OfType<Border>().ToList();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].Child is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock value)
                {
                    Color color = isMulti && i < MultiTileColors.Length
                        ? MultiTileColors[i]
                        : AccentFor(accentKey);
                    value.Foreground = new SolidColorBrush(color);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplySettings();  // size needs to be set before position math

            var wa = SystemParameters.WorkArea;

            int savedX = AppConfig.Get("gadget_x");
            int savedY = AppConfig.Get("gadget_y");

            if (savedX > 0 && savedY > 0 && IsOnScreenDip(savedX, savedY, wa))
            {
                Left = savedX;
                Top = savedY;
            }
            else
            {
                Left = wa.Right - Width - 20;
                Top = wa.Top + 20;
            }
        }

        private static bool IsOnScreenDip(int x, int y, Rect workArea)
        {
            return x >= workArea.Left - 10
                && y >= workArea.Top - 10
                && x <= workArea.Right - 40
                && y <= workArea.Bottom - 40;
        }

        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (IsLoaded)
            {
                AppConfig.Set("gadget_x", (int)Left);
                AppConfig.Set("gadget_y", (int)Top);
            }
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void Root_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_hoverFadeEnabled) Opacity = Math.Max(0.05, _baseOpacity * 0.1);
        }

        private void Root_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoverFadeEnabled) Opacity = _baseOpacity;
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            Services.GadgetService.SetEnabled(false);
        }
    }
}
