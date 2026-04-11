using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GHelper.WPF.Controls
{
    public class ToggleSwitch : FrameworkElement
    {
        private const double TrackWidth = 40;
        private const double TrackHeight = 20;
        private const double ThumbRadius = 7;

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnCheckedChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToggleSwitch),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly RoutedEvent CheckedChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(CheckedChanged), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ToggleSwitch));

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public event RoutedEventHandler CheckedChanged
        {
            add => AddHandler(CheckedChangedEvent, value);
            remove => RemoveHandler(CheckedChangedEvent, value);
        }

        public ToggleSwitch()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
        }

        private static void OnCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToggleSwitch ts)
                ts.RaiseEvent(new RoutedEventArgs(CheckedChangedEvent));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double textWidth = 0;
            if (!string.IsNullOrEmpty(Label))
            {
                var formatted = CreateLabelText();
                textWidth = formatted.Width + 8;
            }
            return new Size(textWidth + TrackWidth + 4, Math.Max(24, TrackHeight + 4));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double textWidth = 0;
            if (!string.IsNullOrEmpty(Label))
            {
                var formatted = CreateLabelText();
                textWidth = formatted.Width + 8;
                double textY = (ActualHeight - formatted.Height) / 2.0;
                dc.DrawText(formatted, new Point(0, textY));
            }

            // Track
            double trackX = textWidth;
            double trackY = (ActualHeight - TrackHeight) / 2.0;
            double trackRadius = TrackHeight / 2.0;
            var trackRect = new Rect(trackX, trackY, TrackWidth, TrackHeight);
            var trackGeo = CreatePillGeometry(trackRect, trackRadius);

            Color trackColor = IsChecked
                ? Services.ThemeService.AccentColor
                : Color.FromArgb(80, 128, 128, 128);
            dc.DrawGeometry(new SolidColorBrush(trackColor), null, trackGeo);

            if (!IsChecked)
            {
                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 160, 160, 160)), 1);
                dc.DrawGeometry(null, borderPen, trackGeo);
            }

            // Thumb
            double thumbX = IsChecked ? trackX + TrackWidth - trackRadius : trackX + trackRadius;
            double thumbY = trackY + TrackHeight / 2.0;
            dc.DrawEllipse(Brushes.White, null, new Point(thumbX, thumbY), ThumbRadius, ThumbRadius);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            IsChecked = !IsChecked;
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                IsChecked = !IsChecked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private FormattedText CreateLabelText()
        {
            var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(Label, System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, typeface, 12, new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static Geometry CreatePillGeometry(Rect rect, double radius)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                double d = Math.Min(radius * 2, Math.Min(rect.Height, rect.Width));
                double r = d / 2;
                ctx.BeginFigure(new Point(rect.Left + r, rect.Top), true, true);
                ctx.LineTo(new Point(rect.Right - r, rect.Top), true, false);
                ctx.ArcTo(new Point(rect.Right, rect.Top + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Right, rect.Bottom - r), true, false);
                ctx.ArcTo(new Point(rect.Right - r, rect.Bottom), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Left + r, rect.Bottom), true, false);
                ctx.ArcTo(new Point(rect.Left, rect.Bottom - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Left, rect.Top + r), true, false);
                ctx.ArcTo(new Point(rect.Left + r, rect.Top), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }
    }
}
