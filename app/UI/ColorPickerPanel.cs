using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class ColorPickerPanel : Control
    {
        private static readonly Color[] PresetColors = new[]
        {
            Color.White,
            Color.FromArgb(255, 0, 0),       // Red
            Color.FromArgb(255, 100, 0),      // Orange
            Color.FromArgb(255, 200, 0),      // Yellow
            Color.FromArgb(0, 255, 0),        // Green
            Color.FromArgb(0, 255, 160),      // Teal
            Color.FromArgb(0, 200, 255),      // Cyan
            Color.FromArgb(0, 100, 255),      // Blue
            Color.FromArgb(80, 0, 255),       // Indigo
            Color.FromArgb(160, 0, 255),      // Purple
            Color.FromArgb(255, 0, 200),      // Magenta
            Color.FromArgb(255, 0, 100),      // Hot Pink
        };

        private const int SwatchSize = 22;
        private const int SwatchSpacing = 4;
        private const int CustomWidth = 50;

        private Color _selectedColor = Color.White;
        private Color _selectedColor2 = Color.Black;
        private int _hoverIndex = -1;
        private bool _showSecondary = false;
        private RectangleF _customRect;

        public event EventHandler? ColorChanged;
        public event EventHandler? Color2Changed;

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor == value) return;
                _selectedColor = value;
                Invalidate();
            }
        }

        public Color SelectedColor2
        {
            get => _selectedColor2;
            set
            {
                if (_selectedColor2 == value) return;
                _selectedColor2 = value;
                Invalidate();
            }
        }

        public bool ShowSecondary
        {
            get => _showSecondary;
            set
            {
                if (_showSecondary == value) return;
                _showSecondary = value;
                Invalidate();
            }
        }

        public ColorPickerPanel()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Height = SwatchSize + 8;
        }

        private RectangleF GetSwatchRect(int index, int row)
        {
            float x = 4 + index * (SwatchSize + SwatchSpacing);
            float y = 4 + row * (SwatchSize + SwatchSpacing);
            return new RectangleF(x, y, SwatchSize, SwatchSize);
        }

        private int HitTest(Point pt, out bool isCustom, out bool isSecondaryRow)
        {
            isCustom = false;
            isSecondaryRow = false;

            // Check primary row
            for (int i = 0; i < PresetColors.Length; i++)
            {
                var rect = GetSwatchRect(i, 0);
                if (rect.Contains(pt)) return i;
            }

            // Check custom button
            if (_customRect.Contains(pt))
            {
                isCustom = true;
                return -1;
            }

            // Check secondary row
            if (_showSecondary)
            {
                for (int i = 0; i < PresetColors.Length; i++)
                {
                    var rect = GetSwatchRect(i, 1);
                    if (rect.Contains(pt))
                    {
                        isSecondaryRow = true;
                        return i;
                    }
                }
            }

            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int newHover = HitTest(e.Location, out bool isCustom, out _);
            if (isCustom) newHover = 100; // sentinel for custom
            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int index = HitTest(e.Location, out bool isCustom, out bool isSecondaryRow);

            if (isCustom)
            {
                OpenCustomPicker(isSecondaryRow);
            }
            else if (index >= 0 && index < PresetColors.Length)
            {
                if (isSecondaryRow)
                {
                    SelectedColor2 = PresetColors[index];
                    Color2Changed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    SelectedColor = PresetColors[index];
                    ColorChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            base.OnMouseClick(e);
        }

        private void OpenCustomPicker(bool secondary)
        {
            var dlg = new ColorDialog
            {
                AllowFullOpen = true,
                Color = secondary ? _selectedColor2 : _selectedColor
            };

            try
            {
                string? customColors = AppConfig.GetString("aura_color_custom", "");
                if (!string.IsNullOrEmpty(customColors))
                    dlg.CustomColors = customColors.Split('-').Select(int.Parse).ToArray();
            }
            catch { }

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                AppConfig.Set("aura_color_custom", string.Join("-", dlg.CustomColors));
                if (secondary)
                {
                    SelectedColor2 = dlg.Color;
                    Color2Changed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    SelectedColor = dlg.Color;
                    ColorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            int rows = _showSecondary ? 2 : 1;

            for (int row = 0; row < rows; row++)
            {
                Color selectedForRow = row == 0 ? _selectedColor : _selectedColor2;

                // Draw label
                string label = row == 0 ? "" : "2nd:";
                if (!string.IsNullOrEmpty(label))
                {
                    var labelRect = new RectangleF(4, 4 + row * (SwatchSize + SwatchSpacing), 30, SwatchSize);
                    TextRenderer.DrawText(e.Graphics, label, Font, Rectangle.Round(labelRect),
                        Color.FromArgb(150, RForm.foreMain),
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                }

                for (int i = 0; i < PresetColors.Length; i++)
                {
                    var rect = GetSwatchRect(i, row);
                    var color = PresetColors[i];
                    bool isSelected = ColorsMatch(color, selectedForRow);
                    bool isHovered = (_hoverIndex == i && row == 0) ||
                                     (_hoverIndex == i && row == 1);

                    float radius = SwatchSize / 2f;
                    float cx = rect.X + radius;
                    float cy = rect.Y + radius;

                    // Draw swatch circle
                    using var brush = new SolidBrush(color);
                    e.Graphics.FillEllipse(brush, rect);

                    // Selection ring
                    if (isSelected)
                    {
                        using var pen = new Pen(RForm.colorStandard, 2.5f);
                        e.Graphics.DrawEllipse(pen, rect.X - 2, rect.Y - 2,
                            rect.Width + 4, rect.Height + 4);
                    }
                    else if (isHovered)
                    {
                        using var pen = new Pen(Color.FromArgb(100, 255, 255, 255), 1.5f);
                        e.Graphics.DrawEllipse(pen, rect.X - 1, rect.Y - 1,
                            rect.Width + 2, rect.Height + 2);
                    }

                    // For white/light colors, add a subtle border so they're visible on light backgrounds
                    if (color.GetBrightness() > 0.9f && !isSelected)
                    {
                        using var borderPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
                        e.Graphics.DrawEllipse(borderPen, rect);
                    }
                }
            }

            // Draw "Custom" button after the last swatch in row 0
            float customX = 4 + PresetColors.Length * (SwatchSize + SwatchSpacing) + 4;
            _customRect = new RectangleF(customX, 4, CustomWidth, SwatchSize);

            bool customHovered = _hoverIndex == 100;
            var customFill = customHovered ? Color.FromArgb(40, 255, 255, 255) : Color.Transparent;

            using (var customBrush = new SolidBrush(customFill))
            using (var customPath = RComboBox.RoundedRect(Rectangle.Round(_customRect), SwatchSize / 2, SwatchSize / 2))
            {
                e.Graphics.FillPath(customBrush, customPath);
                using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
                e.Graphics.DrawPath(borderPen, customPath);
            }

            TextRenderer.DrawText(e.Graphics, "Custom", Font, Rectangle.Round(_customRect),
                RForm.foreMain,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Draw current selected color preview (larger, at far right)
            float previewX = _customRect.Right + 8;
            float previewSize = SwatchSize;
            var previewRect = new RectangleF(previewX, 4, previewSize, previewSize);
            using (var previewBrush = new SolidBrush(_selectedColor))
            {
                e.Graphics.FillEllipse(previewBrush, previewRect);
            }
            using (var ringPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                e.Graphics.DrawEllipse(ringPen, previewRect);
            }

            if (_showSecondary)
            {
                var preview2Rect = new RectangleF(previewX + previewSize + 4, 4, previewSize, previewSize);
                using (var previewBrush = new SolidBrush(_selectedColor2))
                {
                    e.Graphics.FillEllipse(previewBrush, preview2Rect);
                }
                using (var ringPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
                {
                    e.Graphics.DrawEllipse(ringPen, preview2Rect);
                }
            }
        }

        private static bool ColorsMatch(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) < 20 &&
                   Math.Abs(a.G - b.G) < 20 &&
                   Math.Abs(a.B - b.B) < 20;
        }
    }
}
