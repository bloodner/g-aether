using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GHelper.WPF.Services;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// A semicircular arc gauge for selecting screen refresh rate.
    /// Tick marks at each available frequency, filled arc up to selected rate,
    /// large Hz readout centered inside.
    /// </summary>
    public class RefreshRateArc : FrameworkElement
    {
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(RefreshRateArc),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(RefreshRateArc),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(RefreshRateArc),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsAutoModeProperty =
            DependencyProperty.Register(nameof(IsAutoMode), typeof(bool), typeof(RefreshRateArc),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentRateTextProperty =
            DependencyProperty.Register(nameof(CurrentRateText), typeof(string), typeof(RefreshRateArc),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

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

        public Color AccentColor
        {
            get => (Color)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        public bool IsAutoMode
        {
            get => (bool)GetValue(IsAutoModeProperty);
            set => SetValue(IsAutoModeProperty, value);
        }

        public string CurrentRateText
        {
            get => (string)GetValue(CurrentRateTextProperty);
            set => SetValue(CurrentRateTextProperty, value);
        }

        // Arc layout constants
        private const double StartAngle = 225; // bottom-left (degrees, 0=right, CW)
        private const double EndAngle = 315;   // bottom-right
        private const double SweepAngle = 270;  // total arc sweep (225 -> 315 going CW through top)

        private double _cx, _cy, _arcRadius;
        private readonly List<Rect> _hitAreas = new();

        public RefreshRateArc()
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

        // "Auto" means "let the system decide" — same semantic as GPU Optimized mode,
        // so we reuse its magenta. Any "system-adaptive" surface in the app reads as
        // magenta, creating a learnable palette rule.
        private static Color AutoAccent => ThemeService.ColorOptimized;

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
            bool autoMode = IsAutoMode;

            // Layout — compact arc with padding for labels
            double penWidth = 10;
            double topPad = 50;    // room for top label + breathing room from header
            double bottomPad = 20; // room for bottom labels
            double sidePad = 44;   // room for side labels
            double usableH = h - topPad - bottomPad;
            double usableW = w - sidePad * 2;
            double maxRadius = Math.Min(usableW / 2, usableH * 0.65);
            _arcRadius = Math.Max(36, maxRadius);
            _cx = w / 2;
            _cy = topPad + usableH * 0.45;

            Color accent = autoMode ? AutoAccent : AccentColor;
            Color dimTrack = Color.FromArgb(40, 255, 255, 255);
            Color dimText = Color.FromArgb(120, 255, 255, 255);
            Color dimmedLabel = Color.FromArgb(60, 255, 255, 255); // extra dim for auto mode manual labels
            Color brightText = Color.FromRgb(0xF0, 0xF0, 0xF0);

            // --- Background track arc ---
            DrawArc(dc, _cx, _cy, _arcRadius, StartAngle, SweepAngle, penWidth, dimTrack);

            // --- Filled arc ---
            if (autoMode)
            {
                // Auto mode: fill the entire arc green
                DrawArc(dc, _cx, _cy, _arcRadius, StartAngle, SweepAngle, penWidth, accent);
            }
            else if (count > 1 && selIdx > 0)
            {
                double fillSweep = SweepAngle * selIdx / (count - 1);
                DrawArc(dc, _cx, _cy, _arcRadius, StartAngle, fillSweep, penWidth, accent);
            }
            else if (count == 1)
            {
                DrawArc(dc, _cx, _cy, _arcRadius, StartAngle, SweepAngle, penWidth, accent);
            }

            // --- Tick marks and labels ---
            _hitAreas.Clear();
            var labelFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var labelFaceBold = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            double dotR = penWidth * 0.8; // circle radius larger than arc stroke

            for (int i = 0; i < count; i++)
            {
                double angle = GetAngleForIndex(i, count);
                double rad = angle * Math.PI / 180;
                var center = new Point(_cx + _arcRadius * Math.Cos(rad), _cy - _arcRadius * Math.Sin(rad));
                bool selected = (i == selIdx);
                bool isAutoItem = (i == 0); // first item is always "Auto"

                if (selected)
                {
                    // Selected: accent circle with white center + glow
                    double thumbR = dotR * 1.3;
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)),
                        null, center, thumbR * 2, thumbR * 2);
                    dc.DrawEllipse(new SolidColorBrush(accent), null, center, thumbR, thumbR);
                    dc.DrawEllipse(Brushes.White, null, center, thumbR * 0.55, thumbR * 0.55);
                }
                else if (autoMode && !isAutoItem)
                {
                    // Auto mode: manual rate dots are extra dimmed
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), null, center, dotR, dotR);
                    dc.DrawEllipse(null,
                        new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1.5),
                        center, dotR, dotR);
                }
                else
                {
                    // Unselected: subtle circle with border
                    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), null, center, dotR, dotR);
                    dc.DrawEllipse(null,
                        new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.5),
                        center, dotR, dotR);
                }

                // Label outside arc
                double labelRadius = _arcRadius + penWidth * 1.5 + 14;
                var labelPos = new Point(_cx + labelRadius * Math.Cos(rad), _cy - labelRadius * Math.Sin(rad));

                // In auto mode, manual labels are dimmed to ~40% opacity
                Color lColor;
                Typeface face;
                double fontSize;
                if (selected)
                {
                    lColor = accent;
                    face = labelFaceBold;
                    fontSize = 11.5;
                }
                else if (autoMode && !isAutoItem)
                {
                    lColor = dimmedLabel;
                    face = labelFace;
                    fontSize = 10.5;
                }
                else
                {
                    lColor = dimText;
                    face = labelFace;
                    fontSize = 10.5;
                }

                var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, face, fontSize,
                    new SolidColorBrush(lColor), dpiScale);

                // Center label on the tick position
                double lx = labelPos.X - ft.Width / 2;
                double ly = labelPos.Y - ft.Height / 2;
                dc.DrawText(ft, new Point(lx, ly));

                // Hit area for click detection
                _hitAreas.Add(new Rect(lx - 8, ly - 8, ft.Width + 16, ft.Height + 16));
            }

            // --- Center readout ---
            if (autoMode)
            {
                // Auto mode: show "Auto" label and actual current rate
                var autoFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var normalFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                string rateText = CurrentRateText;
                bool hasRate = !string.IsNullOrEmpty(rateText) && rateText != "0";

                if (hasRate)
                {
                    // Show actual rate large, with "Auto" subtitle
                    var rateFt = new FormattedText(rateText, CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, autoFace, _arcRadius * 0.32,
                        new SolidColorBrush(brightText), dpiScale);
                    dc.DrawText(rateFt, new Point(_cx - rateFt.Width / 2, _cy - rateFt.Height * 0.7));

                    // "Hz" unit
                    var unitFt = new FormattedText("Hz", CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, normalFace, 14,
                        new SolidColorBrush(dimText), dpiScale);
                    dc.DrawText(unitFt, new Point(_cx - unitFt.Width / 2, _cy + rateFt.Height * 0.15));

                    // "Auto" badge below
                    var autoLabelFt = new FormattedText("Auto", CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, normalFace, 10,
                        new SolidColorBrush(accent), dpiScale);
                    dc.DrawText(autoLabelFt, new Point(_cx - autoLabelFt.Width / 2, _cy + rateFt.Height * 0.15 + unitFt.Height + 2));
                }
                else
                {
                    // No rate data yet — show "Auto" with subtitle
                    var autoFt = new FormattedText("Auto", CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, autoFace, _arcRadius * 0.32,
                        new SolidColorBrush(accent), dpiScale);
                    dc.DrawText(autoFt, new Point(_cx - autoFt.Width / 2, _cy - autoFt.Height * 0.55));

                    var subFt = new FormattedText("Hz", CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, normalFace, 14,
                        new SolidColorBrush(dimText), dpiScale);
                    dc.DrawText(subFt, new Point(_cx - subFt.Width / 2, _cy + autoFt.Height * 0.25));
                }
            }
            else
            {
                // Normal mode: Hz readout
                string hzText = items[selIdx].Replace("Hz", "").Replace("+OD", "").Trim();
                var hzFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var hzFt = new FormattedText(hzText, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, hzFace, _arcRadius * 0.38,
                    new SolidColorBrush(brightText), dpiScale);
                dc.DrawText(hzFt, new Point(_cx - hzFt.Width / 2, _cy - hzFt.Height * 0.7));

                // "Hz" unit below the number
                var unitFt = new FormattedText("Hz", CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    14, new SolidColorBrush(dimText), dpiScale);
                dc.DrawText(unitFt, new Point(_cx - unitFt.Width / 2, _cy + hzFt.Height * 0.15));
            }
        }

        /// <summary>
        /// Maps item index to angle on the arc.
        /// Index 0 = StartAngle (bottom-left), last index = EndAngle (bottom-right).
        /// Angles are in math convention (0=right, CCW positive) for Cos/Sin.
        /// </summary>
        private static double GetAngleForIndex(int index, int count)
        {
            if (count <= 1) return 90; // top center
            // We go from 135° (bottom-left in math coords) CCW to 45° (bottom-right)
            // through 90° (top). In math coords: 135 -> 90 -> 45
            // That's a sweep of -270° starting from 135° going through 180, 270, 0, 45
            // Actually let's think in math coordinates where 0=right, CCW positive:
            // Start at bottom-left = 225° math = 135° on unit circle...
            //
            // Let's use: start = 225° (lower-left), end = -45° (=315° lower-right)
            // going CCW: 225 -> 270 (top) -> 315
            // No wait, for standard arc: start bottom-left, go through left, top, right, to bottom-right
            // Math coords: start=225° going CCW means decreasing angle: 225 -> 180 -> 90 -> 0 -> 315
            // That's not CCW. Let me think again.
            //
            // In math coords (0=right, CCW+):
            // Bottom-left = 225°, Top = 90°, Bottom-right = 315° (or -45°)
            // Going from bottom-left CW through top to bottom-right:
            // 225° -> 180° -> 90° -> 0° -> 315°
            // That's decreasing from 225 to -45 (=315), total sweep = 270°

            double startMath = 225.0; // bottom-left
            double t = (double)index / (count - 1);
            double angle = startMath - t * 270.0; // sweep 270° clockwise in math coords
            return angle;
        }

        private static void DrawArc(DrawingContext dc, double cx, double cy, double radius,
            double startAngleDeg, double sweepAngleDeg, double thickness, Color color)
        {
            // Convert from "gauge convention" (StartAngle=225 CW) to math convention for drawing
            // startAngleDeg is in gauge convention where 225° start means bottom-left
            // We need to convert to the drawing system where we use Cos/Sin

            // In our drawing: Y increases downward, so we flip Y.
            // Math angle -> screen: Point(cx + r*cos(a), cy - r*sin(a)) for math-convention angle a
            // But for the arc geometry, we need screen-space angles where 0=right, CW positive (since Y is down)

            // Screen angle = -math angle (since Y is flipped)
            // Math start = 225° -> screen start = -225° = 135°
            // We sweep -270° in math -> +270° in screen (CW)

            double screenStart = -startAngleDeg;
            double screenSweep = sweepAngleDeg; // CW in screen space

            double startRad = screenStart * Math.PI / 180;
            double endRad = (screenStart + screenSweep) * Math.PI / 180;

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

        /// <summary>
        /// Convert mouse position to angle relative to arc center, then snap to nearest item.
        /// </summary>
        private void SnapToAngle(Point pos)
        {
            var items = Items;
            if (items == null || items.Length == 0) return;

            // Get angle from center to mouse in math coords (0=right, CCW positive)
            double dx = pos.X - _cx;
            double dy = -(pos.Y - _cy); // flip Y for math coords
            double mouseAngle = Math.Atan2(dy, dx) * 180 / Math.PI;

            // Find nearest item by angular distance
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
