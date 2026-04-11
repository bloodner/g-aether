using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    public class SparklineChart : FrameworkElement
    {
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(double[]), typeof(SparklineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SparklineChart),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeColorProperty =
            DependencyProperty.Register(nameof(StrokeColor), typeof(Color), typeof(SparklineChart),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(SparklineChart),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentTextProperty =
            DependencyProperty.Register(nameof(CurrentText), typeof(string), typeof(SparklineChart),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFontSizeProperty =
            DependencyProperty.Register(nameof(ValueFontSize), typeof(double), typeof(SparklineChart),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double[] Values { get => (double[])GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
        public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
        public Color StrokeColor { get => (Color)GetValue(StrokeColorProperty); set => SetValue(StrokeColorProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public string CurrentText { get => (string)GetValue(CurrentTextProperty); set => SetValue(CurrentTextProperty, value); }
        public double ValueFontSize { get => (double)GetValue(ValueFontSizeProperty); set => SetValue(ValueFontSizeProperty, value); }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var font = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var fontNormal = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            // Draw label (top-left)
            if (!string.IsNullOrEmpty(Label))
            {
                var labelFt = new FormattedText(Label, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, fontNormal, 11,
                    new SolidColorBrush(Color.FromArgb(180, 240, 240, 240)), dpi);
                dc.DrawText(labelFt, new Point(0, 0));
            }

            // Draw current value (top-right)
            if (!string.IsNullOrEmpty(CurrentText))
            {
                var valFt = new FormattedText(CurrentText, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, font, ValueFontSize,
                    new SolidColorBrush(StrokeColor), dpi);
                dc.DrawText(valFt, new Point(w - valFt.Width, -2));
            }

            // Chart area starts below the text
            double chartTop = 24;
            double chartH = h - chartTop;
            if (chartH < 10) return;

            var values = Values;
            if (values == null || values.Length < 2) return;

            double max = MaxValue > 0 ? MaxValue : 100;
            int count = values.Length;
            double step = w / (count - 1);

            // Build the line geometry
            var lineGeo = new StreamGeometry();
            var fillGeo = new StreamGeometry();

            using (var ctx = lineGeo.Open())
            {
                var first = new Point(0, chartTop + chartH - (values[0] / max) * chartH);
                ctx.BeginFigure(first, false, false);
                for (int i = 1; i < count; i++)
                {
                    double x = i * step;
                    double y = chartTop + chartH - (Math.Clamp(values[i], 0, max) / max) * chartH;
                    ctx.LineTo(new Point(x, y), true, true);
                }
            }
            lineGeo.Freeze();

            // Fill under the line
            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(0, chartTop + chartH), true, true);
                for (int i = 0; i < count; i++)
                {
                    double x = i * step;
                    double y = chartTop + chartH - (Math.Clamp(values[i], 0, max) / max) * chartH;
                    ctx.LineTo(new Point(x, y), true, true);
                }
                ctx.LineTo(new Point((count - 1) * step, chartTop + chartH), true, true);
            }
            fillGeo.Freeze();

            // Gradient fill
            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(60, StrokeColor.R, StrokeColor.G, StrokeColor.B),
                Color.FromArgb(5, StrokeColor.R, StrokeColor.G, StrokeColor.B),
                new Point(0, 0), new Point(0, 1));
            dc.DrawGeometry(fillBrush, null, fillGeo);

            // Line stroke
            var pen = new Pen(new SolidColorBrush(StrokeColor), 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            dc.DrawGeometry(null, pen, lineGeo);

            // Current value dot at the end
            double lastY = chartTop + chartH - (Math.Clamp(values[count - 1], 0, max) / max) * chartH;
            dc.DrawEllipse(new SolidColorBrush(StrokeColor), null, new Point((count - 1) * step, lastY), 3, 3);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, StrokeColor.R, StrokeColor.G, StrokeColor.B)),
                null, new Point((count - 1) * step, lastY), 6, 6);
        }
    }
}
