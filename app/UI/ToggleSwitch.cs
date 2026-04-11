using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class ToggleSwitch : Control
    {
        private bool _checked;
        private string _label = "";

        public event EventHandler? CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public string Label
        {
            get => _label;
            set { _label = value; Invalidate(); }
        }

        public ToggleSwitch()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float trackW = 40;
            float trackH = 20;
            float thumbR = 7;
            float trackRadius = trackH / 2f;

            // Label text
            float labelX = 0;
            float textWidth = 0;
            if (!string.IsNullOrEmpty(_label))
            {
                using var font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
                using var brush = new SolidBrush(RForm.foreMain);
                var sz = g.MeasureString(_label, font);
                textWidth = sz.Width + 8;
                float textY = (ClientSize.Height - sz.Height) / 2f;
                g.DrawString(_label, font, brush, labelX, textY);
            }

            // Track position
            float trackX = textWidth;
            float trackY = (ClientSize.Height - trackH) / 2f;
            var trackRect = new RectangleF(trackX, trackY, trackW, trackH);

            // Track background
            Color trackColor = _checked ? RForm.colorStandard : Color.FromArgb(80, 128, 128, 128);
            using var trackPath = CreatePillPath(trackRect, trackRadius);
            using var trackBrush = new SolidBrush(trackColor);
            g.FillPath(trackBrush, trackPath);

            // Border
            if (!_checked)
            {
                using var borderPen = new Pen(Color.FromArgb(100, 160, 160, 160), 1f);
                g.DrawPath(borderPen, trackPath);
            }

            // Thumb
            float thumbX = _checked ? trackX + trackW - trackRadius : trackX + trackRadius;
            float thumbY = trackY + trackH / 2f;
            using var thumbBrush = new SolidBrush(Color.White);
            g.FillCircle(thumbBrush, thumbX, thumbY, thumbR);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Checked = !Checked;
        }

        private static GraphicsPath CreatePillPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d > rect.Height) d = rect.Height;
            var arc = new RectangleF(rect.X, rect.Y, d, d);
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
