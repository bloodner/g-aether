using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GHelper.WPF.Services
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public static class ToastService
    {
        private static readonly SolidColorBrush InfoBrush;
        private static readonly SolidColorBrush SuccessBrush;
        private static readonly SolidColorBrush WarningBrush;
        private static readonly SolidColorBrush ErrorBrush;
        private static readonly SolidColorBrush InfoBorderBrush;
        private static readonly SolidColorBrush SuccessBorderBrush;
        private static readonly SolidColorBrush WarningBorderBrush;
        private static readonly SolidColorBrush ErrorBorderBrush;

        static ToastService()
        {
            InfoBrush = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
            InfoBrush.Freeze();
            SuccessBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xC9, 0x5E));
            SuccessBrush.Freeze();
            WarningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47));
            WarningBrush.Freeze();
            ErrorBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
            ErrorBrush.Freeze();

            InfoBorderBrush = new SolidColorBrush(Color.FromArgb(60, 0x60, 0xCD, 0xFF));
            InfoBorderBrush.Freeze();
            SuccessBorderBrush = new SolidColorBrush(Color.FromArgb(60, 0x4C, 0xC9, 0x5E));
            SuccessBorderBrush.Freeze();
            WarningBorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xB3, 0x47));
            WarningBorderBrush.Freeze();
            ErrorBorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0x44, 0x44));
            ErrorBorderBrush.Freeze();
        }

        // --- In-window toast (overlay inside MainWindow) ---
        private static Border? _toastBorder;
        private static TextBlock? _iconBlock;
        private static TextBlock? _messageBlock;
        private static DispatcherTimer? _hideTimer;

        // --- OSD toast (standalone topmost window for hotkey feedback) ---
        private static Window? _osdWindow;
        private static TextBlock? _osdIcon;
        private static TextBlock? _osdMessage;
        private static DispatcherTimer? _osdHideTimer;

        public static void Initialize(Grid parentGrid)
        {
            // --- In-window overlay ---
            _toastBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 30)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.5
                },
                RenderTransform = new TranslateTransform(0, -20),
                RenderTransformOrigin = new Point(0.5, 0)
            };

            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            _iconBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            _messageBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                VerticalAlignment = VerticalAlignment.Center
            };

            stack.Children.Add(_iconBlock);
            stack.Children.Add(_messageBlock);
            _toastBorder.Child = stack;

            Grid.SetRow(_toastBorder, 1);
            Grid.SetColumn(_toastBorder, 1);
            System.Windows.Controls.Panel.SetZIndex(_toastBorder, 100);
            parentGrid.Children.Add(_toastBorder);

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                HideInWindowToast();
            };

            // --- OSD window (created lazily on first ShowOsd call) ---
            _osdHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _osdHideTimer.Tick += (s, e) =>
            {
                _osdHideTimer!.Stop();
                HideOsd();
            };
        }

        /// <summary>
        /// Show toast inside the main window (for in-app interactions).
        /// </summary>
        public static void Show(string message, ToastType type = ToastType.Info)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // If main window is visible, show in-window toast
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow.IsVisible && _toastBorder != null)
                {
                    ShowInWindow(message, type);
                }
                // Always show OSD for visibility even when window is hidden
                ShowOsd(message, type);
            });
        }

        private static void ShowInWindow(string message, ToastType type)
        {
            if (_toastBorder == null || _iconBlock == null || _messageBlock == null) return;

            _messageBlock.Text = message;

            var (icon, foreground, border) = GetTypeVisuals(type);
            _iconBlock.Text = icon;
            _iconBlock.Foreground = foreground;
            _toastBorder.BorderBrush = border;

            _toastBorder.Visibility = Visibility.Visible;

            var transform = (TranslateTransform)_toastBorder.RenderTransform;
            var slideIn = new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            _toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _hideTimer?.Stop();
            _hideTimer?.Start();
        }

        private static void HideInWindowToast()
        {
            if (_toastBorder == null) return;

            var transform = (TranslateTransform)_toastBorder.RenderTransform;
            var slideOut = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) => _toastBorder.Visibility = Visibility.Collapsed;

            transform.BeginAnimation(TranslateTransform.YProperty, slideOut);
            _toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        /// <summary>
        /// Show a standalone OSD window on top of everything (for hotkey feedback).
        /// </summary>
        private static void ShowOsd(string message, ToastType type)
        {
            if (_osdWindow == null)
                CreateOsdWindow();

            if (_osdIcon == null || _osdMessage == null || _osdWindow == null) return;

            _osdMessage.Text = message;

            var (icon, foreground, border) = GetTypeVisuals(type);
            _osdIcon.Text = icon;
            _osdIcon.Foreground = foreground;
            _osdMessage.Foreground = foreground;

            PositionOsd();

            _osdWindow.Opacity = 0;
            _osdWindow.Show();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            _osdWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _osdHideTimer?.Stop();
            _osdHideTimer?.Start();
        }

        private static void HideOsd()
        {
            if (_osdWindow == null) return;

            var scaleTransform = (ScaleTransform)_osdWindow.RenderTransform;
            var scaleX = new DoubleAnimation(1.0, 0.92, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleY = new DoubleAnimation(1.0, 0.92, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            fadeOut.Completed += (s, e) => _osdWindow?.Hide();

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            _osdWindow.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private static Border? _osdBorder;
        private static Border? _osdIconCircle;

        private static void CreateOsdWindow()
        {
            _osdWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Width = 150,
                Height = 140,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.Manual,
                RenderTransform = new ScaleTransform(1, 1),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            _osdBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 16, 16, 26)),
                CornerRadius = new CornerRadius(20),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 20, 20, 16),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 30,
                    ShadowDepth = 6,
                    Opacity = 0.8
                }
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon inside a glowing circle
            _osdIconCircle = new Border
            {
                Width = 64,
                Height = 64,
                CornerRadius = new CornerRadius(32),
                Background = new SolidColorBrush(Color.FromArgb(30, 96, 205, 255)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
            };

            _osdIcon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 28,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _osdIconCircle.Child = _osdIcon;

            _osdMessage = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            stack.Children.Add(_osdIconCircle);
            stack.Children.Add(_osdMessage);
            _osdBorder.Child = stack;
            _osdWindow.Content = _osdBorder;
        }

        /// <summary>
        /// Show a toast with a custom icon and color (for mode-specific visuals).
        /// </summary>
        public static void Show(string message, string icon, Color accentColor)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var brush = new SolidColorBrush(accentColor);
                brush.Freeze();
                var borderBrush = new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B));
                borderBrush.Freeze();

                // In-window toast
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow.IsVisible && _toastBorder != null &&
                    _iconBlock != null && _messageBlock != null)
                {
                    _messageBlock.Text = message;
                    _iconBlock.Text = icon;
                    _iconBlock.Foreground = brush;
                    _toastBorder.BorderBrush = borderBrush;

                    _toastBorder.Visibility = Visibility.Visible;
                    var transform = (TranslateTransform)_toastBorder.RenderTransform;
                    transform.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(-20, 0, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                    _toastBorder.BeginAnimation(UIElement.OpacityProperty,
                        new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                    _hideTimer?.Stop();
                    _hideTimer?.Start();
                }

                // OSD toast
                ShowOsdCustom(message, icon, brush);
            });
        }

        private static void ShowOsdCustom(string message, string icon, SolidColorBrush foreground)
        {
            if (_osdWindow == null)
                CreateOsdWindow();
            if (_osdIcon == null || _osdMessage == null || _osdWindow == null) return;

            _osdMessage.Text = message;
            _osdMessage.Foreground = foreground;
            _osdIcon.Text = icon;
            _osdIcon.Foreground = foreground;

            // Accent glow circle behind icon
            if (_osdIconCircle != null)
            {
                var c = ((SolidColorBrush)foreground).Color;
                _osdIconCircle.Background = new SolidColorBrush(Color.FromArgb(35, c.R, c.G, c.B));
            }

            // Accent-tinted border
            if (_osdBorder != null)
            {
                var c = ((SolidColorBrush)foreground).Color;
                _osdBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B));
            }

            PositionOsd();

            // Scale + fade animation (pop in from 90%)
            _osdWindow.Opacity = 0;
            _osdWindow.Show();

            var scaleTransform = (ScaleTransform)_osdWindow.RenderTransform;
            var scaleX = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleY = new DoubleAnimation(0.9, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            _osdWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            _osdHideTimer?.Stop();
            _osdHideTimer?.Start();
        }

        /// <summary>
        /// Show a performance mode toast with mode-specific icon and color.
        /// </summary>
        public static void ShowPerformanceMode(int mode, string modeName)
        {
            // ACPI base: 0=Balanced, 1=Turbo, 2=Silent
            // Colors synced with PerformanceModeArc and TrayIconService
            var (icon, color) = mode switch
            {
                0 => ("\uE9E9", Color.FromRgb(0x60, 0xCD, 0xFF)),   // Speed gauge, Balanced blue
                1 => ("\uE945", Color.FromRgb(0xFF, 0x6B, 0x35)),   // Lightning bolt, Turbo orange
                2 => ("\uE8BE", Color.FromRgb(0xA7, 0x8B, 0xFA)),   // Leaf, Silent purple
                _ => ("\uE945", Color.FromRgb(0x60, 0xCD, 0xFF)),   // Bolt fallback, accent blue
            };

            ShowOsdOnly(modeName, icon, color);
        }

        /// <summary>
        /// Show only the OSD window (no in-window toast). Used for hotkey-triggered changes.
        /// </summary>
        public static void ShowOsdOnly(string message, string icon, Color accentColor)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var brush = new SolidColorBrush(accentColor);
                brush.Freeze();
                ShowOsdCustom(message, icon, brush);
            });
        }

        private static void PositionOsd()
        {
            if (_osdWindow == null) return;

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen != null)
            {
                var source = PresentationSource.FromVisual(Application.Current?.MainWindow ?? _osdWindow);
                double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

                double screenW = screen.WorkingArea.Width * dpiX;
                double screenH = screen.WorkingArea.Height * dpiY;
                double screenLeft = screen.WorkingArea.Left * dpiX;
                double screenTop = screen.WorkingArea.Top * dpiY;

                _osdWindow.Left = screenLeft + (screenW - _osdWindow.Width) / 2;
                _osdWindow.Top = screenTop + (screenH - _osdWindow.Height) / 2;
            }
        }

        private static (string icon, SolidColorBrush foreground, SolidColorBrush border) GetTypeVisuals(ToastType type)
        {
            return type switch
            {
                ToastType.Success => ("\uE73E", SuccessBrush, SuccessBorderBrush),
                ToastType.Warning => ("\uE7BA", WarningBrush, WarningBorderBrush),
                ToastType.Error   => ("\uEA39", ErrorBrush, ErrorBorderBrush),
                _                 => ("\uE946", InfoBrush, InfoBorderBrush),
            };
        }
    }
}
