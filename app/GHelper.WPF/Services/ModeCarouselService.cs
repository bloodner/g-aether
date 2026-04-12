using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GHelper.WPF.Services
{
    public class ModeCarouselItem
    {
        public required string Label { get; init; }
        public required string Icon { get; init; }
        public Color AccentColor { get; init; }
    }

    /// <summary>
    /// Filmstrip-style OSD for cycling through modes (performance, GPU, lighting, etc.).
    /// Shows all available options as tiles with the active one highlighted.
    /// </summary>
    public static class ModeCarouselService
    {
        // ── State ──────────────────────────────────────────────
        private static Window? _window;
        private static Border? _windowBorder;
        private static TextBlock? _categoryLabel;
        private static StackPanel? _tilesPanel;
        private static StackPanel? _dotsPanel;
        private static DispatcherTimer? _dismissTimer;

        private static List<ModeCarouselItem>? _items;
        private static int _selectedIndex;
        private static string? _currentCategory;

        // Track tile borders and icon/label blocks for highlight updates
        private static readonly List<Border> _tileBorders = new();
        private static readonly List<Border> _iconCircles = new();
        private static readonly List<TextBlock> _iconBlocks = new();
        private static readonly List<TextBlock> _labelBlocks = new();
        private static readonly List<Ellipse> _dots = new();

        // Layout constants
        private const double TileWidth = 100;
        private const double TileHeight = 92;
        private const double TileGap = 8;
        private const double MaxVisibleTiles = 7;
        private const double DismissSeconds = 2.5;

        public static bool IsVisible => _window?.IsVisible == true;

        // ── Public API ─────────────────────────────────────────

        /// <summary>
        /// Show the carousel with the given items and selected index.
        /// If already visible with the same category, just updates the selection.
        /// </summary>
        public static void Show(string category, List<ModeCarouselItem> items, int selectedIndex)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                bool categoryChanged = _currentCategory != category;
                _items = items;
                _selectedIndex = Math.Clamp(selectedIndex, 0, items.Count - 1);
                _currentCategory = category;

                if (_window == null)
                    CreateWindow();

                if (categoryChanged || !_window!.IsVisible)
                {
                    RebuildTiles();
                    PositionWindow();
                }

                UpdateHighlights();

                if (!_window!.IsVisible)
                {
                    _window.Opacity = 0;
                    _window.Show();

                    var scale = (ScaleTransform)_window.RenderTransform;
                    Animate(scale, ScaleTransform.ScaleXProperty, 0.92, 1.0, 200);
                    Animate(scale, ScaleTransform.ScaleYProperty, 0.92, 1.0, 200);
                    Animate(_window, UIElement.OpacityProperty, 0, 1, 160);
                }

                ResetDismissTimer();
            });
        }

        /// <summary>
        /// Dismiss the carousel immediately with a fade-out animation.
        /// </summary>
        public static void Dismiss()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_window == null || !_window.IsVisible) return;

                _dismissTimer?.Stop();

                var scale = (ScaleTransform)_window.RenderTransform;
                Animate(scale, ScaleTransform.ScaleXProperty, 1.0, 0.94, 200);
                Animate(scale, ScaleTransform.ScaleYProperty, 1.0, 0.94, 200);
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fade.Completed += (s, e) => _window?.Hide();
                _window.BeginAnimation(UIElement.OpacityProperty, fade);
            });
        }

        // ── Window creation ────────────────────────────────────

        private static void CreateWindow()
        {
            _window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                RenderTransform = new ScaleTransform(1, 1),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            _windowBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(210, 16, 16, 26)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 16, 20, 14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 30,
                    ShadowDepth = 6,
                    Opacity = 0.8
                }
            };

            var outerStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Category label
            _categoryLabel = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Text = ""
            };

            // Tiles row
            _tilesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Dot indicators
            _dotsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            outerStack.Children.Add(_categoryLabel);
            outerStack.Children.Add(_tilesPanel);
            outerStack.Children.Add(_dotsPanel);
            _windowBorder.Child = outerStack;
            _window.Content = _windowBorder;

            // Dismiss timer
            _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DismissSeconds) };
            _dismissTimer.Tick += (s, e) =>
            {
                _dismissTimer.Stop();
                Dismiss();
            };
        }

        // ── Tile layout ────────────────────────────────────────

        private static void RebuildTiles()
        {
            if (_tilesPanel == null || _dotsPanel == null || _categoryLabel == null || _items == null) return;

            _tilesPanel.Children.Clear();
            _dotsPanel.Children.Clear();
            _tileBorders.Clear();
            _iconCircles.Clear();
            _iconBlocks.Clear();
            _labelBlocks.Clear();
            _dots.Clear();

            _categoryLabel.Text = _currentCategory?.ToUpperInvariant() ?? "";

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];

                // Icon inside a circular background
                var iconBlock = new TextBlock
                {
                    Text = item.Icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(item.AccentColor)
                };

                var iconCircle = new Border
                {
                    Width = 44,
                    Height = 44,
                    CornerRadius = new CornerRadius(22),
                    Background = new SolidColorBrush(Color.FromArgb(25, item.AccentColor.R, item.AccentColor.G, item.AccentColor.B)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = iconBlock
                };

                var label = new TextBlock
                {
                    Text = item.Label,
                    FontFamily = new FontFamily("Segoe UI Variable"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = TileWidth - 8,
                    Margin = new Thickness(0, 6, 0, 0)
                };

                var tileStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tileStack.Children.Add(iconCircle);
                tileStack.Children.Add(label);

                var tileBorder = new Border
                {
                    Width = TileWidth,
                    Height = TileHeight,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(4),
                    Margin = new Thickness(i > 0 ? TileGap : 0, 0, 0, 0),
                    Child = tileStack
                };

                _tilesPanel.Children.Add(tileBorder);
                _tileBorders.Add(tileBorder);
                _iconCircles.Add(iconCircle);
                _iconBlocks.Add(iconBlock);
                _labelBlocks.Add(label);

                // Dot indicator
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(80, 80, 90)),
                    Margin = new Thickness(i > 0 ? 4 : 0, 0, 0, 0)
                };
                _dotsPanel.Children.Add(dot);
                _dots.Add(dot);
            }
        }

        // ── Highlight updates ──────────────────────────────────

        private static void UpdateHighlights()
        {
            if (_items == null) return;

            for (int i = 0; i < _items.Count; i++)
            {
                bool active = i == _selectedIndex;
                var item = _items[i];
                var accent = item.AccentColor;

                // Tile border
                if (i < _tileBorders.Count)
                {
                    _tileBorders[i].BorderBrush = new SolidColorBrush(
                        active ? Color.FromArgb(180, accent.R, accent.G, accent.B)
                               : Color.FromArgb(20, 255, 255, 255));
                    _tileBorders[i].Background = new SolidColorBrush(
                        active ? Color.FromArgb(30, accent.R, accent.G, accent.B)
                               : Color.FromArgb(0, 255, 255, 255));
                }

                // Icon circle glow
                if (i < _iconCircles.Count)
                {
                    _iconCircles[i].Background = new SolidColorBrush(
                        active ? Color.FromArgb(50, accent.R, accent.G, accent.B)
                               : Color.FromArgb(15, accent.R, accent.G, accent.B));
                }

                // Icon opacity
                if (i < _iconBlocks.Count)
                {
                    _iconBlocks[i].Opacity = active ? 1.0 : 0.45;
                }

                // Label opacity
                if (i < _labelBlocks.Count)
                {
                    _labelBlocks[i].Opacity = active ? 1.0 : 0.5;
                    _labelBlocks[i].FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }

                // Dot indicator
                if (i < _dots.Count)
                {
                    _dots[i].Fill = new SolidColorBrush(
                        active ? Color.FromArgb(220, accent.R, accent.G, accent.B)
                               : Color.FromRgb(80, 80, 90));
                    _dots[i].Width = active ? 8 : 6;
                    _dots[i].Height = active ? 8 : 6;
                }
            }

            // Accent-tint the window border to match selected item
            if (_selectedIndex < _items.Count && _windowBorder != null)
            {
                var c = _items[_selectedIndex].AccentColor;
                _windowBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
            }
        }

        // ── Positioning ────────────────────────────────────────

        private static void PositionWindow()
        {
            if (_window == null) return;

            // Force layout so ActualWidth/Height are computed
            _window.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _window.Arrange(new Rect(_window.DesiredSize));

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen != null)
            {
                var source = PresentationSource.FromVisual(Application.Current?.MainWindow ?? _window);
                double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

                double screenW = screen.WorkingArea.Width * dpiX;
                double screenH = screen.WorkingArea.Height * dpiY;
                double screenLeft = screen.WorkingArea.Left * dpiX;
                double screenTop = screen.WorkingArea.Top * dpiY;

                _window.Left = screenLeft + (screenW - _window.DesiredSize.Width) / 2;
                _window.Top = screenTop + (screenH - _window.DesiredSize.Height) / 2;
            }
        }

        // ── Timer ──────────────────────────────────────────────

        private static void ResetDismissTimer()
        {
            _dismissTimer?.Stop();
            _dismissTimer?.Start();
        }

        // ── Animation helper ───────────────────────────────────

        private static void Animate(Animatable target, DependencyProperty prop, double from, double to, int ms)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            target.BeginAnimation(prop, anim);
        }

        // ── Convenience methods for common mode categories ─────

        /// <summary>
        /// Show the performance mode carousel. Reads current modes from Modes class.
        /// </summary>
        public static void ShowPerformanceModes()
        {
            var modeList = GHelper.Mode.Modes.GetList();
            int current = GHelper.Mode.Modes.GetCurrent();
            int selectedIdx = modeList.IndexOf(current);
            if (selectedIdx < 0) selectedIdx = 0;

            var items = new List<ModeCarouselItem>();
            foreach (var mode in modeList)
            {
                int baseMode = GHelper.Mode.Modes.GetBase(mode);
                var (icon, color) = baseMode switch
                {
                    AsusACPI.PerformanceSilent   => ("\uE8BE", Color.FromRgb(0xA7, 0x8B, 0xFA)),
                    AsusACPI.PerformanceBalanced  => ("\uE9E9", Color.FromRgb(0x60, 0xCD, 0xFF)),
                    AsusACPI.PerformanceTurbo     => ("\uE945", Color.FromRgb(0xFF, 0x6B, 0x35)),
                    _ => ("\uE9E9", Color.FromRgb(0x60, 0xCD, 0xFF)),
                };

                items.Add(new ModeCarouselItem
                {
                    Label = GHelper.Mode.Modes.GetName(mode),
                    Icon = icon,
                    AccentColor = color
                });
            }

            Show("Performance", items, selectedIdx);
        }

        /// <summary>
        /// Show the GPU mode carousel with the given mode highlighted.
        /// </summary>
        public static void ShowGpuModes(int activeGpuMode)
        {
            bool isAuto = AppConfig.Is("gpu_auto");

            var items = new List<ModeCarouselItem>
            {
                new() { Label = "Eco",       Icon = "\uE8BE", AccentColor = Color.FromRgb(0x4C, 0xC9, 0x5E) },
                new() { Label = "Standard",  Icon = "\uE9E9", AccentColor = Color.FromRgb(0x60, 0xCD, 0xFF) },
                new() { Label = "Optimized", Icon = "\uEA8A", AccentColor = Color.FromRgb(0x60, 0xCD, 0xFF) },
            };

            // Determine selected index
            int selectedIdx = activeGpuMode switch
            {
                AsusACPI.GPUModeEco => 0,
                AsusACPI.GPUModeStandard when isAuto => 2,
                AsusACPI.GPUModeStandard => 1,
                AsusACPI.GPUModeUltimate => items.Count - 1, // would need Ultimate tile if available
                _ => 1
            };

            Show("GPU Mode", items, selectedIdx);
        }

        /// <summary>
        /// Show the aura/lighting mode carousel.
        /// </summary>
        public static void ShowAuraModes(string[] labels, string[] icons, int selectedIndex)
        {
            var items = new List<ModeCarouselItem>();
            var accent = Color.FromRgb(0x60, 0xCD, 0xFF); // default accent blue

            for (int i = 0; i < labels.Length; i++)
            {
                items.Add(new ModeCarouselItem
                {
                    Label = labels[i],
                    Icon = i < icons.Length ? icons[i] : "\uE781",
                    AccentColor = accent
                });
            }

            Show("Lighting", items, selectedIndex);
        }
    }
}
