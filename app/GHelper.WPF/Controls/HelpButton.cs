using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GHelper.WPF.Services;

namespace GHelper.WPF.Controls
{
    public class HelpButton : FrameworkElement
    {
        public static readonly DependencyProperty HelpKeyProperty =
            DependencyProperty.Register(nameof(HelpKey), typeof(string), typeof(HelpButton),
                new PropertyMetadata(null));

        public string HelpKey
        {
            get => (string)GetValue(HelpKeyProperty);
            set => SetValue(HelpKeyProperty, value);
        }

        private Popup? _popup;
        private bool _isOpen;
        private bool _hoveringPopup;

        public HelpButton()
        {
            Width = 18;
            Height = 18;
            Cursor = Cursors.Help;
            Focusable = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double size = Math.Min(ActualWidth, ActualHeight);
            double cx = ActualWidth / 2;
            double cy = ActualHeight / 2;
            double r = size / 2;

            var bgColor = _isOpen
                ? Color.FromArgb(60, 0x60, 0xCD, 0xFF)
                : IsMouseOver
                    ? Color.FromArgb(40, 255, 255, 255)
                    : Color.FromArgb(20, 255, 255, 255);
            dc.DrawEllipse(new SolidColorBrush(bgColor), null, new Point(cx, cy), r, r);

            var textColor = _isOpen
                ? Color.FromRgb(0x60, 0xCD, 0xFF)
                : Color.FromRgb(0x90, 0x90, 0x9A);
            var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = new FormattedText("?", System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, typeface, 10, new SolidColorBrush(textColor), dpi);
            dc.DrawText(ft, new Point(cx - ft.Width / 2, cy - ft.Height / 2));
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            OpenPopup();
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            // Delay close slightly so user can move to the popup
            if (!_hoveringPopup)
                ClosePopup();
            InvalidateVisual();
        }

        private void OpenPopup()
        {
            var entry = HelpContent.Get(HelpKey);
            if (entry == null) return;

            var title = new TextBlock
            {
                Text = entry.Title,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            };

            var body = new TextBlock
            {
                Text = entry.Description,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB8)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
            };

            var stack = new StackPanel();
            stack.Children.Add(title);
            stack.Children.Add(body);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 20, 20, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0x60, 0xCD, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                MaxWidth = 280,
                Child = stack,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 20, ShadowDepth = 4, Opacity = 0.6
                }
            };

            // Keep popup open while hovering over it
            border.MouseEnter += (s, e) => _hoveringPopup = true;
            border.MouseLeave += (s, e) =>
            {
                _hoveringPopup = false;
                if (!IsMouseOver) ClosePopup();
            };

            _popup = new Popup
            {
                Child = border,
                PlacementTarget = this,
                Placement = PlacementMode.Left,
                HorizontalOffset = -8,
                VerticalOffset = -4,
                StaysOpen = true,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
            };

            _popup.IsOpen = true;
            _isOpen = true;
            InvalidateVisual();
        }

        private void ClosePopup()
        {
            if (_popup != null)
            {
                _popup.IsOpen = false;
                _isOpen = false;
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(18, 18);
        }
    }
}
