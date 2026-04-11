using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// Collapsed lighting mode selector: shows current mode icon + name inline.
    /// Clicking expands to reveal a grid of icon tiles for all available modes.
    /// </summary>
    public class LightingModeSelector : FrameworkElement
    {
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(LightingModeSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty IconsProperty =
            DependencyProperty.Register(nameof(Icons), typeof(string[]), typeof(LightingModeSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(LightingModeSelector),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedIndexChanged));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(LightingModeSelector),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0x78, 0xD4), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int>), typeof(LightingModeSelector));

        public string[]? Items
        {
            get => (string[]?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public string[]? Icons
        {
            get => (string[]?)GetValue(IconsProperty);
            set => SetValue(IconsProperty, value);
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

        public event RoutedPropertyChangedEventHandler<int> SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        // Layout constants
        private const double CollapsedHeight = 36;
        private const double TileSize = 40;
        private const double TileGap = 6;
        private const double TileRadius = 8;
        private const double GridPadTop = 8;
        private const double GridPadBottom = 4;
        private const double CollapsedRadius = 10;
        private const double ChevronSize = 20;

        // State
        private bool _expanded;
        private int _hoverIndex = -1;
        private bool _hoverChevron;

        // Hit rects
        private Rect _collapsedRect;
        private Rect _chevronRect;
        private readonly List<Rect> _tileRects = new();

        private static readonly Typeface LabelFace = new(
            new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Typeface IconFace = new(
            new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public LightingModeSelector()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            PreviewMouseDown += OnPreviewClick;
            PreviewMouseMove += OnPreviewMove;
            MouseLeave += OnLeave;
        }

        // Ensure hit testing works on the entire control area
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LightingModeSelector s)
                s.RaiseEvent(new RoutedPropertyChangedEventArgs<int>(
                    (int)e.OldValue, (int)e.NewValue, SelectionChangedEvent));
        }

        private int TilesPerRow(double width)
        {
            if (width <= 0) return 1;
            return Math.Max(1, (int)((width + TileGap) / (TileSize + TileGap)));
        }

        private int GridRows(int itemCount, int perRow) =>
            itemCount <= 0 ? 0 : (int)Math.Ceiling((double)itemCount / perRow);

        protected override Size MeasureOverride(Size available)
        {
            double w = double.IsInfinity(available.Width) ? 300 : available.Width;
            double h = CollapsedHeight;

            if (_expanded)
            {
                var items = Items;
                if (items != null && items.Length > 0)
                {
                    int perRow = TilesPerRow(w);
                    int rows = GridRows(items.Length, perRow);
                    h += GridPadTop + rows * TileSize + (rows - 1) * TileGap + GridPadBottom;
                }
            }

            return new Size(w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _tileRects.Clear();

            // Draw transparent background so WPF registers hit tests on entire area
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

            var items = Items;
            var icons = Icons;
            double w = ActualWidth;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // --- Collapsed bar ---
            _collapsedRect = new Rect(0, 0, w, CollapsedHeight);
            var barBg = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            dc.DrawRoundedRectangle(barBg, null, _collapsedRect, CollapsedRadius, CollapsedRadius);

            // Border
            var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            dc.DrawRoundedRectangle(null, borderPen, _collapsedRect, CollapsedRadius, CollapsedRadius);

            // Selected icon + label
            int sel = SelectedIndex;
            if (items != null && sel >= 0 && sel < items.Length)
            {
                string label = items[sel];
                string? icon = (icons != null && sel < icons.Length) ? icons[sel] : null;

                var accentBrush = new SolidColorBrush(AccentColor);

                double contentX = 14;
                double cy = CollapsedHeight / 2;

                if (!string.IsNullOrEmpty(icon))
                {
                    var iconFt = new FormattedText(icon, CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, IconFace, 14, accentBrush, dpi);
                    dc.DrawText(iconFt, new Point(contentX, cy - iconFt.Height / 2));
                    contentX += iconFt.Width + 8;
                }

                var labelFt = new FormattedText(label, CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, LabelFace, 12.5, Brushes.White, dpi);
                dc.DrawText(labelFt, new Point(contentX, cy - labelFt.Height / 2));
            }

            // Chevron
            string chevronGlyph = _expanded ? "\uE70E" : "\uE70D";
            var chevronBrush = _hoverChevron
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            var chevronFt2 = new FormattedText(chevronGlyph, CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, IconFace, 12, chevronBrush, dpi);

            double chevX = w - ChevronSize - 10;
            double chevY = CollapsedHeight / 2 - chevronFt2.Height / 2;
            _chevronRect = new Rect(chevX - 4, 0, ChevronSize + 14, CollapsedHeight);
            dc.DrawText(chevronFt2, new Point(chevX, chevY));

            // --- Expanded grid ---
            if (!_expanded || items == null || items.Length == 0) return;

            int perRow = TilesPerRow(w);
            double gridStartY = CollapsedHeight + GridPadTop;

            // Center the grid horizontally
            double gridWidth = perRow * TileSize + (perRow - 1) * TileGap;
            double gridOffsetX = (w - gridWidth) / 2;

            for (int i = 0; i < items.Length; i++)
            {
                int col = i % perRow;
                int row = i / perRow;
                double tx = gridOffsetX + col * (TileSize + TileGap);
                double ty = gridStartY + row * (TileSize + TileGap);
                var tileRect = new Rect(tx, ty, TileSize, TileSize);
                _tileRects.Add(tileRect);

                bool isSelected = (i == sel);
                bool isHovered = (i == _hoverIndex && !isSelected);

                // Tile background
                Brush tileBg;
                if (isSelected)
                    tileBg = new SolidColorBrush(Color.FromArgb(200, AccentColor.R, AccentColor.G, AccentColor.B));
                else if (isHovered)
                    tileBg = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                else
                    tileBg = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));

                dc.DrawRoundedRectangle(tileBg, null, tileRect, TileRadius, TileRadius);

                if (!isSelected)
                {
                    var tileBorder = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 0.5);
                    dc.DrawRoundedRectangle(null, tileBorder, tileRect, TileRadius, TileRadius);
                }

                double cx = tx + TileSize / 2;
                double tcy = ty + TileSize / 2;

                // Icon
                string? tileIcon = (icons != null && i < icons.Length) ? icons[i] : null;
                bool hasIcon = !string.IsNullOrEmpty(tileIcon);

                Color tileTextColor = isSelected ? Colors.White : Color.FromRgb(0xC0, 0xC0, 0xC0);
                var tileTextBrush = new SolidColorBrush(tileTextColor);

                if (hasIcon)
                {
                    // Icon above, tiny label below
                    var iconFt = new FormattedText(tileIcon!, CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, IconFace, 14, tileTextBrush, dpi);

                    // Abbreviate label to fit
                    string shortLabel = AbbreviateLabel(items[i]);
                    var labelFt = new FormattedText(shortLabel, CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, LabelFace, 7.5, tileTextBrush, dpi);

                    double totalH = iconFt.Height + 1 + labelFt.Height;
                    double startY = tcy - totalH / 2;

                    dc.DrawText(iconFt, new Point(cx - iconFt.Width / 2, startY));
                    dc.DrawText(labelFt, new Point(cx - labelFt.Width / 2, startY + iconFt.Height + 1));
                }
                else
                {
                    // Just label centered
                    string shortLabel = AbbreviateLabel(items[i]);
                    var labelFt = new FormattedText(shortLabel, CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, LabelFace, 9, tileTextBrush, dpi);
                    dc.DrawText(labelFt, new Point(cx - labelFt.Width / 2, tcy - labelFt.Height / 2));
                }

                // Tooltip on hover: show full name
                if (isHovered)
                {
                    var tipFt = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, LabelFace, 10,
                        new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), dpi);
                    double tipW = tipFt.Width + 12;
                    double tipH = tipFt.Height + 6;
                    double tipX = cx - tipW / 2;
                    double tipY = ty - tipH - 3;

                    // Keep tooltip within bounds
                    if (tipX < 0) tipX = 0;
                    if (tipX + tipW > w) tipX = w - tipW;

                    var tipRect = new Rect(tipX, tipY, tipW, tipH);
                    dc.DrawRoundedRectangle(
                        new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)), null,
                        tipRect, 4, 4);
                    dc.DrawRoundedRectangle(null,
                        new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5),
                        tipRect, 4, 4);
                    dc.DrawText(tipFt, new Point(tipX + 6, tipY + 3));
                }
            }
        }

        private static string AbbreviateLabel(string label)
        {
            if (label.Length <= 6) return label;
            // Common abbreviations
            return label switch
            {
                "Color Cycle" => "Cycle",
                "Highlight" => "Hi-lite",
                "Heatmap" => "Heat",
                "GPU Mode" => "GPU",
                "Ambient" => "Ambi",
                "Battery" => "Batt",
                "Contrast" => "Cntrst",
                _ => label.Length > 6 ? label[..5] + "." : label
            };
        }

        private void OnPreviewClick(object sender, MouseButtonEventArgs e)
        {
            Focus();
            var pos = e.GetPosition(this);

            // Check tile clicks first (when expanded)
            if (_expanded)
            {
                int tileIdx = HitTestTile(pos);
                if (tileIdx >= 0)
                {
                    SelectedIndex = tileIdx;
                    _expanded = false;
                    InvalidateMeasure();
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            // Toggle expand/collapse on bar click
            if (_collapsedRect.Contains(pos))
            {
                _expanded = !_expanded;
                _hoverIndex = -1;
                InvalidateMeasure();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        private void OnPreviewMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);

            bool chevron = _chevronRect.Contains(pos);
            int tileIdx = _expanded ? HitTestTile(pos) : -1;

            if (chevron != _hoverChevron || tileIdx != _hoverIndex)
            {
                _hoverChevron = chevron;
                _hoverIndex = tileIdx;
                InvalidateVisual();
            }
        }

        private void OnLeave(object sender, MouseEventArgs e)
        {
            _hoverIndex = -1;
            _hoverChevron = false;
            InvalidateVisual();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var items = Items;
            if (items == null) { base.OnKeyDown(e); return; }

            switch (e.Key)
            {
                case Key.Enter:
                case Key.Space:
                    _expanded = !_expanded;
                    InvalidateMeasure();
                    InvalidateVisual();
                    e.Handled = true;
                    break;
                case Key.Escape when _expanded:
                    _expanded = false;
                    InvalidateMeasure();
                    InvalidateVisual();
                    e.Handled = true;
                    break;
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

        private int HitTestTile(Point pt)
        {
            for (int i = 0; i < _tileRects.Count; i++)
                if (_tileRects[i].Contains(pt)) return i;
            return -1;
        }
    }
}
