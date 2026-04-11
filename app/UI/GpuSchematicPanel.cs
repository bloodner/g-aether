using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class GpuSchematicPanel : Control
    {
        private int _activeMode = 1; // 0=Eco, 1=Standard, 2=Ultimate, 3=Optimized

        public int ActiveMode
        {
            get => _activeMode;
            set { _activeMode = value; Invalidate(); }
        }

        public GpuSchematicPanel()
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

            float dpi = 1f; // Use fixed sizing; control is DPI-scaled by WinForms
            float w = ClientSize.Width;
            float h = ClientSize.Height;
            float pad = 16;

            // Layout positions (5 nodes: iGPU, dGPU, MUX, dGPU-direct, Screen)
            float nodeW = 56;
            float nodeH = 40;
            float iconSize = 24;

            // Vertical centers for iGPU (top) and dGPU (bottom)
            float topY = pad;
            float bottomY = h - pad - nodeH;
            float midY = h / 2f - nodeH / 2f;

            // Horizontal positions
            float col1 = pad; // iGPU / dGPU column
            float col2 = w * 0.38f; // dGPU label column
            float col3 = w * 0.55f; // MUX switch column
            float col4 = w - pad - nodeW; // Screen column

            Color activeColor = RForm.colorStandard;
            Color dimColor = Color.FromArgb(60, 128, 128, 128);
            Color glowColor = Color.FromArgb(30, activeColor.R, activeColor.G, activeColor.B);

            // Determine which paths are active
            bool iGpuToMux = _activeMode == 1 || _activeMode == 3; // Standard, Optimized
            bool dGpuToMux = _activeMode == 1 || _activeMode == 2 || _activeMode == 3; // Standard, Ultimate, Optimized
            bool muxToScreen = true;
            bool iGpuDirect = _activeMode == 0; // Eco
            bool dGpuDirect = _activeMode == 2; // Ultimate (bypass MUX)

            // Draw connecting lines first (behind nodes)
            float lineWidth = 2.5f * dpi;
            float glowWidth = 8f * dpi;

            // iGPU node center
            float iCx = col1 + nodeW / 2;
            float iCy = topY + nodeH / 2;

            // dGPU node center
            float dCx = col2 + nodeW / 2;
            float dCy = bottomY + nodeH / 2;

            // MUX center
            float mCx = col3 + nodeW / 2;
            float mCy = midY + nodeH / 2;

            // Screen center
            float sCx = col4 + nodeW / 2;
            float sCy = midY + nodeH / 2;

            // Line: iGPU → MUX
            DrawConnection(g, iCx + nodeW / 2, iCy, mCx - nodeW / 2, mCy,
                iGpuToMux ? activeColor : dimColor, iGpuToMux ? glowColor : Color.Empty, lineWidth, glowWidth);

            // Line: dGPU → MUX
            DrawConnection(g, dCx + nodeW / 2, dCy, mCx - nodeW / 2, mCy,
                dGpuToMux ? activeColor : dimColor, dGpuToMux ? glowColor : Color.Empty, lineWidth, glowWidth);

            // Line: MUX → Screen
            DrawConnection(g, mCx + nodeW / 2, mCy, sCx - nodeW / 2, sCy,
                muxToScreen ? activeColor : dimColor, muxToScreen ? glowColor : Color.Empty, lineWidth, glowWidth);

            // Eco direct: iGPU → Screen (curved top)
            if (iGpuDirect)
            {
                using var glowPen = new Pen(glowColor, glowWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var pen = new Pen(activeColor, lineWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float arcY = topY - 10 * dpi;
                PointF[] curve = { new(iCx + nodeW / 2, iCy), new(w / 2, arcY), new(sCx - nodeW / 2, sCy) };
                if (glowColor != Color.Empty) g.DrawCurve(glowPen, curve, 0.5f);
                g.DrawCurve(pen, curve, 0.5f);
            }

            // Ultimate direct: dGPU → Screen (curved bottom)
            if (dGpuDirect)
            {
                using var glowPen = new Pen(glowColor, glowWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var pen = new Pen(activeColor, lineWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float arcYb = bottomY + nodeH + 10 * dpi;
                PointF[] curve = { new(dCx + nodeW / 2, dCy), new(w * 0.65f, arcYb), new(sCx - nodeW / 2, sCy) };
                if (glowColor != Color.Empty) g.DrawCurve(glowPen, curve, 0.5f);
                g.DrawCurve(pen, curve, 0.5f);
            }

            // Draw nodes
            DrawNode(g, col1, topY, nodeW, nodeH, "iGPU", "(Internal)", iGpuDirect || iGpuToMux, activeColor, dimColor, dpi);
            DrawNode(g, col2, bottomY, nodeW, nodeH, "dGPU", "(Nvidia)", dGpuToMux || dGpuDirect, activeColor, dimColor, dpi);
            DrawMuxNode(g, col3, midY, nodeW, nodeH, muxToScreen, activeColor, dimColor, dpi);
            DrawScreenNode(g, col4, midY, nodeW, nodeH, true, activeColor, dimColor, dpi);

            // "Requires Restart" label
            if (_activeMode == 2)
            {
                using var font = new Font("Segoe UI", 7.5f * dpi, FontStyle.Italic);
                using var brush = new SolidBrush(Color.FromArgb(150, RForm.foreMain.R, RForm.foreMain.G, RForm.foreMain.B));
                var text = "Requires Restart";
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, brush, w - pad - sz.Width, pad / 2);
            }
        }

        private void DrawConnection(Graphics g, float x1, float y1, float x2, float y2,
            Color color, Color glow, float width, float glowWidth)
        {
            if (glow != Color.Empty)
            {
                using var glowPen = new Pen(glow, glowWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(glowPen, x1, y1, x2, y2);
            }
            using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, x1, y1, x2, y2);
        }

        private static void DrawNode(Graphics g, float x, float y, float w, float h,
            string label, string sublabel, bool active, Color activeColor, Color dimColor, float dpi)
        {
            var rect = new RectangleF(x, y, w, h);
            float r = 8 * dpi;
            using var path = CreateRoundedRect(rect, r);

            Color fill = active ? Color.FromArgb(40, activeColor.R, activeColor.G, activeColor.B) : Color.FromArgb(20, 128, 128, 128);
            Color border = active ? activeColor : dimColor;

            using var fillBrush = new SolidBrush(fill);
            using var borderPen = new Pen(border, 1.5f * dpi);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);

            using var font = new Font("Segoe UI", 9f * dpi, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 7f * dpi, FontStyle.Regular);
            using var textBrush = new SolidBrush(active ? activeColor : Color.FromArgb(150, 200, 200, 200));

            var ls = g.MeasureString(label, font);
            g.DrawString(label, font, textBrush, x + w / 2 - ls.Width / 2, y + h / 2 - ls.Height);

            var ss = g.MeasureString(sublabel, subFont);
            using var dimBrush = new SolidBrush(Color.FromArgb(100, 180, 180, 180));
            g.DrawString(sublabel, subFont, dimBrush, x + w / 2 - ss.Width / 2, y + h / 2 + 2);
        }

        private static void DrawMuxNode(Graphics g, float x, float y, float w, float h,
            bool active, Color activeColor, Color dimColor, float dpi)
        {
            // Diamond/switch shape
            float cx = x + w / 2;
            float cy = y + h / 2;
            float hw = w * 0.45f;
            float hh = h * 0.45f;
            PointF[] diamond = {
                new(cx, cy - hh), new(cx + hw, cy),
                new(cx, cy + hh), new(cx - hw, cy)
            };

            Color fill = active ? Color.FromArgb(30, activeColor.R, activeColor.G, activeColor.B) : Color.FromArgb(15, 128, 128, 128);
            Color border = active ? activeColor : dimColor;

            using var fillBrush = new SolidBrush(fill);
            using var borderPen = new Pen(border, 1.5f * dpi);
            g.FillPolygon(fillBrush, diamond);
            g.DrawPolygon(borderPen, diamond);

            // Switch icon (two arrows)
            using var font = new Font("Segoe UI", 7f * dpi, FontStyle.Regular);
            using var textBrush = new SolidBrush(active ? activeColor : Color.FromArgb(120, 180, 180, 180));
            var text = "MUX";
            var sz = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush, cx - sz.Width / 2, cy - sz.Height / 2);

        }

        private static void DrawScreenNode(Graphics g, float x, float y, float w, float h,
            bool active, Color activeColor, Color dimColor, float dpi)
        {
            float cx = x + w / 2;
            float cy = y + h / 2;

            // Monitor shape
            float mw = w * 0.7f;
            float mh = h * 0.55f;
            var monRect = new RectangleF(cx - mw / 2, cy - mh / 2 - 4 * dpi, mw, mh);

            Color border = active ? activeColor : dimColor;
            using var borderPen = new Pen(border, 1.5f * dpi);
            using var fillBrush = new SolidBrush(Color.FromArgb(20, 128, 128, 128));

            float r = 4 * dpi;
            using var path = CreateRoundedRect(monRect, r);
            g.FillPath(fillBrush, path);
            g.DrawPath(borderPen, path);

            // Stand
            float standX = cx;
            float standTop = monRect.Bottom;
            float standBottom = standTop + 8 * dpi;
            g.DrawLine(borderPen, standX, standTop, standX, standBottom);
            g.DrawLine(borderPen, standX - 8 * dpi, standBottom, standX + 8 * dpi, standBottom);

            // Label
            using var font = new Font("Segoe UI", 7.5f * dpi, FontStyle.Regular);
            using var textBrush = new SolidBrush(active ? activeColor : Color.FromArgb(120, 180, 180, 180));
            var label = "Screen";
            var sz = g.MeasureString(label, font);
            g.DrawString(label, font, textBrush, cx - sz.Width / 2, standBottom + 2);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
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
