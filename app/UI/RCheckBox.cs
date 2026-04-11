using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class RCheckBox : CheckBox
    {
        public RCheckBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            float dpiScale = e.Graphics.DpiX / 96.0f;
            int boxSize = (int)(18 * dpiScale);
            int boxY = (Height - boxSize) / 2;
            int boxX = Padding.Left + (int)(4 * dpiScale);
            int cornerRadius = (int)(4 * dpiScale);

            var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            if (Checked)
            {
                // Filled rounded square with accent color
                using var fillBrush = new SolidBrush(RForm.colorStandard);
                using var path = RComboBox.RoundedRect(boxRect, cornerRadius, cornerRadius);
                e.Graphics.FillPath(fillBrush, path);

                // White checkmark
                using var checkPen = new Pen(Color.White, 2f * dpiScale);
                checkPen.StartCap = LineCap.Round;
                checkPen.EndCap = LineCap.Round;
                checkPen.LineJoin = LineJoin.Round;

                float cx = boxX + boxSize * 0.28f;
                float cy = boxY + boxSize * 0.52f;
                float mx = boxX + boxSize * 0.44f;
                float my = boxY + boxSize * 0.7f;
                float ex = boxX + boxSize * 0.74f;
                float ey = boxY + boxSize * 0.32f;

                e.Graphics.DrawLines(checkPen, new PointF[]
                {
                    new(cx, cy), new(mx, my), new(ex, ey)
                });
            }
            else
            {
                // Empty rounded square with border
                using var borderPen = new Pen(RForm.borderMain, 1.5f * dpiScale);
                using var path = RComboBox.RoundedRect(boxRect, cornerRadius, cornerRadius);
                e.Graphics.DrawPath(borderPen, path);
            }

            // Draw text
            int textX = boxX + boxSize + (int)(8 * dpiScale);
            var textRect = new Rectangle(textX, 0, Width - textX - Padding.Right, Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }
    }
}
