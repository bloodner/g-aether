using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class ButtonSelector : FlowLayoutPanel
    {
        private readonly List<PillLabel> _pills = new();
        private readonly List<(object? key, string display)> _items = new();
        private int _selectedIndex = -1;
        private int _hoverIndex = -1;

        public event EventHandler? SelectedIndexChanged;
        public event EventHandler? SelectedValueChanged;

        public ButtonSelector()
        {
            DoubleBuffered = true;
            WrapContents = true;
            AutoSize = false;
            Padding = new Padding(2);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value < -1 || value >= _items.Count) return;
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                UpdatePillStates();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object? SelectedValue
        {
            get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex].key : null;
            set
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(_items[i].key, value))
                    {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        public object? SelectedItem
        {
            get => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex].display : null;
        }

        public int ItemCount => _items.Count;

        public string GetItemText(object? item)
        {
            if (item is string s) return s;
            foreach (var i in _items)
                if (Equals(i.display, item) || Equals(i.key, item)) return i.display;
            return item?.ToString() ?? "";
        }

        public string GetItemText(int index)
        {
            if (index >= 0 && index < _items.Count) return _items[index].display;
            return "";
        }

        // Simple string items
        public void AddItem(string display)
        {
            _items.Add((null, display));
            RebuildPills();
        }

        // Key/value items
        public void AddItem(object key, string display)
        {
            _items.Add((key, display));
            RebuildPills();
        }

        public void UpdateItemText(int index, string display)
        {
            if (index < 0 || index >= _items.Count) return;
            _items[index] = (_items[index].key, display);
            if (index < _pills.Count)
            {
                _pills[index].Text = display;
                using var g = _pills[index].CreateGraphics();
                var textSize = TextRenderer.MeasureText(g, display, _pills[index].Font);
                _pills[index].Size = new Size(textSize.Width + 20, textSize.Height + 10);
                _pills[index].Invalidate();
            }
        }

        public void ClearItems()
        {
            _selectedIndex = -1;
            _items.Clear();
            RebuildPills();
        }

        // Batch add from dictionary (replaces DataSource binding)
        public void SetItems(IEnumerable<KeyValuePair<int, string>> items)
        {
            _items.Clear();
            foreach (var kv in items)
                _items.Add((kv.Key, kv.Value));
            RebuildPills();
        }

        public void SetItems<TKey>(IEnumerable<KeyValuePair<TKey, string>> items) where TKey : notnull
        {
            _items.Clear();
            foreach (var kv in items)
                _items.Add((kv.Key, kv.Value));
            RebuildPills();
        }

        // Simple string list
        public void SetItems(IEnumerable<string> items)
        {
            _items.Clear();
            foreach (var s in items)
                _items.Add((null, s));
            RebuildPills();
        }

        private void RebuildPills()
        {
            SuspendLayout();
            foreach (var p in _pills) p.Dispose();
            _pills.Clear();
            Controls.Clear();

            for (int i = 0; i < _items.Count; i++)
            {
                var pill = new PillLabel(this, i, _items[i].display);
                _pills.Add(pill);
                Controls.Add(pill);
            }

            UpdatePillStates();
            ResumeLayout(true);
        }

        private void UpdatePillStates()
        {
            for (int i = 0; i < _pills.Count; i++)
            {
                _pills[i].IsSelected = (i == _selectedIndex);
                _pills[i].Invalidate();
            }
        }

        internal void OnPillClicked(int index)
        {
            SelectedIndex = index;
        }

        internal void OnPillHover(int index, bool entering)
        {
            _hoverIndex = entering ? index : -1;
        }

        internal bool IsPillHovered(int index) => _hoverIndex == index;

        private class PillLabel : Control
        {
            private readonly ButtonSelector _owner;
            private readonly int _index;
            private bool _isSelected;
            private bool _isHovered;
            private static readonly int _cornerRadius = 12;
            private static readonly int _hPad = 10;
            private static readonly int _vPad = 5;

            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Invalidate(); }
            }

            public PillLabel(ButtonSelector owner, int index, string text)
            {
                _owner = owner;
                _index = index;
                Text = text;
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
                Margin = new Padding(2);

                // Measure text and set size
                using var g = CreateGraphics();
                var textSize = TextRenderer.MeasureText(g, text, Font);
                Size = new Size(textSize.Width + _hPad * 2, textSize.Height + _vPad * 2);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _isHovered = true;
                _owner.OnPillHover(_index, true);
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _isHovered = false;
                _owner.OnPillHover(_index, false);
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnClick(EventArgs e)
            {
                _owner.OnPillClicked(_index);
                base.OnClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Clear background to parent
                e.Graphics.Clear(_owner.BackColor);

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                int radius = Math.Min(_cornerRadius, Height / 2);
                using var path = RComboBox.RoundedRect(rect, radius, radius);

                Color fillColor;
                Color textColor;

                if (_isSelected)
                {
                    fillColor = RForm.colorStandard;
                    textColor = Color.White;
                }
                else if (_isHovered)
                {
                    fillColor = Color.FromArgb(40, 255, 255, 255);
                    textColor = RForm.foreMain;
                }
                else
                {
                    fillColor = RForm.buttonMain;
                    textColor = RForm.foreMain;
                }

                using var brush = new SolidBrush(fillColor);
                e.Graphics.FillPath(brush, path);

                // Subtle border for unselected pills
                if (!_isSelected)
                {
                    using var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 1);
                    e.Graphics.DrawPath(pen, path);
                }

                // Text
                var textRect = new Rectangle(0, 0, Width, Height);
                TextRenderer.DrawText(e.Graphics, Text, Font, textRect, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }
}
