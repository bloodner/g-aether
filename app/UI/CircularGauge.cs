using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class CircularGauge : Control
    {
        private int _value = 100;
        private string _healthText = "";
        private string _cyclesText = "";
        private string _bottomLabel = "";

        public int Value
        {
            get => _value;
            set { _value = Math.Clamp(value, 0, 100); Invalidate(); }
        }

        public string HealthText
        {
            get => _healthText;
            set { _healthText = value; Invalidate(); }
        }

        public string CyclesText
        {
            get => _cyclesText;
            set { _cyclesText = value; Invalidate(); }
        }

        public string BottomLabel
        {
            get => _bottomLabel;
            set { _bottomLabel = value; Invalidate(); }
        }

        public CircularGauge()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float dpi = DeviceDpi / 96f;
            float pad = 8 * dpi;
            float size = Math.Min(ClientSize.Width, ClientSize.Height) - pad * 2;
            if (size <= 0) return;

            float cx = ClientSize.Width / 2f;
            float infoHeight = 0;
            if (!string.IsNullOrEmpty(_healthText) || !string.IsNullOrEmpty(_cyclesText))
                infoHeight = 36 * dpi;

            float arcDiameter = Math.Min(size, ClientSize.Height - infoHeight - pad);
            float cy = pad + arcDiameter / 2f;

            float penWidth = Math.Max(6 * dpi, arcDiameter * 0.08f);
            float arcRadius = (arcDiameter - penWidth) / 2f;
            var arcRect = new RectangleF(cx - arcRadius, cy - arcRadius, arcRadius * 2, arcRadius * 2);

            // Start at bottom-left (135 deg), sweep 270 degrees
            float startAngle = 135f;
            float sweepAngle = 270f;
            float fillSweep = sweepAngle * _value / 100f;

            // Track (background arc)
            using var trackPen = new Pen(Color.FromArgb(60, 128, 128, 128), penWidth);
            trackPen.StartCap = LineCap.Round;
            trackPen.EndCap = LineCap.Round;
            g.DrawArc(trackPen, arcRect, startAngle, sweepAngle);

            // Fill arc
            Color fillColor = GetFillColor();
            using var fillPen = new Pen(fillColor, penWidth);
            fillPen.StartCap = LineCap.Round;
            fillPen.EndCap = LineCap.Round;
            if (fillSweep > 0.5f)
                g.DrawArc(fillPen, arcRect, startAngle, fillSweep);

            // Glow effect on fill end
            if (fillSweep > 1f)
            {
                using var glowPen = new Pen(Color.FromArgb(40, fillColor.R, fillColor.G, fillColor.B), penWidth * 2.5f);
                glowPen.StartCap = LineCap.Round;
                glowPen.EndCap = LineCap.Round;
                float glowStart = startAngle + fillSweep - 10;
                g.DrawArc(glowPen, arcRect, glowStart, 10);
            }

            // Percentage text centered
            string pctText = $"{_value}%";
            using var bigFont = new Font("Segoe UI", arcRadius * 0.45f, FontStyle.Bold);
            using var textBrush = new SolidBrush(RForm.foreMain);
            var pctSize = g.MeasureString(pctText, bigFont);
            g.DrawString(pctText, bigFont, textBrush, cx - pctSize.Width / 2, cy - pctSize.Height / 2);

            // Health and Cycles text below the arc
            float infoY = cy + arcRadius + penWidth / 2 + 4 * dpi;
            using var smallFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            using var dimBrush = new SolidBrush(Color.FromArgb(180, RForm.foreMain.R, RForm.foreMain.G, RForm.foreMain.B));

            if (!string.IsNullOrEmpty(_healthText))
            {
                var hs = g.MeasureString(_healthText, smallFont);
                g.DrawString(_healthText, smallFont, dimBrush, cx - hs.Width / 2, infoY);
                infoY += hs.Height + 2;
            }
            if (!string.IsNullOrEmpty(_cyclesText))
            {
                var cs = g.MeasureString(_cyclesText, smallFont);
                g.DrawString(_cyclesText, smallFont, dimBrush, cx - cs.Width / 2, infoY);
            }
        }

        private Color GetFillColor()
        {
            if (_value > 60) return RForm.colorStandard; // Blue
            if (_value > 30) return Color.FromArgb(255, 255, 180, 0); // Amber
            return RForm.colorTurbo; // Red
        }
    }
}
