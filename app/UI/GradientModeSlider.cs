using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class GradientModeSlider : Control
    {
        private int _selectedIndex = 0;
        private int _modeCount = 4;
        private Color[] _gradientStops = { };
        private string[] _labels = { };
        private float _thumbRadius;
        private float _barHeight;
        private RectangleF _barRect;

        public event EventHandler? SelectedIndexChanged;

        public Color[] GradientStops
        {
            get => _gradientStops;
            set { _gradientStops = value; Invalidate(); }
        }

        public string[] Labels
        {
            get => _labels;
            set { _labels = value; _modeCount = value.Length; Recalculate(); }
        }

        public int ModeCount
        {
            get => _modeCount;
            set { _modeCount = Math.Max(2, value); Recalculate(); }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                value = Math.Clamp(value, 0, _modeCount - 1);
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public GradientModeSlider()
        {
            DoubleBuffered = true;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Recalculate();
        }

        private void Recalculate()
        {
            float dpi = DeviceDpi / 96f;
            _thumbRadius = Math.Max(8, ClientSize.Height * 0.22f);
            _barHeight = Math.Max(6 * dpi, ClientSize.Height * 0.16f);
            float labelSpace = _labels.Length > 0 ? ClientSize.Height * 0.38f : 0;
            float barY = (ClientSize.Height - labelSpace - _barHeight) / 2f;
            _barRect = new RectangleF(_thumbRadius, barY, ClientSize.Width - 2 * _thumbRadius, _barHeight);
            Invalidate();
        }

        private float GetSnapX(int index)
        {
            if (_modeCount <= 1) return _barRect.Left;
            return _barRect.Left + _barRect.Width * index / (_modeCount - 1);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Draw gradient rail
            if (_barRect.Width > 0 && _barRect.Height > 0)
            {
                float barRadius = _barHeight / 2f;
                using var barPath = CreateRoundedBarPath(_barRect, barRadius);

                if (_gradientStops.Length >= 2)
                {
                    using var gradBrush = new LinearGradientBrush(
                        new PointF(_barRect.Left, 0), new PointF(_barRect.Right, 0),
                        _gradientStops[0], _gradientStops[^1]);
                    var blend = new ColorBlend(_gradientStops.Length);
                    for (int i = 0; i < _gradientStops.Length; i++)
                    {
                        blend.Colors[i] = _gradientStops[i];
                        blend.Positions[i] = (float)i / (_gradientStops.Length - 1);
                    }
                    gradBrush.InterpolationColors = blend;
                    g.FillPath(gradBrush, barPath);
                }
                else
                {
                    using var brush = new SolidBrush(Color.FromArgb(100, 128, 128, 128));
                    g.FillPath(brush, barPath);
                }
            }

            // Draw snap point dots
            using var dotBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
            for (int i = 0; i < _modeCount; i++)
            {
                if (i == _selectedIndex) continue;
                float cx = GetSnapX(i);
                float cy = _barRect.Top + _barRect.Height / 2f;
                g.FillCircle(dotBrush, cx, cy, 3f);
            }

            // Draw thumb
            float thumbX = GetSnapX(_selectedIndex);
            float thumbY = _barRect.Top + _barRect.Height / 2f;

            // Glow behind thumb
            using var glowBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
            g.FillCircle(glowBrush, thumbX, thumbY, _thumbRadius * 1.3f);

            // Outer accent circle
            Color thumbColor = GetColorAtPosition(_selectedIndex);
            using var accentBrush = new SolidBrush(thumbColor);
            g.FillCircle(accentBrush, thumbX, thumbY, _thumbRadius);

            // Inner white circle
            using var innerBrush = new SolidBrush(RForm.foreMain);
            g.FillCircle(innerBrush, thumbX, thumbY, _thumbRadius * 0.55f);

            // Draw labels below bar
            if (_labels.Length > 0)
            {
                using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                using var labelBrush = new SolidBrush(RForm.foreMain);
                using var selectedBrush = new SolidBrush(thumbColor);
                float labelY = _barRect.Bottom + _thumbRadius + 4;

                for (int i = 0; i < Math.Min(_labels.Length, _modeCount); i++)
                {
                    float cx = GetSnapX(i);
                    var size = g.MeasureString(_labels[i], labelFont);
                    var brush = i == _selectedIndex ? selectedBrush : labelBrush;
                    float labelX = cx - size.Width / 2;
                    // Clamp labels to stay within control bounds
                    labelX = Math.Max(0, Math.Min(labelX, ClientSize.Width - size.Width));
                    g.DrawString(_labels[i], labelFont, brush, labelX, labelY);
                }
            }
        }

        private Color GetColorAtPosition(int index)
        {
            if (_gradientStops.Length == 0) return RForm.colorStandard;
            if (_gradientStops.Length == 1) return _gradientStops[0];
            float t = (float)index / Math.Max(1, _modeCount - 1);
            int segCount = _gradientStops.Length - 1;
            float segT = t * segCount;
            int seg = Math.Min((int)segT, segCount - 1);
            float lerp = segT - seg;
            return LerpColor(_gradientStops[seg], _gradientStops[Math.Min(seg + 1, segCount)], lerp);
        }

        private static Color LerpColor(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            SnapToNearest(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left)
                SnapToNearest(e.X);
        }

        private void SnapToNearest(float x)
        {
            float bestDist = float.MaxValue;
            int bestIdx = 0;
            for (int i = 0; i < _modeCount; i++)
            {
                float dist = Math.Abs(x - GetSnapX(i));
                if (dist < bestDist) { bestDist = dist; bestIdx = i; }
            }
            SelectedIndex = bestIdx;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData switch
            {
                Keys.Left or Keys.Right or Keys.Up or Keys.Down => true,
                _ => base.IsInputKey(keyData)
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Right:
                case Keys.Down:
                    SelectedIndex = Math.Min(_modeCount - 1, _selectedIndex + 1);
                    break;
                case Keys.Left:
                case Keys.Up:
                    SelectedIndex = Math.Max(0, _selectedIndex - 1);
                    break;
            }
            base.OnKeyDown(e);
        }

        private static GraphicsPath CreateRoundedBarPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d > rect.Height) d = rect.Height;
            if (d > rect.Width) d = rect.Width;
            var arc = new RectangleF(rect.Location, new SizeF(d, d));
            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - d;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - d;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
