using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// Underline-style tab strip for in-page view switching. Inactive labels
    /// are dim, the active label is accent-colored with a 2px underline bar.
    /// Distinct from <see cref="PillSelector"/>, which means "apply a mode" —
    /// TabHeader means "switch what I'm looking at" with no side effects.
    /// </summary>
    public class TabHeader : FrameworkElement
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(TabHeader),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(TabHeader),
                new FrameworkPropertyMetadata(0,
                    FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(TabHeader),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public string[]? Items
        {
            get => (string[]?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public Color AccentColor
        {
            get => (Color)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        private readonly List<Rect> _tabRects = new();
        private int _hoverIndex = -1;

        public TabHeader()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
            return new Size(w, 38);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _tabRects.Clear();

            var items = Items;
            if (items == null || items.Length == 0) return;

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var font = new Typeface(new FontFamily("Segoe UI Variable"),
                FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double fontSize = 12.5;
            double labelPadX = 14;
            double gap = 2;
            double underlineThickness = 2;

            // Subtle baseline separator — a hairline right above the bottom edge
            // to visually group the tabs as a header row.
            var baselinePen = new Pen(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)), 1);
            double baselineY = h - 0.5;
            dc.DrawLine(baselinePen, new Point(0, baselineY), new Point(w, baselineY));

            Color activeColor = AccentColor;
            Color inactiveColor = Color.FromArgb(0xA0, 0xE6, 0xE6, 0xEE);
            Color hoverColor = Color.FromArgb(0xE8, 0xF2, 0xF2, 0xF8);

            double x = 0;
            for (int i = 0; i < items.Length; i++)
            {
                bool selected = i == SelectedIndex;
                bool hovered = i == _hoverIndex && !selected;

                Color textColor = selected ? activeColor
                                 : hovered ? hoverColor
                                 : inactiveColor;

                var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, font, fontSize, new SolidColorBrush(textColor), dpi);

                double tabW = ft.Width + labelPadX * 2;
                var hitRect = new Rect(x, 0, tabW, h);
                _tabRects.Add(hitRect);

                double textX = x + labelPadX;
                double textY = (h - ft.Height) / 2 - 1;
                dc.DrawText(ft, new Point(textX, textY));

                if (selected)
                {
                    // Underline bar — slightly inset from the text so it hugs
                    // the label instead of stretching across the padding.
                    double inset = labelPadX * 0.35;
                    var underline = new Rect(
                        x + inset,
                        h - underlineThickness - 1,
                        tabW - inset * 2,
                        underlineThickness);
                    dc.DrawRoundedRectangle(new SolidColorBrush(activeColor), null, underline, 1, 1);
                }

                x += tabW + gap;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int idx = HitTest(e.GetPosition(this));
            if (idx >= 0) SelectedIndex = idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.GetPosition(this));
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var items = Items;
            if (items == null) { base.OnKeyDown(e); return; }

            switch (e.Key)
            {
                case Key.Right:
                    SelectedIndex = Math.Min(items.Length - 1, SelectedIndex + 1);
                    e.Handled = true;
                    break;
                case Key.Left:
                    SelectedIndex = Math.Max(0, SelectedIndex - 1);
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _tabRects.Count; i++)
                if (_tabRects[i].Contains(pt)) return i;
            return -1;
        }
    }
}
