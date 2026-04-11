using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GHelper.WPF.Controls
{
    public class FanCurveEditor : FrameworkElement
    {
        private static readonly Typeface NormalFont = new(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface BoldFont = new(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly SolidColorBrush DimBrush;
        private static readonly Pen GridPen;
        private static readonly Pen BorderPen;

        static FanCurveEditor()
        {
            DimBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            DimBrush.Freeze();
            var gridBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            gridBrush.Freeze();
            GridPen = new Pen(gridBrush, 1);
            GridPen.Freeze();
            var borderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            borderBrush.Freeze();
            BorderPen = new Pen(borderBrush, 1);
            BorderPen.Freeze();
        }

        // 8 points: temps and fan speeds (0-100%)
        public static readonly DependencyProperty TempsProperty =
            DependencyProperty.Register(nameof(Temps), typeof(byte[]), typeof(FanCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpeedsProperty =
            DependencyProperty.Register(nameof(Speeds), typeof(byte[]), typeof(FanCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeColorProperty =
            DependencyProperty.Register(nameof(StrokeColor), typeof(Color), typeof(FanCurveEditor),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(FanCurveEditor),
                new FrameworkPropertyMetadata("Fan Curve", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AnimateColorProperty =
            DependencyProperty.Register(nameof(AnimateColor), typeof(bool), typeof(FanCurveEditor),
                new PropertyMetadata(false, OnAnimateColorChanged));

        public static readonly DependencyProperty BaseAccentColorProperty =
            DependencyProperty.Register(nameof(BaseAccentColor), typeof(Color), typeof(FanCurveEditor),
                new PropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF)));

        public static readonly DependencyProperty HueOffsetProperty =
            DependencyProperty.Register(nameof(HueOffset), typeof(double), typeof(FanCurveEditor),
                new PropertyMetadata(0.0));

        public byte[] Temps { get => (byte[])GetValue(TempsProperty); set => SetValue(TempsProperty, value); }
        public byte[] Speeds { get => (byte[])GetValue(SpeedsProperty); set => SetValue(SpeedsProperty, value); }
        public Color StrokeColor { get => (Color)GetValue(StrokeColorProperty); set => SetValue(StrokeColorProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public bool AnimateColor { get => (bool)GetValue(AnimateColorProperty); set => SetValue(AnimateColorProperty, value); }
        public Color BaseAccentColor { get => (Color)GetValue(BaseAccentColorProperty); set => SetValue(BaseAccentColorProperty, value); }
        public double HueOffset { get => (double)GetValue(HueOffsetProperty); set => SetValue(HueOffsetProperty, value); }

        public event EventHandler? CurveChanged;

        private int _dragIndex = -1;
        private int _focusIndex = -1;
        private const double DotRadius = 6;

        // Chart area margins
        private const double MarginLeft = 35;
        private const double MarginRight = 10;
        private const double MarginTop = 24;
        private const double MarginBottom = 24;

        // Animation
        private DispatcherTimer? _animTimer;
        private double _animPhase;

        public FanCurveEditor()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            IsVisibleChanged += OnVisibilityChanged;
        }

        private static void OnAnimateColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FanCurveEditor editor)
            {
                if ((bool)e.NewValue)
                    editor.StartColorAnimation();
                else
                    editor.StopColorAnimation();
            }
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                if (AnimateColor && _animTimer == null)
                    StartColorAnimation();
            }
            else
            {
                StopColorAnimation();
            }
        }

        private void StartColorAnimation()
        {
            if (_animTimer != null || !IsVisible) return;
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _animTimer.Tick += (s, e) =>
            {
                _animPhase += 0.008; // slow cycle ~31s full loop
                if (_animPhase > 1.0) _animPhase -= 1.0;

                // Shift hue ±35 degrees around the base accent color
                var baseColor = BaseAccentColor;
                ColorToHSL(baseColor, out double h, out double sat, out double lum);

                double hueShift = Math.Sin(_animPhase * 2 * Math.PI + HueOffset) * 35.0;
                double newHue = (h + hueShift + 360) % 360;

                // Slightly vary saturation and lightness for more life
                double satVar = sat + Math.Sin(_animPhase * 4 * Math.PI) * 0.08;
                double lumVar = lum + Math.Cos(_animPhase * 3 * Math.PI) * 0.04;
                satVar = Math.Clamp(satVar, 0.3, 1.0);
                lumVar = Math.Clamp(lumVar, 0.35, 0.7);

                StrokeColor = HSLToColor(newHue, satVar, lumVar);
            };
            _animTimer.Start();
        }

        private void StopColorAnimation()
        {
            _animTimer?.Stop();
            _animTimer = null;
        }

        private static void ColorToHSL(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;

            if (max == min)
            {
                h = s = 0;
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) * 60;
                else if (max == g) h = ((b - r) / d + 2) * 60;
                else h = ((r - g) / d + 4) * 60;
            }
        }

        private static Color HSLToColor(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromRgb(
                (byte)Math.Clamp((r + m) * 255, 0, 255),
                (byte)Math.Clamp((g + m) * 255, 0, 255),
                (byte)Math.Clamp((b + m) * 255, 0, 255));
        }

        private Rect ChartRect => new(
            MarginLeft, MarginTop,
            Math.Max(1, ActualWidth - MarginLeft - MarginRight),
            Math.Max(1, ActualHeight - MarginTop - MarginBottom));

        private Point TempSpeedToPoint(byte temp, byte speed)
        {
            var r = ChartRect;
            double x = r.Left + (temp / 100.0) * r.Width;
            double y = r.Bottom - (speed / 100.0) * r.Height;
            return new Point(x, y);
        }

        private (byte temp, byte speed) PointToTempSpeed(Point pt)
        {
            var r = ChartRect;
            double t = Math.Clamp((pt.X - r.Left) / r.Width * 100, 0, 100);
            double s = Math.Clamp((r.Bottom - pt.Y) / r.Height * 100, 0, 100);
            return ((byte)t, (byte)s);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var temps = Temps;
            var speeds = Speeds;
            if (temps == null || speeds == null || temps.Length != 8) return;

            var pos = e.GetPosition(this);
            for (int i = 0; i < 8; i++)
            {
                var pt = TempSpeedToPoint(temps[i], speeds[i]);
                if ((pos - pt).Length < DotRadius * 2.5)
                {
                    _dragIndex = i;
                    CaptureMouse();
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragIndex < 0) return;

            var temps = Temps;
            var speeds = Speeds;
            if (temps == null || speeds == null) return;

            var (temp, speed) = PointToTempSpeed(e.GetPosition(this));

            // Clamp to neighbors
            byte minT = _dragIndex > 0 ? (byte)(temps[_dragIndex - 1] + 1) : (byte)0;
            byte maxT = _dragIndex < 7 ? (byte)(temps[_dragIndex + 1] - 1) : (byte)100;
            temp = (byte)Math.Clamp(temp, minT, maxT);

            temps[_dragIndex] = temp;
            speeds[_dragIndex] = speed;

            // Force re-render by creating new arrays
            Temps = (byte[])temps.Clone();
            Speeds = (byte[])speeds.Clone();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                ReleaseMouseCapture();
                CurveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var temps = Temps;
            var speeds = Speeds;
            if (temps == null || speeds == null || temps.Length != 8) return;

            // Tab/Left/Right to move between points
            if (e.Key == Key.Tab || e.Key == Key.Left || e.Key == Key.Right)
            {
                if (_focusIndex < 0) _focusIndex = 0;
                else if (e.Key == Key.Left || (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
                    _focusIndex = Math.Max(0, _focusIndex - 1);
                else
                    _focusIndex = Math.Min(7, _focusIndex + 1);
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Up/Down to adjust speed of focused point
            if (_focusIndex >= 0 && (e.Key == Key.Up || e.Key == Key.Down))
            {
                int step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5 : 1;
                byte newSpeed = e.Key == Key.Up
                    ? (byte)Math.Min(100, speeds[_focusIndex] + step)
                    : (byte)Math.Max(0, speeds[_focusIndex] - step);
                speeds[_focusIndex] = newSpeed;
                Temps = (byte[])temps.Clone();
                Speeds = (byte[])speeds.Clone();
                CurveChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            if (_focusIndex < 0) _focusIndex = 0;
            InvalidateVisual();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            _focusIndex = -1;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var r = ChartRect;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Title
            var titleFt = new FormattedText(Label, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, BoldFont, 11,
                new SolidColorBrush(Color.FromArgb(200, 240, 240, 240)), dpi);
            dc.DrawText(titleFt, new Point(MarginLeft, 2));

            // Grid lines (25%, 50%, 75%)
            for (int pct = 25; pct <= 75; pct += 25)
            {
                double y = r.Bottom - (pct / 100.0) * r.Height;
                dc.DrawLine(GridPen, new Point(r.Left, y), new Point(r.Right, y));

                var labelFt = new FormattedText($"{pct}%", CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, NormalFont, 8, DimBrush, dpi);
                dc.DrawText(labelFt, new Point(r.Left - labelFt.Width - 4, y - labelFt.Height / 2));
            }

            // Temperature axis labels
            for (int t = 20; t <= 80; t += 20)
            {
                double x = r.Left + (t / 100.0) * r.Width;
                dc.DrawLine(GridPen, new Point(x, r.Top), new Point(x, r.Bottom));

                var labelFt = new FormattedText($"{t}°", CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, NormalFont, 8, DimBrush, dpi);
                dc.DrawText(labelFt, new Point(x - labelFt.Width / 2, r.Bottom + 4));
            }

            // Border
            dc.DrawRectangle(null, BorderPen, r);

            var temps = Temps;
            var speeds = Speeds;
            if (temps == null || speeds == null || temps.Length != 8 || speeds.Length != 8) return;

            // Draw curve with fill
            var lineGeo = new StreamGeometry();
            var fillGeo = new StreamGeometry();

            using (var ctx = lineGeo.Open())
            {
                var first = TempSpeedToPoint(temps[0], speeds[0]);
                ctx.BeginFigure(first, false, false);
                for (int i = 1; i < 8; i++)
                    ctx.LineTo(TempSpeedToPoint(temps[i], speeds[i]), true, true);
            }
            lineGeo.Freeze();

            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(TempSpeedToPoint(temps[0], speeds[0]).X, r.Bottom), true, true);
                for (int i = 0; i < 8; i++)
                    ctx.LineTo(TempSpeedToPoint(temps[i], speeds[i]), true, true);
                ctx.LineTo(new Point(TempSpeedToPoint(temps[7], speeds[7]).X, r.Bottom), true, true);
            }
            fillGeo.Freeze();

            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(40, StrokeColor.R, StrokeColor.G, StrokeColor.B),
                Color.FromArgb(5, StrokeColor.R, StrokeColor.G, StrokeColor.B),
                new Point(0, 0), new Point(0, 1));
            dc.DrawGeometry(fillBrush, null, fillGeo);

            var linePen = new Pen(new SolidColorBrush(StrokeColor), 2)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, linePen, lineGeo);

            // Draw draggable dots
            var dotFill = new SolidColorBrush(StrokeColor);
            var dotGlow = new SolidColorBrush(Color.FromArgb(50, StrokeColor.R, StrokeColor.G, StrokeColor.B));
            for (int i = 0; i < 8; i++)
            {
                var pt = TempSpeedToPoint(temps[i], speeds[i]);
                bool dragging = i == _dragIndex;
                dc.DrawEllipse(dotGlow, null, pt, dragging ? 10 : 7, dragging ? 10 : 7);
                dc.DrawEllipse(dotFill, null, pt, DotRadius, DotRadius);
                dc.DrawEllipse(Brushes.White, null, pt, 2.5, 2.5);
            }

            // Focus indicator for keyboard navigation
            if (_focusIndex >= 0 && _focusIndex < 8 && IsFocused)
            {
                var focusPt = TempSpeedToPoint(temps[_focusIndex], speeds[_focusIndex]);
                var focusPen = new Pen(Brushes.White, 1.5) { DashStyle = DashStyles.Dash };
                dc.DrawEllipse(null, focusPen, focusPt, DotRadius + 4, DotRadius + 4);
            }
        }
    }
}
