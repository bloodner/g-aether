using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// Hybrid color picker: slim hue spectrum strip inline with color preview circles.
    /// Expandable section reveals saturation/brightness sliders, preset swatches, and hex.
    /// </summary>
    public class ColorWheel : FrameworkElement
    {
        public static readonly DependencyProperty Color1Property =
            DependencyProperty.Register(nameof(Color1), typeof(Color), typeof(ColorWheel),
                new FrameworkPropertyMetadata(Colors.White,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnColorPropertyChanged));

        public static readonly DependencyProperty Color2Property =
            DependencyProperty.Register(nameof(Color2), typeof(Color), typeof(ColorWheel),
                new FrameworkPropertyMetadata(Colors.Black,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnColorPropertyChanged));

        public static readonly DependencyProperty ShowSecondaryProperty =
            DependencyProperty.Register(nameof(ShowSecondary), typeof(bool), typeof(ColorWheel),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public Color Color1
        {
            get => (Color)GetValue(Color1Property);
            set => SetValue(Color1Property, value);
        }

        public Color Color2
        {
            get => (Color)GetValue(Color2Property);
            set => SetValue(Color2Property, value);
        }

        public bool ShowSecondary
        {
            get => (bool)GetValue(ShowSecondaryProperty);
            set => SetValue(ShowSecondaryProperty, value);
        }

        public event EventHandler? Color1Changed;
        public event EventHandler? Color2Changed;

        // Layout
        private const double BarHeight = 14;
        private const double BarRadius = 7;
        private const double ThumbRadius = 8;
        private const double ThumbBorder = 2.5;
        private const double PreviewDot = 24;
        private const double PreviewGap = 6;
        private const double PresetDot = 14;
        private const double PresetGap = 4;
        private const double ExpandBtnSize = 24;
        private const double SectionGap = 10;
        private const double InlineHeight = 30; // collapsed row height

        // Presets
        private static readonly Color[] Presets =
        [
            Colors.White,
            Color.FromRgb(255, 0, 0),
            Color.FromRgb(255, 100, 0),
            Color.FromRgb(255, 200, 0),
            Color.FromRgb(0, 255, 0),
            Color.FromRgb(0, 255, 160),
            Color.FromRgb(0, 200, 255),
            Color.FromRgb(0, 100, 255),
            Color.FromRgb(80, 0, 255),
            Color.FromRgb(160, 0, 255),
            Color.FromRgb(255, 0, 200),
            Color.FromRgb(255, 0, 100),
        ];

        // State
        private bool _expanded;
        private bool _editingColor2;
        private double _hue1, _sat1 = 1, _bri1 = 1;
        private double _hue2, _sat2 = 1, _bri2;
        private bool _suppressEvents;

        private enum DragTarget { None, Hue, Saturation, Brightness }
        private DragTarget _dragging = DragTarget.None;

        // Hit rects
        private Rect _hueBarRect;
        private Rect _satBarRect;
        private Rect _briBarRect;
        private Rect _preview1Rect;
        private Rect _preview2Rect;
        private Rect _expandBtnRect;
        private Rect[] _presetRects = [];
        private int _hoverPreset = -1;
        private bool _hoverExpand;

        private static readonly Typeface TextFace = new(
            new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface BoldFace = new(
            new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface IconFace = new(
            new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public ColorWheel()
        {
            Cursor = Cursors.Hand;
            ClipToBounds = false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 380 : availableSize.Width;
            double h = InlineHeight;
            if (_expanded)
            {
                h += SectionGap + BarHeight + ThumbRadius; // saturation
                h += SectionGap + BarHeight + ThumbRadius; // brightness
                h += SectionGap + PresetDot + 4; // presets row
            }
            return new Size(w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double w = ActualWidth;

            double hue = _editingColor2 ? _hue2 : _hue1;
            double sat = _editingColor2 ? _sat2 : _sat1;
            double bri = _editingColor2 ? _bri2 : _bri1;
            Color activeColor = _editingColor2 ? Color2 : Color1;

            // === INLINE ROW ===
            // [●1] [●2] [===== hue strip =====] [▼]
            double x = 0;
            double cy = InlineHeight / 2;

            // Preview dot 1
            double pr = PreviewDot / 2;
            _preview1Rect = new Rect(x, cy - pr, PreviewDot, PreviewDot);
            Point p1c = new(x + pr, cy);
            dc.DrawEllipse(new SolidColorBrush(Color1), null, p1c, pr, pr);
            if (!_editingColor2)
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)), 2), p1c, pr + 1.5, pr + 1.5);
            else
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1), p1c, pr, pr);
            x += PreviewDot + PreviewGap;

            // Preview dot 2 (if dual-color)
            if (ShowSecondary)
            {
                _preview2Rect = new Rect(x, cy - pr, PreviewDot, PreviewDot);
                Point p2c = new(x + pr, cy);
                dc.DrawEllipse(new SolidColorBrush(Color2), null, p2c, pr, pr);
                if (_editingColor2)
                    dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)), 2), p2c, pr + 1.5, pr + 1.5);
                else
                    dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1), p2c, pr, pr);
                x += PreviewDot + PreviewGap;
            }

            // Expand button (right side)
            double btnX = w - ExpandBtnSize;
            _expandBtnRect = new Rect(btnX, cy - ExpandBtnSize / 2, ExpandBtnSize, ExpandBtnSize);

            var btnBg = new SolidColorBrush(_hoverExpand
                ? Color.FromArgb(40, 255, 255, 255)
                : Color.FromArgb(15, 255, 255, 255));
            double btnR = ExpandBtnSize / 2;
            dc.DrawEllipse(btnBg, null, new Point(btnX + btnR, cy), btnR, btnR);

            // Chevron icon (E70D = ChevronDown, E70E = ChevronUp)
            string chevron = _expanded ? "\uE70E" : "\uE70D";
            var chevronFt = new FormattedText(chevron, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, IconFace, 10,
                new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), dpi);
            dc.DrawText(chevronFt, new Point(
                btnX + (ExpandBtnSize - chevronFt.Width) / 2,
                cy - chevronFt.Height / 2));

            // Hue strip (between previews and expand button)
            double hueLeft = x + 6;
            double hueRight = btnX - 8;
            double hueWidth = hueRight - hueLeft;
            if (hueWidth > 40)
            {
                _hueBarRect = new Rect(hueLeft, cy - BarHeight / 2, hueWidth, BarHeight);
                DrawHueBar(dc, _hueBarRect, hue);
            }

            // === EXPANDED SECTION ===
            if (_expanded)
            {
                double ey = InlineHeight + SectionGap;
                double barLeft = 0;
                double barWidth = w;

                // Saturation bar
                _satBarRect = new Rect(barLeft, ey, barWidth, BarHeight);
                DrawGradientBar(dc, _satBarRect, HsvToRgb(hue, 0, bri > 0 ? bri : 1), HsvToRgb(hue, 1, bri > 0 ? bri : 1), sat);
                DrawBarLabel(dc, dpi, "Saturation", barLeft, ey);

                ey += BarHeight + ThumbRadius + SectionGap;

                // Brightness bar
                _briBarRect = new Rect(barLeft, ey, barWidth, BarHeight);
                DrawGradientBar(dc, _briBarRect, Colors.Black, HsvToRgb(hue, sat, 1), bri);
                DrawBarLabel(dc, dpi, "Brightness", barLeft, ey);

                ey += BarHeight + ThumbRadius + SectionGap;

                // Preset row + hex
                _presetRects = new Rect[Presets.Length];
                double totalPresetsW = Presets.Length * PresetDot + (Presets.Length - 1) * PresetGap;
                double presetStartX = 0;

                for (int i = 0; i < Presets.Length; i++)
                {
                    double px = presetStartX + i * (PresetDot + PresetGap);
                    _presetRects[i] = new Rect(px, ey, PresetDot, PresetDot);
                    double pcx = px + PresetDot / 2;
                    double pcy = ey + PresetDot / 2;
                    double pRad = PresetDot / 2;

                    dc.DrawEllipse(new SolidColorBrush(Presets[i]), null,
                        new Point(pcx, pcy), pRad, pRad);

                    bool selected = ColorsMatch(Presets[i], activeColor);
                    bool hovered = _hoverPreset == i;

                    if (selected)
                        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)), 1.5),
                            new Point(pcx, pcy), pRad + 2, pRad + 2);
                    else if (hovered)
                        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 1),
                            new Point(pcx, pcy), pRad + 1, pRad + 1);

                    if (Presets[i].R > 230 && Presets[i].G > 230 && Presets[i].B > 230 && !selected)
                        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1),
                            new Point(pcx, pcy), pRad, pRad);
                }

                // Hex value (right-aligned on preset row)
                string hex = $"#{activeColor.R:X2}{activeColor.G:X2}{activeColor.B:X2}";
                var hexFt = new FormattedText(hex, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, TextFace, 10,
                    new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), dpi);
                dc.DrawText(hexFt, new Point(w - hexFt.Width, ey + (PresetDot - hexFt.Height) / 2));
            }
            else
            {
                _satBarRect = default;
                _briBarRect = default;
                _presetRects = [];
            }
        }

        private void DrawHueBar(DrawingContext dc, Rect rect, double hue)
        {
            var geo = CreatePill(rect, BarRadius);

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            for (int i = 0; i <= 7; i++)
            {
                double h = (360.0 * i) / 7;
                grad.GradientStops.Add(new GradientStop(HsvToRgb(h, 1, 1), (double)i / 7));
            }
            grad.Freeze();

            dc.PushClip(geo);
            dc.DrawRectangle(grad, null, rect);
            dc.Pop();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1), geo);

            double thumbX = rect.Left + (hue / 360.0) * rect.Width;
            thumbX = Math.Clamp(thumbX, rect.Left, rect.Right);
            double thumbY = rect.Top + rect.Height / 2;
            DrawThumb(dc, thumbX, thumbY, HsvToRgb(hue, 1, 1));
        }

        private void DrawGradientBar(DrawingContext dc, Rect rect, Color left, Color right, double value)
        {
            var geo = CreatePill(rect, BarRadius);

            var grad = new LinearGradientBrush(left, right, new Point(0, 0.5), new Point(1, 0.5));
            grad.Freeze();

            dc.PushClip(geo);
            dc.DrawRectangle(grad, null, rect);
            dc.Pop();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1), geo);

            double thumbX = rect.Left + value * rect.Width;
            thumbX = Math.Clamp(thumbX, rect.Left, rect.Right);
            double thumbY = rect.Top + rect.Height / 2;

            // Interpolate thumb color
            Color thumbColor = Color.FromRgb(
                (byte)(left.R + (right.R - left.R) * value),
                (byte)(left.G + (right.G - left.G) * value),
                (byte)(left.B + (right.B - left.B) * value));
            DrawThumb(dc, thumbX, thumbY, thumbColor);
        }

        private static void DrawBarLabel(DrawingContext dc, double dpi, string text, double barLeft, double barY)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, TextFace, 9,
                new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), dpi);
            dc.DrawText(ft, new Point(barLeft + 2, barY - ft.Height - 2));
        }

        private static void DrawThumb(DrawingContext dc, double x, double y, Color fill)
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), null,
                new Point(x, y + 1), ThumbRadius + 1, ThumbRadius + 1);
            dc.DrawEllipse(null, new Pen(Brushes.White, ThumbBorder),
                new Point(x, y), ThumbRadius, ThumbRadius);
            dc.DrawEllipse(new SolidColorBrush(fill), null,
                new Point(x, y), ThumbRadius - ThumbBorder + 0.5, ThumbRadius - ThumbBorder + 0.5);
        }

        // --- Input ---

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            CaptureMouse();
            var pos = e.GetPosition(this);

            // Expand/collapse button
            if (IsInCircle(pos, _expandBtnRect))
            {
                _expanded = !_expanded;
                InvalidateMeasure();
                InvalidateVisual();
                return;
            }

            // Preview clicks (switch active color)
            if (IsInCircle(pos, _preview1Rect) && _editingColor2)
            {
                _editingColor2 = false;
                InvalidateVisual();
                return;
            }
            if (ShowSecondary && IsInCircle(pos, _preview2Rect) && !_editingColor2)
            {
                _editingColor2 = true;
                InvalidateVisual();
                return;
            }

            // Preset clicks
            for (int i = 0; i < _presetRects.Length; i++)
            {
                if (IsInCircle(pos, _presetRects[i]))
                {
                    ApplyColor(Presets[i]);
                    return;
                }
            }

            // Bar drags
            if (HitBar(pos, _hueBarRect))
            {
                _dragging = DragTarget.Hue;
                UpdateFromDrag(pos);
                return;
            }
            if (_expanded && HitBar(pos, _satBarRect))
            {
                _dragging = DragTarget.Saturation;
                UpdateFromDrag(pos);
                return;
            }
            if (_expanded && HitBar(pos, _briBarRect))
            {
                _dragging = DragTarget.Brightness;
                UpdateFromDrag(pos);
                return;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);

            if (_dragging != DragTarget.None)
            {
                UpdateFromDrag(pos);
                return;
            }

            // Hover states
            bool newHoverExpand = IsInCircle(pos, _expandBtnRect);
            int newHoverPreset = -1;
            for (int i = 0; i < _presetRects.Length; i++)
            {
                if (IsInCircle(pos, _presetRects[i]))
                {
                    newHoverPreset = i;
                    break;
                }
            }

            if (newHoverExpand != _hoverExpand || newHoverPreset != _hoverPreset)
            {
                _hoverExpand = newHoverExpand;
                _hoverPreset = newHoverPreset;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = DragTarget.None;
            ReleaseMouseCapture();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverExpand || _hoverPreset >= 0)
            {
                _hoverExpand = false;
                _hoverPreset = -1;
                InvalidateVisual();
            }
        }

        private void UpdateFromDrag(Point pos)
        {
            ref double hue = ref (_editingColor2 ? ref _hue2 : ref _hue1);
            ref double sat = ref (_editingColor2 ? ref _sat2 : ref _sat1);
            ref double bri = ref (_editingColor2 ? ref _bri2 : ref _bri1);

            Rect bar = _dragging switch
            {
                DragTarget.Hue => _hueBarRect,
                DragTarget.Saturation => _satBarRect,
                DragTarget.Brightness => _briBarRect,
                _ => default,
            };
            if (bar.Width <= 0) return;

            double t = (pos.X - bar.Left) / bar.Width;
            t = Math.Clamp(t, 0, 1);

            switch (_dragging)
            {
                case DragTarget.Hue: hue = t * 360; break;
                case DragTarget.Saturation: sat = t; break;
                case DragTarget.Brightness: bri = t; break;
            }

            Color result = HsvToRgb(hue, sat, bri);
            if (_editingColor2)
            {
                Color2 = result;
                Color2Changed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Color1 = result;
                Color1Changed?.Invoke(this, EventArgs.Empty);
            }
            InvalidateVisual();
        }

        private void ApplyColor(Color c)
        {
            if (_editingColor2)
            {
                Color2 = c;
                RgbToHsv(c, out _hue2, out _sat2, out _bri2);
                Color2Changed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Color1 = c;
                RgbToHsv(c, out _hue1, out _sat1, out _bri1);
                Color1Changed?.Invoke(this, EventArgs.Empty);
            }
            InvalidateVisual();
        }

        private static bool HitBar(Point pos, Rect bar)
        {
            if (bar.Width <= 0) return false;
            double expand = ThumbRadius;
            return pos.X >= bar.Left - expand && pos.X <= bar.Right + expand &&
                   pos.Y >= bar.Top - expand && pos.Y <= bar.Bottom + expand;
        }

        private static bool IsInCircle(Point pt, Rect bounds)
        {
            if (bounds.Width == 0) return false;
            double cx = bounds.Left + bounds.Width / 2;
            double cy = bounds.Top + bounds.Height / 2;
            double r = bounds.Width / 2 + 3;
            return (pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy) <= r * r;
        }

        private static bool ColorsMatch(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) < 15 && Math.Abs(a.G - b.G) < 15 && Math.Abs(a.B - b.B) < 15;
        }

        private static void OnColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorWheel cw && !cw._suppressEvents)
            {
                cw._suppressEvents = true;
                if (e.Property == Color1Property)
                    RgbToHsv(cw.Color1, out cw._hue1, out cw._sat1, out cw._bri1);
                else if (e.Property == Color2Property)
                    RgbToHsv(cw.Color2, out cw._hue2, out cw._sat2, out cw._bri2);
                cw._suppressEvents = false;
            }
        }

        // --- Color conversion ---

        internal static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        internal static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            v = max;
            s = max > 0 ? delta / max : 0;
            if (delta == 0) { h = 0; }
            else if (max == r) { h = 60 * (((g - b) / delta) % 6); }
            else if (max == g) { h = 60 * ((b - r) / delta + 2); }
            else { h = 60 * ((r - g) / delta + 4); }
            if (h < 0) h += 360;
        }

        private static Geometry CreatePill(Rect rect, double r)
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
