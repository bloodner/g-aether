using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GHelper.WPF.Controls
{
    public class GradientModeSlider : FrameworkElement
    {
        private double _thumbX;
        private double _thumbRadius;
        private double _barHeight;
        private Rect _barRect;

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(GradientModeSlider),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedIndexChanged));

        public static readonly DependencyProperty ModeCountProperty =
            DependencyProperty.Register(nameof(ModeCount), typeof(int), typeof(GradientModeSlider),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty GradientStopsProperty =
            DependencyProperty.Register(nameof(GradientStops), typeof(GradientStopCollection), typeof(GradientModeSlider),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelsProperty =
            DependencyProperty.Register(nameof(Labels), typeof(string[]), typeof(GradientModeSlider),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly RoutedEvent SelectedIndexChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectedIndexChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int>), typeof(GradientModeSlider));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public int ModeCount
        {
            get => (int)GetValue(ModeCountProperty);
            set => SetValue(ModeCountProperty, value);
        }

        public GradientStopCollection? GradientStops
        {
            get => (GradientStopCollection?)GetValue(GradientStopsProperty);
            set => SetValue(GradientStopsProperty, value);
        }

        public string[]? Labels
        {
            get => (string[]?)GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<int> SelectedIndexChanged
        {
            add => AddHandler(SelectedIndexChangedEvent, value);
            remove => RemoveHandler(SelectedIndexChangedEvent, value);
        }

        public GradientModeSlider()
        {
            Focusable = true;
            Cursor = Cursors.Hand;
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GradientModeSlider slider)
            {
                slider.AnimateThumb();
                slider.RaiseEvent(new RoutedPropertyChangedEventArgs<int>(
                    (int)e.OldValue, (int)e.NewValue, SelectedIndexChangedEvent));
            }
        }

        private void AnimateThumb()
        {
            Recalculate();
            double targetX = GetSnapX(SelectedIndex);
            _thumbX = targetX;
            InvalidateVisual();
        }

        private void Recalculate()
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            _thumbRadius = Math.Max(8, h * 0.22);
            _barHeight = Math.Max(6, h * 0.16);
            double labelSpace = (Labels?.Length > 0) ? h * 0.38 : 0;
            double barY = (h - labelSpace - _barHeight) / 2.0;
            _barRect = new Rect(_thumbRadius, barY, w - 2 * _thumbRadius, _barHeight);
        }

        private double GetSnapX(int index)
        {
            if (ModeCount <= 1) return _barRect.Left;
            return _barRect.Left + _barRect.Width * index / (ModeCount - 1);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double h = double.IsInfinity(availableSize.Height) ? 80 : availableSize.Height;
            double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
            return new Size(w, Math.Max(60, h));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            Recalculate();
            if (_barRect.Width <= 0) return;

            // Draw gradient rail
            double barRadius = _barHeight / 2.0;
            var barGeo = CreateRoundedBarGeometry(_barRect, barRadius);

            Brush railBrush;
            if (GradientStops != null && GradientStops.Count >= 2)
            {
                railBrush = new LinearGradientBrush(GradientStops,
                    new Point(0, 0.5), new Point(1, 0.5));
            }
            else
            {
                railBrush = new SolidColorBrush(Color.FromArgb(100, 128, 128, 128));
            }
            dc.DrawGeometry(railBrush, null, barGeo);

            // Draw snap dots
            var dotBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            double barCenterY = _barRect.Top + _barRect.Height / 2.0;
            for (int i = 0; i < ModeCount; i++)
            {
                if (i == SelectedIndex) continue;
                double cx = GetSnapX(i);
                dc.DrawEllipse(dotBrush, null, new Point(cx, barCenterY), 3, 3);
            }

            // Draw thumb
            double thumbX = _thumbX > 0 ? _thumbX : GetSnapX(SelectedIndex);
            double thumbY = barCenterY;

            // Glow
            var glowBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            dc.DrawEllipse(glowBrush, null, new Point(thumbX, thumbY), _thumbRadius * 1.3, _thumbRadius * 1.3);

            // Accent circle
            Color thumbColor = GetColorAtPosition(SelectedIndex);
            dc.DrawEllipse(new SolidColorBrush(thumbColor), null, new Point(thumbX, thumbY), _thumbRadius, _thumbRadius);

            // Inner white dot
            dc.DrawEllipse(Brushes.White, null, new Point(thumbX, thumbY), _thumbRadius * 0.55, _thumbRadius * 0.55);

            // Labels
            var labels = Labels;
            if (labels != null && labels.Length > 0)
            {
                double labelY = _barRect.Bottom + _thumbRadius + 4;
                var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                for (int i = 0; i < Math.Min(labels.Length, ModeCount); i++)
                {
                    Color labelColor = (i == SelectedIndex) ? thumbColor : Color.FromRgb(0xE0, 0xE0, 0xE0);
                    var formatted = new FormattedText(labels[i], System.Globalization.CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, typeface, 11, new SolidColorBrush(labelColor),
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double cx = GetSnapX(i);
                    double labelX = cx - formatted.Width / 2.0;
                    labelX = Math.Max(0, Math.Min(labelX, ActualWidth - formatted.Width));
                    dc.DrawText(formatted, new Point(labelX, labelY));
                }
            }
        }

        private Color GetColorAtPosition(int index)
        {
            var stops = GradientStops;
            if (stops == null || stops.Count == 0) return Color.FromRgb(0x4B, 0xC8, 0xFF);
            if (stops.Count == 1) return stops[0].Color;

            double t = (double)index / Math.Max(1, ModeCount - 1);
            // Find the two surrounding stops
            var sorted = stops.OrderBy(s => s.Offset).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (t >= sorted[i].Offset && t <= sorted[i + 1].Offset)
                {
                    double segT = (t - sorted[i].Offset) / (sorted[i + 1].Offset - sorted[i].Offset);
                    return LerpColor(sorted[i].Color, sorted[i + 1].Color, segT);
                }
            }
            return sorted[^1].Color;
        }

        private static Color LerpColor(Color a, Color b, double t)
        {
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            CaptureMouse();
            SnapToNearest(e.GetPosition(this).X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
                SnapToNearest(e.GetPosition(this).X);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ReleaseMouseCapture();
        }

        private void SnapToNearest(double x)
        {
            double bestDist = double.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < ModeCount; i++)
            {
                double dist = Math.Abs(x - GetSnapX(i));
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }
            SelectedIndex = bestIdx;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                case Key.Down:
                    SelectedIndex = Math.Min(ModeCount - 1, SelectedIndex + 1);
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.Up:
                    SelectedIndex = Math.Max(0, SelectedIndex - 1);
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }

        private static Geometry CreateRoundedBarGeometry(Rect rect, double radius)
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
