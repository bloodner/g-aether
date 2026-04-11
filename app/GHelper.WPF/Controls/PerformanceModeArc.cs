using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// A semicircular arc gauge for selecting performance mode (Silent / Balanced / Turbo).
    /// Gradient-colored arc (teal -> green -> yellow -> red), mode name centered inside.
    /// </summary>
    public class PerformanceModeArc : FrameworkElement
    {
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(PerformanceModeArc),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(PerformanceModeArc),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public string[]? Items
        {
            get => (string[]?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        // Mode colors: Silent=purple, Balanced=green, Turbo=orange
        private static readonly Color[] ModeColors =
        [
            Color.FromRgb(0xA7, 0x8B, 0xFA), // Silent - purple
            Color.FromRgb(0x60, 0xCD, 0xFF), // Balanced - blue
            Color.FromRgb(0xFF, 0x6B, 0x35),  // Turbo - orange
        ];

        private double _cx, _cy, _arcRadius;
        private readonly List<Rect> _hitAreas = new();

        public PerformanceModeArc()
        {
            Focusable = true;
            Cursor = Cursors.Hand;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 280 : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height) ? 200 : Math.Min(availableSize.Height, 220);
            return new Size(w, Math.Max(140, h));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var items = Items;
            if (items == null || items.Length == 0) return;

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            int count = items.Length;
            int selIdx = Math.Clamp(SelectedIndex, 0, count - 1);

            // Layout
            double penWidth = 10;
            double topPad = 50;
            double bottomPad = 20;
            double sidePad = 44;
            double usableH = h - topPad - bottomPad;
            double usableW = w - sidePad * 2;
            double maxRadius = Math.Min(usableW / 2, usableH * 0.65);
            _arcRadius = Math.Max(36, maxRadius);
            _cx = w / 2;
            _cy = topPad + usableH * 0.45;

            Color accent = GetModeColor(selIdx);
            Color dimTrack = Color.FromArgb(40, 255, 255, 255);
            Color dimText = Color.FromArgb(120, 255, 255, 255);
            Color brightText = Color.FromRgb(0xF0, 0xF0, 0xF0);

            // Background track arc
            DrawArc(dc, _cx, _cy, _arcRadius, 225, 270, penWidth, dimTrack);

            // Filled arc — draw sub-segments with interpolated gradient color
            // Each sub-segment covers ~3° of arc, all starting from gauge angle 225 (Silent)
            // and progressing toward the selected mode position
            if (count > 1 && selIdx > 0)
            {
                double totalFillSweep = 270.0 * selIdx / (count - 1);
                int subSegments = Math.Max(1, (int)(totalFillSweep / 3));
                double subSweep = totalFillSweep / subSegments;

                for (int s = 0; s < subSegments; s++)
                {
                    double t = (s + 0.5) / subSegments;
                    Color segColor = GetGradientColor(t, selIdx, count);
                    // GetAngleForIndex goes from 225° down, so sub-segments go from
                    // math angle 225 decreasing. In gauge coords, start = 225 - offset.
                    double gaugeStart = 225 - s * subSweep;
                    // Draw a small arc segment at this position
                    DrawArcSegment(dc, _cx, _cy, _arcRadius, gaugeStart, subSweep + 0.5, penWidth, segColor);
                }
            }
            else if (count == 1)
            {
                DrawArc(dc, _cx, _cy, _arcRadius, 225, 270, penWidth, accent);
            }

            // Tick marks and labels
            _hitAreas.Clear();
            var labelFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var labelFaceBold = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double dotR = penWidth * 0.8;

            for (int i = 0; i < count; i++)
            {
                double angle = GetAngleForIndex(i, count);
                double rad = angle * Math.PI / 180;
                var center = new Point(_cx + _arcRadius * Math.Cos(rad), _cy - _arcRadius * Math.Sin(rad));
                bool selected = (i == selIdx);
                Color modeColor = GetModeColor(i);

                if (selected)
                {
                    double thumbR = dotR * 1.3;
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, modeColor.R, modeColor.G, modeColor.B)),
                        null, center, thumbR * 2, thumbR * 2);
                    dc.DrawEllipse(new SolidColorBrush(modeColor), null, center, thumbR, thumbR);
                    dc.DrawEllipse(Brushes.White, null, center, thumbR * 0.55, thumbR * 0.55);
                }
                else
                {
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), null, center, dotR, dotR);
                    dc.DrawEllipse(null,
                        new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.5),
                        center, dotR, dotR);
                }

                // Label outside arc
                double labelRadius = _arcRadius + penWidth * 1.5 + 14;
                var labelPos = new Point(_cx + labelRadius * Math.Cos(rad), _cy - labelRadius * Math.Sin(rad));

                Color lColor = selected ? modeColor : dimText;
                Typeface face = selected ? labelFaceBold : labelFace;
                double fontSize = selected ? 11.5 : 10.5;

                var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, face, fontSize,
                    new SolidColorBrush(lColor), dpiScale);

                double lx = labelPos.X - ft.Width / 2;
                double ly = labelPos.Y - ft.Height / 2;
                dc.DrawText(ft, new Point(lx, ly));

                _hitAreas.Add(new Rect(lx - 8, ly - 8, ft.Width + 16, ft.Height + 16));
            }

            // Center readout: mode name
            var modeName = items[selIdx];
            var nameFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var nameFt = new FormattedText(modeName, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, nameFace, _arcRadius * 0.3,
                new SolidColorBrush(brightText), dpiScale);
            dc.DrawText(nameFt, new Point(_cx - nameFt.Width / 2, _cy - nameFt.Height * 0.55));

            // Subtitle: "Performance Mode"
            var subFt = new FormattedText("Performance Mode", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                10, new SolidColorBrush(dimText), dpiScale);
            dc.DrawText(subFt, new Point(_cx - subFt.Width / 2, _cy + nameFt.Height * 0.25));
        }

        /// <summary>
        /// Get interpolated gradient color along the filled arc.
        /// t is 0..1 within the filled portion, selIdx is the selected mode index.
        /// </summary>
        private static Color GetGradientColor(double t, int selIdx, int count)
        {
            if (selIdx == 0) return ModeColors[0];

            // Map t (0..1 of filled arc) to position across mode color stops 0..selIdx
            double pos = t * selIdx; // e.g., for Turbo(2): 0..2
            int lo = Math.Clamp((int)pos, 0, ModeColors.Length - 2);
            int hi = Math.Min(lo + 1, ModeColors.Length - 1);
            double frac = pos - lo;
            return Lerp(ModeColors[lo], ModeColors[hi], frac);
        }

        private static Color GetModeColor(int index)
        {
            if (index < 0) return ModeColors[0];
            if (index >= ModeColors.Length) return ModeColors[^1];
            return ModeColors[index];
        }

        private static Color Lerp(Color a, Color b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        private static double GetAngleForIndex(int index, int count)
        {
            if (count <= 1) return 90;
            double startMath = 225.0;
            double t = (double)index / (count - 1);
            return startMath - t * 270.0;
        }

        /// <summary>
        /// Draw background track arc. Uses gauge convention: startAngleDeg=225 is bottom-left,
        /// sweeps CW through top toward bottom-right.
        /// </summary>
        private static void DrawArc(DrawingContext dc, double cx, double cy, double radius,
            double startAngleDeg, double sweepAngleDeg, double thickness, Color color)
        {
            // Convert gauge start to math angle, then to screen
            // Gauge 225 = math 225° (bottom-left). Sweep goes CW in math = decreasing angle.
            double mathStart = startAngleDeg;
            double mathEnd = startAngleDeg - sweepAngleDeg;

            // Screen coords: Y is flipped, so screen_angle = -math_angle
            double startRad = -mathStart * Math.PI / 180;
            double endRad = -mathEnd * Math.PI / 180;

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

        /// <summary>
        /// Draw a fill sub-segment. mathStartAngle is in math coords (225=bottom-left),
        /// sweeps CW (decreasing math angle) by sweepDeg degrees.
        /// </summary>
        private static void DrawArcSegment(DrawingContext dc, double cx, double cy, double radius,
            double mathStartAngle, double sweepDeg, double thickness, Color color)
        {
            double mathEnd = mathStartAngle - sweepDeg;

            double startRad = -mathStartAngle * Math.PI / 180;
            double endRad = -mathEnd * Math.PI / 180;

            var startPt = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            var endPt = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

            bool isLargeArc = sweepDeg > 180;

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

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            CaptureMouse();
            SnapToAngle(e.GetPosition(this));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
                SnapToAngle(e.GetPosition(this));
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ReleaseMouseCapture();
        }

        private void SnapToAngle(Point pos)
        {
            var items = Items;
            if (items == null || items.Length == 0) return;

            double dx = pos.X - _cx;
            double dy = -(pos.Y - _cy);
            double mouseAngle = Math.Atan2(dy, dx) * 180 / Math.PI;

            double bestDist = double.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < items.Length; i++)
            {
                double itemAngle = GetAngleForIndex(i, items.Length);
                double diff = AngleDiff(mouseAngle, itemAngle);
                if (diff < bestDist) { bestDist = diff; bestIdx = i; }
            }
            SelectedIndex = bestIdx;
        }

        private static double AngleDiff(double a, double b)
        {
            double diff = ((a - b) % 360 + 360) % 360;
            return Math.Min(diff, 360 - diff);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var items = Items;
            int count = items?.Length ?? 0;
            if (count == 0) { base.OnKeyDown(e); return; }

            switch (e.Key)
            {
                case Key.Right:
                case Key.Up:
                    SelectedIndex = Math.Min(count - 1, SelectedIndex + 1);
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.Down:
                    SelectedIndex = Math.Max(0, SelectedIndex - 1);
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }
    }
}
