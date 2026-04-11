using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public static class GraphicsExtensions
    {
        public static void DrawCircle(this Graphics g, Pen pen,
                                      float centerX, float centerY, float radius)
        {
            g.DrawEllipse(pen, centerX - radius, centerY - radius,
                          radius + radius, radius + radius);
        }

        public static void FillCircle(this Graphics g, Brush brush,
                                      float centerX, float centerY, float radius)
        {
            g.FillEllipse(brush, centerX - radius, centerY - radius,
                          radius + radius, radius + radius);
        }
    }

    public class Slider : Control
    {
        private float _radius;
        private PointF _thumbPos;
        private SizeF _barSize;
        private PointF _barPos;


        public Color accentColor = Color.FromArgb(255, 58, 174, 239);
        public Color borderColor = Color.White;

        public event EventHandler? ValueChanged;

        public Slider()
        {
            // This reduces flicker
            DoubleBuffered = true;
            TabStop = true;
        }


        private int _min = 0;
        public int Min
        {
            get => _min;
            set
            {
                _min = value;
                RecalculateParameters();
            }
        }

        private int _max = 100;
        public int Max
        {
            get => _max;
            set
            {
                _max = value;
                RecalculateParameters();
            }
        }

        // TrackBar compatibility aliases
        public int Minimum { get => Min; set => Min = value; }
        public int Maximum { get => Max; set => Max = value; }

        private int _step = 1;
        public int Step
        {
            get => _step;
            set
            {
                _step = value;
            }
        }
        private int _value = 50;
        public int Value
        {
            get => _value;
            set
            {

                value = (int)Math.Round(value / (float)_step) * _step;

                if (_value != value)
                {
                    _value = value;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    RecalculateParameters();
                }
            }
        }


        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Right:
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                    return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {

            switch (e.KeyCode)
            {
                case Keys.Right:
                case Keys.Up:
                    Value = Math.Min(Max, Value + Step);
                    break;
                case Keys.Left:
                case Keys.Down:
                    Value = Math.Max(Min, Value - Step);
                    break;
            }

            AccessibilityNotifyClients(AccessibleEvents.Focus, 0);

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using Brush brushAccent = new SolidBrush(accentColor);
            using Brush brushEmpty = new SolidBrush(Color.FromArgb(100, 128, 128, 128));
            using Brush brushWhite = new SolidBrush(RForm.foreMain);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Thin rounded rail
            float barRadius = _barSize.Height / 2;
            var emptyRect = new RectangleF(_barPos.X, _barPos.Y, _barSize.Width, _barSize.Height);
            var filledRect = new RectangleF(_barPos.X, _barPos.Y, Math.Max(0, _thumbPos.X - _barPos.X), _barSize.Height);

            using var emptyPath = CreateRoundedBarPath(emptyRect, barRadius);
            e.Graphics.FillPath(brushEmpty, emptyPath);

            if (filledRect.Width > 0)
            {
                using var filledPath = CreateRoundedBarPath(filledRect, barRadius);
                e.Graphics.FillPath(brushAccent, filledPath);
            }

            // Thumb: accent outer, white inner (WinUI 3 style)
            e.Graphics.FillCircle(brushAccent, _thumbPos.X, _thumbPos.Y, _radius);
            e.Graphics.FillCircle(brushWhite, _thumbPos.X, _thumbPos.Y, 0.55f * _radius);
        }

        private static GraphicsPath CreateRoundedBarPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;
            if (diameter > rect.Height) diameter = rect.Height;
            if (diameter > rect.Width) diameter = rect.Width;
            var arcRect = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            path.AddArc(arcRect, 180, 90);
            arcRect.X = rect.Right - diameter;
            path.AddArc(arcRect, 270, 90);
            arcRect.Y = rect.Bottom - diameter;
            path.AddArc(arcRect, 0, 90);
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateParameters();
        }

        private void RecalculateParameters()
        {
            _radius = 0.35F * ClientSize.Height;
            float barHeight = Math.Max(4, ClientSize.Height * 0.12F);
            _barSize = new SizeF(ClientSize.Width - 2 * _radius, barHeight);
            _barPos = new PointF(_radius, (ClientSize.Height - _barSize.Height) / 2);
            float range = Max - Min;
            _thumbPos = new PointF(
                range > 0 ? _barSize.Width / range * (Value - Min) + _barPos.X : _barPos.X,
                _barPos.Y + 0.5f * _barSize.Height);
            Invalidate();
        }

        bool _moving = false;
        SizeF _delta;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus();

            // Difference between tumb and mouse position.
            _delta = new SizeF(e.Location.X - _thumbPos.X, e.Location.Y - _thumbPos.Y);
            if (_delta.Width * _delta.Width + _delta.Height * _delta.Height <= _radius * _radius)
            {
                // Clicking inside thumb.
                _moving = true;
            }

            _calculateValue(e);

        }

        private void _calculateValue(MouseEventArgs e)
        {
            float thumbX = e.Location.X;
            if (thumbX < _barPos.X)
            {
                thumbX = _barPos.X;
            }
            else if (thumbX > _barPos.X + _barSize.Width)
            {
                thumbX = _barPos.X + _barSize.Width;
            }
            Value = (int)Math.Round(Min + (thumbX - _barPos.X) * (Max - Min) / _barSize.Width);

        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_moving)
            {
                _calculateValue(e);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _moving = false;
        }

    }

}