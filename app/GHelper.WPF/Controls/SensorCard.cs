using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    public class SensorCard : FrameworkElement
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(SensorCard),
                new FrameworkPropertyMetadata("CPU", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(SensorCard),
                new FrameworkPropertyMetadata("\uE950", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TemperatureProperty =
            DependencyProperty.Register(nameof(Temperature), typeof(double), typeof(SensorCard),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FanSpeedProperty =
            DependencyProperty.Register(nameof(FanSpeed), typeof(string), typeof(SensorCard),
                new FrameworkPropertyMetadata("--", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TargetSpeedProperty =
            DependencyProperty.Register(nameof(TargetSpeed), typeof(string), typeof(SensorCard),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public double Temperature
        {
            get => (double)GetValue(TemperatureProperty);
            set => SetValue(TemperatureProperty, value);
        }

        public string FanSpeed
        {
            get => (string)GetValue(FanSpeedProperty);
            set => SetValue(FanSpeedProperty, value);
        }

        public string TargetSpeed
        {
            get => (string)GetValue(TargetSpeedProperty);
            set => SetValue(TargetSpeedProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
            return new Size(w, 92);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double cornerRadius = 10;
            double padding = 14;

            Color accentColor = GetTempColor(Temperature);
            var accentBrush = new SolidColorBrush(accentColor);
            accentBrush.Freeze();

            var cardRect = new Rect(0, 0, w, h);
            var cardGeo = CreateRoundedRect(cardRect, cornerRadius);

            // Fill: sits one luminance step above the deep window background — matches
            // TileStyle so every tiled surface in the app reads with the same hierarchy.
            var bgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x14, 0x1B));
            bgBrush.Freeze();
            dc.DrawGeometry(bgBrush, null, cardGeo);

            // Dynamic halo — intensity scales with Temperature / 100°C (clamped).
            // Idle sensors fade to pure dark; hot sensors glow vividly. The card
            // becomes a direct visual readout of activity without reading the number.
            double haloIntensity = Math.Clamp(Temperature / 100.0, 0.0, 1.0);
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

            // Hairline border for edge definition — matches TileStyle.
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0x25, 0x2E)), 1);
            dc.DrawGeometry(null, borderPen, cardGeo);

            // Left vertical accent stripe — slim status indicator in the temperature color.
            var stripeRect = new Rect(0, 12, 3, h - 24);
            dc.DrawRoundedRectangle(accentBrush, null, stripeRect, 1.5, 1.5);

            var uiTypeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var uiMedium = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
            var uiBold = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var iconTypeface = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            var dimBrush = new SolidColorBrush(Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF));
            var midBrush = new SolidColorBrush(Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF));
            var white = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));

            double leftX = padding + 4; // clear the stripe
            double top = padding - 2;

            // Row 1: icon + uppercase label (micro).
            var iconFt = new FormattedText(IconGlyph, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, iconTypeface, 11, dimBrush, dpi);
            dc.DrawText(iconFt, new Point(leftX, top + 2));

            var labelFt = new FormattedText(Label.ToUpperInvariant(), CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiMedium, 10, dimBrush, dpi);
            dc.DrawText(labelFt, new Point(leftX + iconFt.Width + 6, top + 3));

            // Hero temperature, right-aligned, with a smaller °C unit beside it.
            string tempNum = Temperature > 0 ? $"{Temperature:0}" : "--";
            var tempFt = new FormattedText(tempNum, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiBold, 28, accentBrush, dpi);
            var unitFt = new FormattedText("°C", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiMedium, 12, accentBrush, dpi);
            double tempRight = w - padding;
            dc.DrawText(unitFt, new Point(tempRight - unitFt.Width, top + 6));
            dc.DrawText(tempFt, new Point(tempRight - unitFt.Width - tempFt.Width - 2, top - 6));

            // Row 2: fan speed (current) — main readout.
            double fanY = h - padding - 18;
            var fanIconFt = new FormattedText("\uE9CA", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, iconTypeface, 11, midBrush, dpi);
            dc.DrawText(fanIconFt, new Point(leftX, fanY + 3));

            var fanFt = new FormattedText(FanSpeed, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiBold, 14, white, dpi);
            dc.DrawText(fanFt, new Point(leftX + fanIconFt.Width + 6, fanY));

            // Row 3: target (only when populated).
            if (!string.IsNullOrEmpty(TargetSpeed))
            {
                var targetLabelFt = new FormattedText("target", CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, uiTypeface, 9.5,
                    new SolidColorBrush(Color.FromArgb(0x64, 0xFF, 0xFF, 0xFF)), dpi);
                var targetFt = new FormattedText(TargetSpeed, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, uiMedium, 10, dimBrush, dpi);
                double tY = fanY + 20;
                dc.DrawText(targetLabelFt, new Point(leftX, tY));
                dc.DrawText(targetFt, new Point(leftX + targetLabelFt.Width + 4, tY));
            }
        }

        private static Color GetTempColor(double temp)
        {
            if (temp <= 0) return Color.FromRgb(0x60, 0x60, 0x60); // inactive grey

            // Blue < 50, Green 50-65, Yellow 65-80, Orange 80-90, Red 90+
            if (temp < 50)
                return Lerp(Color.FromRgb(0x4B, 0xD8, 0xC8), Color.FromRgb(0x4C, 0xC9, 0x5E), (temp - 30) / 20.0);
            if (temp < 65)
                return Lerp(Color.FromRgb(0x4C, 0xC9, 0x5E), Color.FromRgb(0xFF, 0xD8, 0x00), (temp - 50) / 15.0);
            if (temp < 80)
                return Lerp(Color.FromRgb(0xFF, 0xD8, 0x00), Color.FromRgb(0xFF, 0x6B, 0x35), (temp - 65) / 15.0);
            if (temp < 90)
                return Lerp(Color.FromRgb(0xFF, 0x6B, 0x35), Color.FromRgb(0xFF, 0x44, 0x44), (temp - 80) / 10.0);
            return Color.FromRgb(0xFF, 0x44, 0x44);
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
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
