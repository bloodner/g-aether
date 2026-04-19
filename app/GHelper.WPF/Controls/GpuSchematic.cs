using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    public class GpuSchematic : FrameworkElement
    {
        public static readonly DependencyProperty ActiveModeProperty =
            DependencyProperty.Register(nameof(ActiveMode), typeof(int), typeof(GpuSchematic),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowRestartProperty =
            DependencyProperty.Register(nameof(ShowRestart), typeof(bool), typeof(GpuSchematic),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(GpuSchematic),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public int ActiveMode
        {
            get => (int)GetValue(ActiveModeProperty);
            set => SetValue(ActiveModeProperty, value);
        }

        public bool ShowRestart
        {
            get => (bool)GetValue(ShowRestartProperty);
            set => SetValue(ShowRestartProperty, value);
        }

        public Color AccentColor
        {
            get => (Color)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 420 : availableSize.Width;
            double h = double.IsInfinity(availableSize.Height) ? 260 : Math.Min(availableSize.Height, 300);
            return new Size(w, Math.Max(200, h));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Always draw with the system accent — active vs idle is conveyed by
            // segment brightness (dim grey vs accent), not by recoloring every mode.
            Color accent = AccentColor;
            Color dimLine = Color.FromArgb(35, 255, 255, 255);
            Color dimText = Color.FromArgb(90, 200, 200, 200);
            Color activeText = Color.FromRgb(240, 240, 240);

            double nodeSize = 58;
            double halfNode = nodeSize / 2.0;

            // Layout:
            //   iGPU (top-left)
            //                      MUX (center) ----> Screen (right)
            //   dGPU (bottom-left)

            double padX = 20;
            double padY = halfNode + 14; // room for labels below nodes
            double midY = h / 2.0;

            // Column positions
            double col1 = padX + halfNode;                    // iGPU / dGPU center X
            double col2 = w * 0.45;                           // MUX center X
            double col3 = w - padX - halfNode;                // Screen center X

            // Row positions — spread iGPU/dGPU to fill available height
            double iGpuY = padY;                  // iGPU near top
            double dGpuY = h - padY;              // dGPU near bottom
            double muxY = midY;                   // MUX centered
            double screenY = midY;                // Screen centered

            string iGpuIcon = "\uE950";
            string dGpuIcon = "\uE836";
            string muxIcon = "\uE8AB";
            string screenIcon = "\uE7F4";

            // Mode logic:
            // 0=Eco: iGPU -> Screen (bypass MUX), dGPU off
            // 1=Standard: dGPU -> MUX -> Screen, iGPU idle
            // 2=Ultimate: dGPU -> Screen (bypass MUX), iGPU off
            // 3=Optimized: iGPU+dGPU -> MUX -> Screen (auto)
            bool iGpuActive, dGpuActive, muxActive;
            switch (ActiveMode)
            {
                case 0: iGpuActive = true; dGpuActive = false; muxActive = false; break;
                case 1: iGpuActive = false; dGpuActive = true; muxActive = true; break;
                case 2: iGpuActive = false; dGpuActive = true; muxActive = false; break;
                default: iGpuActive = true; dGpuActive = true; muxActive = true; break;
            }

            double lineW = 2.0;
            double glowW = 8.0;

            // --- T-junction layout ---
            // Vertical trunk: iGPU <-> dGPU (through col1 center)
            // Horizontal branch: trunk midpoint -> MUX -> Screen
            double trunkX = col1;
            double trunkTop = iGpuY + halfNode;      // bottom edge of iGPU node
            double trunkBot = dGpuY - halfNode;      // top edge of dGPU node
            double junctionY = midY;                  // midpoint of trunk
            double branchLeft = col1;                 // junction X
            double branchRight = col2 - halfNode;     // left edge of MUX node

            // --- Draw connections ---

            if (ActiveMode == 0) // Eco: iGPU -> Screen bypass, trunk & MUX dim
            {
                // Trunk dim (full)
                DrawSegment(dc, trunkX, trunkTop, trunkX, trunkBot, false, accent, dimLine, lineW, glowW);
                // Branch to MUX dim
                DrawSegment(dc, branchLeft, junctionY, branchRight, junctionY, false, accent, dimLine, lineW, glowW);
                // MUX -> Screen dim
                DrawSegment(dc, col2 + halfNode, muxY, col3 - halfNode, screenY, false, accent, dimLine, lineW, glowW);

                // Bypass: iGPU right edge -> horizontal to Screen X -> down to Screen top
                DrawBypassL(dc, col1 + halfNode, iGpuY, col3, screenY - halfNode, accent, lineW, glowW);
            }
            else if (ActiveMode == 1) // Standard: dGPU -> MUX -> Screen
            {
                // Upper trunk dim (iGPU to junction)
                DrawSegment(dc, trunkX, trunkTop, trunkX, junctionY, false, accent, dimLine, lineW, glowW);
                // Lower trunk active (dGPU to junction)
                DrawSegment(dc, trunkX, trunkBot, trunkX, junctionY, true, accent, dimLine, lineW, glowW);
                // Branch to MUX active
                DrawSegmentWithArrow(dc, branchLeft, junctionY, branchRight, junctionY, true, accent, dimLine, lineW, glowW);
                // MUX -> Screen active
                DrawSegmentWithArrow(dc, col2 + halfNode, muxY, col3 - halfNode, screenY, true, accent, dimLine, lineW, glowW);
            }
            else if (ActiveMode == 2) // Ultimate: dGPU -> Screen bypass, trunk & MUX dim
            {
                // Trunk dim (full)
                DrawSegment(dc, trunkX, trunkTop, trunkX, trunkBot, false, accent, dimLine, lineW, glowW);
                // Branch to MUX dim
                DrawSegment(dc, branchLeft, junctionY, branchRight, junctionY, false, accent, dimLine, lineW, glowW);
                // MUX -> Screen dim
                DrawSegment(dc, col2 + halfNode, muxY, col3 - halfNode, screenY, false, accent, dimLine, lineW, glowW);

                // Bypass: dGPU right edge -> horizontal to Screen X -> up to Screen bottom
                DrawBypassL(dc, col1 + halfNode, dGpuY, col3, screenY + halfNode, accent, lineW, glowW);
            }
            else // Optimized (3): both -> MUX -> Screen
            {
                // Full trunk active
                DrawSegment(dc, trunkX, trunkTop, trunkX, trunkBot, true, accent, dimLine, lineW, glowW);
                // Branch to MUX active
                DrawSegmentWithArrow(dc, branchLeft, junctionY, branchRight, junctionY, true, accent, dimLine, lineW, glowW);
                // MUX -> Screen active
                DrawSegmentWithArrow(dc, col2 + halfNode, muxY, col3 - halfNode, screenY, true, accent, dimLine, lineW, glowW);

                // AUTO badge is shown as subtitle on MUX node
            }

            // --- Draw nodes ---
            string? muxSubtitle = ActiveMode == 3 ? "AUTO" : null;
            DrawIconNode(dc, col1, iGpuY, nodeSize, iGpuIcon, "iGPU", iGpuActive, accent, dimLine, dimText, activeText, dpiScale);
            DrawIconNode(dc, col1, dGpuY, nodeSize, dGpuIcon, "dGPU", dGpuActive, accent, dimLine, dimText, activeText, dpiScale);
            DrawIconNode(dc, col2, muxY, nodeSize, muxIcon, "MUX", muxActive, accent, dimLine, dimText, activeText, dpiScale, subtitle: muxSubtitle);
            DrawIconNode(dc, col3, screenY, nodeSize, screenIcon, "Screen", true, accent, dimLine, dimText, activeText, dpiScale);
        }

        private void DrawIconNode(DrawingContext dc, double cx, double cy, double size,
            string icon, string label, bool active, Color accent, Color dimLine, Color dimText, Color activeText, double dpiScale,
            string? subtitle = null)
        {
            double half = size / 2.0;
            var rect = new Rect(cx - half, cy - half, size, size);
            double radius = 10;

            Color fill = active
                ? Color.FromArgb(25, accent.R, accent.G, accent.B)
                : Color.FromArgb(10, 128, 128, 128);
            Color border = active ? Color.FromArgb(140, accent.R, accent.G, accent.B) : dimLine;

            dc.DrawRoundedRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(border), 1.2), rect, radius, radius);

            if (active)
            {
                var glowRect = new Rect(cx - half - 3, cy - half - 3, size + 6, size + 6);
                Color glowColor = Color.FromArgb(20, accent.R, accent.G, accent.B);
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(glowColor), 4), glowRect, radius + 2, radius + 2);
            }

            // Icon and label both inside the box
            var iconFace = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Color iconColor = active ? accent : dimText;
            var iconFt = new FormattedText(icon, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, iconFace, 22, new SolidColorBrush(iconColor), dpiScale);

            var labelFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            Color labelColor = active ? activeText : dimText;
            var labelFt = new FormattedText(label, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, labelFace, 10, new SolidColorBrush(labelColor), dpiScale);

            // Stack icon + label vertically inside the node
            double totalH = iconFt.Height + 2 + labelFt.Height;
            double startY = cy - totalH / 2;
            dc.DrawText(iconFt, new Point(cx - iconFt.Width / 2, startY));
            dc.DrawText(labelFt, new Point(cx - labelFt.Width / 2, startY + iconFt.Height + 2));

            // Optional subtitle (e.g. "AUTO" on MUX) — below the box
            if (subtitle != null)
            {
                var subFace = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var subFt = new FormattedText(subtitle, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, subFace, 9, new SolidColorBrush(accent), dpiScale);
                dc.DrawText(subFt, new Point(cx - subFt.Width / 2, cy + half + 4));
            }
        }

        /// <summary>Simple segment (no arrow).</summary>
        private void DrawSegment(DrawingContext dc, double x1, double y1, double x2, double y2,
            bool active, Color accent, Color dimColor, double lineW, double glowW)
        {
            Color lineColor = active ? accent : dimColor;
            var p1 = new Point(x1, y1);
            var p2 = new Point(x2, y2);

            if (active)
            {
                Color glow = Color.FromArgb(25, accent.R, accent.G, accent.B);
                dc.DrawLine(MakePen(glow, glowW), p1, p2);
            }

            dc.DrawLine(MakePen(lineColor, lineW), p1, p2);
        }

        /// <summary>Segment with arrow at endpoint.</summary>
        private void DrawSegmentWithArrow(DrawingContext dc, double x1, double y1, double x2, double y2,
            bool active, Color accent, Color dimColor, double lineW, double glowW)
        {
            DrawSegment(dc, x1, y1, x2, y2, active, accent, dimColor, lineW, glowW);

            Color lineColor = active ? accent : dimColor;
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double ux = dx / len;
            double uy = dy / len;
            double px = -uy;
            double py = ux;
            double a = 6;
            var pen = MakePen(lineColor, lineW);
            dc.DrawLine(pen, new Point(x2, y2), new Point(x2 - ux * a + px * a * 0.5, y2 - uy * a + py * a * 0.5));
            dc.DrawLine(pen, new Point(x2, y2), new Point(x2 - ux * a - px * a * 0.5, y2 - uy * a - py * a * 0.5));
        }

        /// <summary>L-shaped bypass: horizontal from source at y1, then vertical to target at y2.</summary>
        private void DrawBypassL(DrawingContext dc, double x1, double y1, double x2, double y2,
            Color accent, double lineW, double glowW)
        {
            // Two segments: horizontal (x1,y1) -> (x2,y1), then vertical (x2,y1) -> (x2,y2)
            var corner = new Point(x2, y1);
            var pts = new[] { new Point(x1, y1), corner, new Point(x2, y2) };

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                ctx.LineTo(pts[1], true, false);
                ctx.LineTo(pts[2], true, false);
            }
            geo.Freeze();

            Color glow = Color.FromArgb(25, accent.R, accent.G, accent.B);
            dc.DrawGeometry(null, MakePen(glow, glowW), geo);
            dc.DrawGeometry(null, MakePen(accent, lineW), geo);

            // Arrow pointing into the Screen node
            double a = 5;
            var arrowPen = MakePen(accent, lineW);
            if (y2 > y1) // arrow pointing down
            {
                dc.DrawLine(arrowPen, pts[2], new Point(x2 - a * 0.6, y2 - a));
                dc.DrawLine(arrowPen, pts[2], new Point(x2 + a * 0.6, y2 - a));
            }
            else // arrow pointing up
            {
                dc.DrawLine(arrowPen, pts[2], new Point(x2 - a * 0.6, y2 + a));
                dc.DrawLine(arrowPen, pts[2], new Point(x2 + a * 0.6, y2 + a));
            }
        }

        private static Pen MakePen(Color c, double width)
        {
            return new Pen(new SolidColorBrush(c), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        }
    }
}
