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
            return new Size(w, 88);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double cornerRadius = 10;
            double padding = 12;

            // Card background
            var bgBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            var cardRect = new Rect(0, 0, w, h);
            var cardGeo = CreateRoundedRect(cardRect, cornerRadius);
            dc.DrawGeometry(bgBrush, borderPen, cardGeo);

            // Temperature-based accent color
            Color accentColor = GetTempColor(Temperature);
            var accentBrush = new SolidColorBrush(accentColor);

            // Top accent bar (2px)
            var accentBarRect = new Rect(1, 1, w - 2, 2.5);
            var accentBarGeo = CreateTopRoundedRect(accentBarRect, cornerRadius - 1);
            dc.DrawGeometry(accentBrush, null, accentBarGeo);

            var uiTypeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var uiBoldTypeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var iconTypeface = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var dimBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));

            double y = padding + 2; // below accent bar

            // Row 1: Icon + Label + Temperature (hero)
            var iconFt = new FormattedText(IconGlyph, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, iconTypeface, 13, dimBrush, dpi);
            dc.DrawText(iconFt, new Point(padding, y + 1));

            var labelFt = new FormattedText(Label, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiBoldTypeface, 11.5, dimBrush, dpi);
            dc.DrawText(labelFt, new Point(padding + iconFt.Width + 5, y + 1));

            // Temperature - right-aligned, hero size, colored
            string tempStr = Temperature > 0 ? $"{Temperature:0}°C" : "--";
            var tempFt = new FormattedText(tempStr, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiBoldTypeface, 20, accentBrush, dpi);
            dc.DrawText(tempFt, new Point(w - padding - tempFt.Width, y - 3));

            // Row 2: Fan speed (current)
            y += 28;
            var fanIconFt = new FormattedText("\uE9CA", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, iconTypeface, 11, accentBrush, dpi);
            dc.DrawText(fanIconFt, new Point(padding, y + 2));

            var fanFt = new FormattedText(FanSpeed, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, uiBoldTypeface, 13,
                new SolidColorBrush(Colors.White), dpi);
            dc.DrawText(fanFt, new Point(padding + fanIconFt.Width + 5, y));

            // Row 3: Target/max speed
            y += 22;
            string targetStr = !string.IsNullOrEmpty(TargetSpeed) ? TargetSpeed : "";
            if (!string.IsNullOrEmpty(targetStr))
            {
                var targetFt = new FormattedText(targetStr, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, uiTypeface, 10, dimBrush, dpi);
                dc.DrawText(targetFt, new Point(padding, y));
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

        private static Geometry CreateTopRoundedRect(Rect rect, double r)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                r = Math.Min(r, Math.Min(rect.Height * 2, rect.Width) / 2);
                ctx.BeginFigure(new Point(rect.Left + r, rect.Top), true, true);
                ctx.LineTo(new Point(rect.Right - r, rect.Top), true, false);
                ctx.ArcTo(new Point(rect.Right, rect.Top + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Right, rect.Bottom), true, false);
                ctx.LineTo(new Point(rect.Left, rect.Bottom), true, false);
                ctx.LineTo(new Point(rect.Left, rect.Top + r), true, false);
                ctx.ArcTo(new Point(rect.Left + r, rect.Top), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }
    }
}
