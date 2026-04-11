using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    public class CircularGauge : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(CircularGauge),
                new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HealthTextProperty =
            DependencyProperty.Register(nameof(HealthText), typeof(string), typeof(CircularGauge),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CyclesTextProperty =
            DependencyProperty.Register(nameof(CyclesText), typeof(string), typeof(CircularGauge),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, Math.Clamp(value, 0, 100));
        }

        public string HealthText
        {
            get => (string)GetValue(HealthTextProperty);
            set => SetValue(HealthTextProperty, value);
        }

        public string CyclesText
        {
            get => (string)GetValue(CyclesTextProperty);
            set => SetValue(CyclesTextProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double s = Math.Min(
                double.IsInfinity(availableSize.Width) ? 140 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 140 : availableSize.Height);
            return new Size(s, s);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double pad = 6;
            double infoHeight = (!string.IsNullOrEmpty(HealthText) || !string.IsNullOrEmpty(CyclesText)) ? 30 : 0;
            double arcDiameter = Math.Min(w - pad * 2, h - infoHeight - pad * 2);
            if (arcDiameter <= 0) return;

            double cx = w / 2;
            double cy = pad + arcDiameter / 2;
            double penWidth = Math.Max(5, arcDiameter * 0.08);
            double arcRadius = (arcDiameter - penWidth) / 2;

            // Arc geometry: start at 135 degrees (bottom-left), sweep 270 degrees clockwise
            double startAngle = 135;
            double sweepAngle = 270;
            double fillSweep = sweepAngle * Value / 100.0;

            // Track arc (background)
            DrawArc(dc, cx, cy, arcRadius, startAngle, sweepAngle, penWidth,
                Color.FromArgb(50, 128, 128, 128));

            // Fill arc
            Color fillColor = GetFillColor();
            if (fillSweep > 0.5)
                DrawArc(dc, cx, cy, arcRadius, startAngle, fillSweep, penWidth, fillColor);

            // Glow on fill tip
            if (fillSweep > 5)
                DrawArc(dc, cx, cy, arcRadius, startAngle + fillSweep - 8, 8, penWidth * 2.2,
                    Color.FromArgb(35, fillColor.R, fillColor.G, fillColor.B));

            // Percentage text
            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var boldFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            string pctText = $"{Value}%";
            var pctFt = new FormattedText(pctText, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, boldFace, arcRadius * 0.42,
                new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), dpiScale);
            dc.DrawText(pctFt, new Point(cx - pctFt.Width / 2, cy - pctFt.Height / 2));

            // Info text below arc
            var subFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double infoY = cy + arcRadius + penWidth / 2 + 4;
            var dimBrush = new SolidColorBrush(Color.FromArgb(180, 0xE0, 0xE0, 0xE0));

            if (!string.IsNullOrEmpty(HealthText))
            {
                var ft = new FormattedText(HealthText, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, subFace, 9.5, dimBrush, dpiScale);
                dc.DrawText(ft, new Point(cx - ft.Width / 2, infoY));
                infoY += ft.Height + 1;
            }
            if (!string.IsNullOrEmpty(CyclesText))
            {
                var ft = new FormattedText(CyclesText, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, subFace, 9.5, dimBrush, dpiScale);
                dc.DrawText(ft, new Point(cx - ft.Width / 2, infoY));
            }
        }

        private void DrawArc(DrawingContext dc, double cx, double cy, double radius,
            double startAngleDeg, double sweepAngleDeg, double thickness, Color color)
        {
            double startRad = startAngleDeg * Math.PI / 180;
            double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180;

            var startPt = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            var endPt = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

            bool isLargeArc = sweepAngleDeg > 180;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(startPt, false, false);
                ctx.ArcTo(endPt, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();

            var pen = new Pen(new SolidColorBrush(color), thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawGeometry(null, pen, geo);
        }

        private Color GetFillColor()
        {
            if (Value > 60) return Services.ThemeService.AccentColor;
            if (Value > 30) return Color.FromRgb(255, 180, 0); // Amber
            return Color.FromRgb(0xFF, 0x44, 0x44); // Red
        }
    }
}
