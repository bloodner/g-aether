using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// Self-rendered tile showing a label, a current-value readout, and a small
    /// sparkline of recent history. Draws its own card background (fill, halo,
    /// hairline border, left accent stripe) so each Monitor tile carries the same
    /// design language as the Performance-page SensorCards.
    /// </summary>
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
            double cornerRadius = 10;
            double padding = 14;
            double leftX = padding + 4;       // clear the stripe
            double rightX = w - padding;

            Color accentColor = StrokeColor;
            var accentBrush = new SolidColorBrush(accentColor);
            accentBrush.Freeze();

            // === Card background ===============================================

            var cardRect = new Rect(0, 0, w, h);
            var cardGeo = CreateRoundedRect(cardRect, cornerRadius);

            // Fill: matches TileStyle / SensorCard.
            var bgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x14, 0x1B));
            bgBrush.Freeze();
            dc.DrawGeometry(bgBrush, null, cardGeo);

            // Dynamic halo — intensity scales with the latest reading as a fraction
            // of MaxValue. Idle metrics (0 W, 0% usage) fade to pure dark; busy metrics
            // light up, so the tile doubles as an at-a-glance activity indicator.
            double haloIntensity = 0.0;
            var snapshot = Values;
            if (snapshot != null && snapshot.Length > 0 && MaxValue > 0)
                haloIntensity = Math.Clamp(snapshot[snapshot.Length - 1] / MaxValue, 0.0, 1.0);
            byte haloAlpha = (byte)(0x32 * haloIntensity);
            if (haloAlpha > 0)
            {
                var halo = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.92, 0.08),
                    Center = new Point(0.92, 0.08),
                    RadiusX = 0.95,
                    RadiusY = 1.4
                };
                halo.GradientStops.Add(new GradientStop(Color.FromArgb(haloAlpha, accentColor.R, accentColor.G, accentColor.B), 0.0));
                halo.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, accentColor.R, accentColor.G, accentColor.B), 0.60));
                halo.Freeze();
                dc.DrawGeometry(halo, null, cardGeo);
            }

            // Hairline border.
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0x25, 0x2E)), 1);
            dc.DrawGeometry(null, borderPen, cardGeo);

            // Left vertical accent stripe.
            var stripeRect = new Rect(0, 12, 3, h - 24);
            dc.DrawRoundedRectangle(accentBrush, null, stripeRect, 1.5, 1.5);

            // === Content =======================================================

            var font = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var fontMedium = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            var fontNormal = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            double labelY = padding - 2;

            // Label (top-left, inset past stripe).
            if (!string.IsNullOrEmpty(Label))
            {
                var labelFt = new FormattedText(Label, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, fontNormal, 11,
                    new SolidColorBrush(Color.FromArgb(180, 240, 240, 240)), dpi);
                dc.DrawText(labelFt, new Point(leftX, labelY));
            }

            // Current value: split into number + unit so the number reads as the hero
            // (matches Performance-page SensorCard). The number hangs up above the label
            // baseline; the unit sits inline with it at roughly half size.
            if (!string.IsNullOrEmpty(CurrentText))
            {
                var (numberPart, unitPart) = SplitValueAndUnit(CurrentText);
                double unitSize = Math.Max(10, ValueFontSize * 0.5);

                var numFt = new FormattedText(numberPart, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, font, ValueFontSize, accentBrush, dpi);
                var unitFt = new FormattedText(unitPart, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, fontMedium, unitSize, accentBrush, dpi);

                double unitX = rightX - unitFt.Width;
                double numX = unitX - numFt.Width - 2;
                dc.DrawText(unitFt, new Point(unitX, padding + 4));
                dc.DrawText(numFt, new Point(numX, padding - 8));
            }

            // Sparkline area begins below the hero value. Push chartTop proportional
            // to the value font so bumping ValueFontSize in XAML doesn't collide with
            // the sparkline.
            double chartLeft = leftX;
            double chartRight = rightX;
            double chartTop = padding + Math.Max(ValueFontSize - 4, 20);
            double chartBot = h - padding;
            double chartW = chartRight - chartLeft;
            double chartH = chartBot - chartTop;
            if (chartW < 10 || chartH < 10) return;

            var values = Values;
            if (values == null || values.Length < 2) return;

            double max = MaxValue > 0 ? MaxValue : 100;
            int count = values.Length;
            double step = chartW / (count - 1);

            var lineGeo = new StreamGeometry();
            var fillGeo = new StreamGeometry();

            using (var ctx = lineGeo.Open())
            {
                var first = new Point(chartLeft, chartBot - (Math.Clamp(values[0], 0, max) / max) * chartH);
                ctx.BeginFigure(first, false, false);
                for (int i = 1; i < count; i++)
                {
                    double x = chartLeft + i * step;
                    double y = chartBot - (Math.Clamp(values[i], 0, max) / max) * chartH;
                    ctx.LineTo(new Point(x, y), true, true);
                }
            }
            lineGeo.Freeze();

            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(chartLeft, chartBot), true, true);
                for (int i = 0; i < count; i++)
                {
                    double x = chartLeft + i * step;
                    double y = chartBot - (Math.Clamp(values[i], 0, max) / max) * chartH;
                    ctx.LineTo(new Point(x, y), true, true);
                }
                ctx.LineTo(new Point(chartLeft + (count - 1) * step, chartBot), true, true);
            }
            fillGeo.Freeze();

            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B),
                Color.FromArgb(5, accentColor.R, accentColor.G, accentColor.B),
                new Point(0, 0), new Point(0, 1));
            dc.DrawGeometry(fillBrush, null, fillGeo);

            var pen = new Pen(new SolidColorBrush(accentColor), 1.5)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            dc.DrawGeometry(null, pen, lineGeo);

            // Current-value dot at the right edge.
            double lastY = chartBot - (Math.Clamp(values[count - 1], 0, max) / max) * chartH;
            var lastPt = new Point(chartLeft + (count - 1) * step, lastY);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                null, lastPt, 6, 6);
            dc.DrawEllipse(accentBrush, null, lastPt, 3, 3);
        }

        /// <summary>
        /// Split a readout like "45°C", "1900RPM", "0W", or "--" into numeric prefix
        /// and unit suffix so they can render at separate sizes.
        /// </summary>
        private static (string number, string unit) SplitValueAndUnit(string text)
        {
            int i = 0;
            while (i < text.Length &&
                   (char.IsDigit(text[i]) || text[i] == '.' || text[i] == '-' || text[i] == '+'))
                i++;
            return (text.Substring(0, i), text.Substring(i));
        }

        private static Geometry CreateRoundedRect(Rect rect, double r)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                r = Math.Min(r, Math.Min(rect.Height, rect.Width) / 2);
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
