using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GHelper.WPF.Views
{
    public partial class TrayMenuWindow : Window
    {
        public event Action<int>? PerfSelected;
        public event Action<int, bool>? GpuSelected;
        public event Action? QuitSelected;

        // Pulled from ThemeService so every channel (tray, toast, status bar) shares
        // the same palette. Balanced/Standard follow the live Windows accent.
        private static Color SilentColor    => GHelper.WPF.Services.ThemeService.ColorSilent;
        private static Color BalancedColor  => GHelper.WPF.Services.ThemeService.ColorBalanced;
        private static Color TurboColor     => GHelper.WPF.Services.ThemeService.ColorTurbo;
        private static Color EcoColor       => GHelper.WPF.Services.ThemeService.ColorEco;
        private static Color StandardColor  => GHelper.WPF.Services.ThemeService.ColorStandard;
        private static Color OptimizedColor => GHelper.WPF.Services.ThemeService.ColorOptimized;
        private static Color UltimateColor  => GHelper.WPF.Services.ThemeService.ColorUltimate;
        private static readonly Color QuitColor = Color.FromRgb(0xFF, 0x6B, 0x6B);
        private static readonly Color DimText   = Color.FromRgb(0xD6, 0xD6, 0xDE);

        private bool _closing;

        public TrayMenuWindow()
        {
            InitializeComponent();
        }

        public void Rebuild(int perfMode, int gpuMode, bool gpuAuto)
        {
            MenuStack.Children.Clear();

            MenuStack.Children.Add(SectionHeader("Performance"));
            // ACPI: 2=Silent, 0=Balanced, 1=Turbo
            MenuStack.Children.Add(ModeItem("\uE8BE", "Silent",   SilentColor,   perfMode == 2, () => PerfSelected?.Invoke(2)));
            MenuStack.Children.Add(ModeItem("\uE9E9", "Balanced", BalancedColor, perfMode == 0, () => PerfSelected?.Invoke(0)));
            MenuStack.Children.Add(ModeItem("\uE945", "Turbo",    TurboColor,    perfMode == 1, () => PerfSelected?.Invoke(1)));

            MenuStack.Children.Add(Separator());

            MenuStack.Children.Add(SectionHeader("GPU Mode"));
            MenuStack.Children.Add(ModeItem("\uE7E8", "Eco",       EcoColor,
                gpuMode == AsusACPI.GPUModeEco,
                () => GpuSelected?.Invoke(AsusACPI.GPUModeEco, false)));
            MenuStack.Children.Add(ModeItem("\uE7F4", "Standard",  StandardColor,
                gpuMode == AsusACPI.GPUModeStandard && !gpuAuto,
                () => GpuSelected?.Invoke(AsusACPI.GPUModeStandard, false)));
            MenuStack.Children.Add(ModeItem("\uEA8A", "Optimized", OptimizedColor,
                gpuMode == AsusACPI.GPUModeStandard && gpuAuto,
                () => GpuSelected?.Invoke(AsusACPI.GPUModeStandard, true)));
            MenuStack.Children.Add(ModeItem("\uE945", "Ultimate", UltimateColor,
                gpuMode == AsusACPI.GPUModeUltimate,
                () => GpuSelected?.Invoke(AsusACPI.GPUModeUltimate, false)));

            MenuStack.Children.Add(Separator());

            MenuStack.Children.Add(ModeItem("\uE7E8", "Quit", QuitColor, false, () => QuitSelected?.Invoke()));
        }

        /// <summary>
        /// Show at screen position (physical pixels, e.g. Cursor.Position), positioning
        /// so the bottom-right of the menu sits just inside the cursor — tray-menu convention.
        /// </summary>
        public void OpenAt(System.Drawing.Point cursorPhysicalPx)
        {
            _closing = false;
            // Keep offscreen until sized
            Left = -10000;
            Top = -10000;
            Opacity = 0;
            Show();

            // After layout, position and animate in
            Dispatcher.BeginInvoke(new Action(() =>
            {
                PositionAt(cursorPhysicalPx);
                PlayOpenAnimation();
                Activate();
                Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void PositionAt(System.Drawing.Point cursorPhysicalPx)
        {
            var src = PresentationSource.FromVisual(this);
            double dipX = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dipY = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            double cx = cursorPhysicalPx.X * dipX;
            double cy = cursorPhysicalPx.Y * dipY;

            double w = ActualWidth;
            double h = ActualHeight;

            // Bottom-right of menu anchored to cursor with slight offset
            double left = cx - w + 8;
            double top = cy - h + 8;

            var screen = System.Windows.Forms.Screen.FromPoint(cursorPhysicalPx);
            double sL = screen.WorkingArea.Left * dipX;
            double sT = screen.WorkingArea.Top * dipY;
            double sR = (screen.WorkingArea.Left + screen.WorkingArea.Width) * dipX;
            double sB = (screen.WorkingArea.Top + screen.WorkingArea.Height) * dipY;

            if (left < sL) left = sL;
            if (top < sT) top = sT;
            if (left + w > sR) left = sR - w;
            if (top + h > sB) top = sB - h;

            Left = left;
            Top = top;
        }

        private void PlayOpenAnimation()
        {
            MenuScale.ScaleX = 0.94;
            MenuScale.ScaleY = 0.94;
            var scaleX = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(160))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var scaleY = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(160))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            MenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            MenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            BeginAnimation(OpacityProperty, fade);
        }

        private void AnimateCloseThenHide()
        {
            if (_closing) return;
            _closing = true;
            var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(110));
            fade.Completed += (s, e) => Hide();
            BeginAnimation(OpacityProperty, fade);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            AnimateCloseThenHide();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) AnimateCloseThenHide();
        }

        // ── Content builders ─────────────────────────────────────────────

        private static TextBlock SectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text.ToUpperInvariant(),
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x7C, 0x86)),
                Margin = new Thickness(10, 8, 10, 4),
            };
        }

        private Border ModeItem(string icon, string label, Color accent, bool isSelected, Action onClick)
        {
            var border = new Border
            {
                Style = (Style)Resources["MenuItemStyle"],
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(0x22, accent.R, accent.G, accent.B))
                    : Brushes.Transparent,
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left accent bar for selected items
            if (isSelected)
            {
                var accentBar = new Border
                {
                    Background = new SolidColorBrush(accent),
                    CornerRadius = new CornerRadius(2),
                    Width = 3,
                    Height = 16,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(accentBar, 0);
                grid.Children.Add(accentBar);
            }

            var iconBlock = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = new SolidColorBrush(isSelected ? accent : DimText),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(iconBlock, 2);
            grid.Children.Add(iconBlock);

            var labelBlock = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 13,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(isSelected ? accent : DimText),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(labelBlock, 4);
            grid.Children.Add(labelBlock);

            if (isSelected)
            {
                // Right-side filled dot
                var dot = new Border
                {
                    Background = new SolidColorBrush(accent),
                    CornerRadius = new CornerRadius(4),
                    Width = 7,
                    Height = 7,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(dot, 5);
                grid.Children.Add(dot);
            }

            border.Child = grid;

            // Hover effect
            border.MouseEnter += (s, e) =>
            {
                if (!isSelected)
                    border.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
            };
            border.MouseLeave += (s, e) =>
            {
                if (!isSelected) border.Background = Brushes.Transparent;
            };
            border.MouseLeftButtonUp += (s, e) =>
            {
                onClick();
                AnimateCloseThenHide();
            };

            return border;
        }

        private static UIElement Separator()
        {
            return new Border
            {
                Height = 1,
                Margin = new Thickness(10, 6, 10, 6),
                Background = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
            };
        }
    }
}
