using System.Drawing.Drawing2D;

namespace GHelper.UI
{
    public class CardPanel : Panel
    {
        private int _cornerRadius = 8;
        private bool _collapsed = false;
        private Panel? _titlePanel;
        private Label? _chevron;
        private readonly HashSet<Control> _hiddenBeforeCollapse = new();

        public bool Collapsible { get; set; } = false;

        public bool Collapsed
        {
            get => _collapsed;
            set { if (value) Collapse(); else Expand(); }
        }

        public event Action<bool>? CollapseStateChanged;

        public CardPanel()
        {
            DoubleBuffered = true;
        }

        public void SetTitlePanel(Panel title)
        {
            _titlePanel = title;
            title.Cursor = Cursors.Hand;
            title.Click += (s, e) => Toggle();
            foreach (Control c in title.Controls)
            {
                // Don't attach collapse toggle to interactive controls
                if (c is ToggleSwitch) continue;
                c.Click += (s, e) => Toggle();
            }
        }

        public void SetChevron(Label chevron)
        {
            _chevron = chevron;
            chevron.Click += (s, e) => Toggle();
        }

        private void Toggle()
        {
            if (!Collapsible) return;
            if (_collapsed) Expand(); else Collapse();
        }

        private void Collapse()
        {
            if (_collapsed) return;
            _collapsed = true;
            if (_chevron != null) _chevron.Text = "\u25B6";

            _hiddenBeforeCollapse.Clear();
            SuspendLayout();
            foreach (Control c in Controls)
            {
                if (c == _titlePanel) continue;
                if (!c.Visible) _hiddenBeforeCollapse.Add(c);
                c.Visible = false;
            }
            ResumeLayout(true);

            CollapseStateChanged?.Invoke(true);
        }

        private void Expand()
        {
            if (!_collapsed) return;
            _collapsed = false;
            if (_chevron != null) _chevron.Text = "\u25BC";

            SuspendLayout();
            foreach (Control c in Controls)
            {
                if (_hiddenBeforeCollapse.Contains(c)) continue;
                c.Visible = true;
            }
            _hiddenBeforeCollapse.Clear();
            ResumeLayout(true);

            CollapseStateChanged?.Invoke(false);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(RForm.formBack);
        }

        // Visual gap between stacked cards (painted as formBack at bottom)
        private const int CardGap = 6;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1 - CardGap);
            using var path = RComboBox.RoundedRect(rect, _cornerRadius, _cornerRadius);
            using var brush = new SolidBrush(BackColor);
            using var pen = new Pen(Color.FromArgb(20, 255, 255, 255), 1);

            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

    }
}
