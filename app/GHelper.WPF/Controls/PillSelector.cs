using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    public class PillSelector : FrameworkElement
    {
        private readonly List<Rect> _pillRects = new();
        private int _hoverIndex = -1;

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(PillSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(PillSelector),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender, OnSelectedIndexChanged));

        public static readonly DependencyProperty IconsProperty =
            DependencyProperty.Register(nameof(Icons), typeof(string[]), typeof(PillSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(PillSelector),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0x78, 0xD4), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ItemColorsProperty =
            DependencyProperty.Register(nameof(ItemColors), typeof(Color[]), typeof(PillSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(SelectionChanged), RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int>), typeof(PillSelector));

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

        public Color[]? ItemColors
        {
            get => (Color[]?)GetValue(ItemColorsProperty);
            set => SetValue(ItemColorsProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<int> SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        public PillSelector()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PillSelector ps)
                ps.RaiseEvent(new RoutedPropertyChangedEventArgs<int>(
                    (int)e.OldValue, (int)e.NewValue, SelectionChangedEvent));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;

            // Calculate if we need wrapping
            var items = Items;
            if (items != null && items.Length > 0)
            {
                double rows = CalculateRows(w, items);
                double rowH = 32;
                double rowGap = 6;
                return new Size(w, rows * rowH + (rows - 1) * rowGap);
            }
            return new Size(w, 34);
        }

        private double CalculateRows(double width, string[] items)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var iconFace = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double gap = 6;
            double padX = 12;
            double x = 0;
            double rows = 1;
            var icons = Icons;
            for (int i = 0; i < items.Length; i++)
            {
                var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, typeface, 11.5, Brushes.White, dpiScale);
                double cw = ft.Width;
                if (icons != null && i < icons.Length && !string.IsNullOrEmpty(icons[i]))
                {
                    var iconFt = new FormattedText(icons[i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, iconFace, 12, Brushes.White, dpiScale);
                    cw = iconFt.Width + 5 + ft.Width;
                }
                double pillW = cw + padX * 2;
                if (x > 0 && x + pillW > width)
                {
                    rows++;
                    x = 0;
                }
                x += pillW + gap;
            }
            return rows;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            _pillRects.Clear();

            var items = Items;
            if (items == null || items.Length == 0) return;

            double w = ActualWidth;
            double gap = 6;
            double pillH = 28;
            double rowH = 32;
            double rowGap = 6;
            double pillR = pillH / 2.0;
            double pillPadX = 12;

            var typeface = new Typeface(new FontFamily("Segoe UI Variable"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            double dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Measure all items
            var icons = Icons;
            var iconFace = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var contentWidths = new double[items.Length];
            var texts = new FormattedText[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                texts[i] = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                    System.Windows.FlowDirection.LeftToRight, typeface, 11.5, Brushes.White, dpiScale);
                contentWidths[i] = texts[i].Width;
                if (icons != null && i < icons.Length && !string.IsNullOrEmpty(icons[i]))
                {
                    var iconFt = new FormattedText(icons[i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, iconFace, 12, Brushes.White, dpiScale);
                    contentWidths[i] = iconFt.Width + 5 + texts[i].Width;
                }
            }

            // Force single row: shrink padding if needed to fit all items
            double totalContent = 0;
            for (int i = 0; i < items.Length; i++) totalContent += contentWidths[i];
            double totalGaps = (items.Length - 1) * gap;
            double defaultTotal = totalContent + (items.Length * pillPadX * 2) + totalGaps;

            double effectivePadX = pillPadX;
            if (defaultTotal > w)
            {
                // Shrink padding to fit — floor at 4px so pills stay visually distinct
                double availableForPadding = w - totalContent - totalGaps;
                effectivePadX = Math.Max(4, availableForPadding / (items.Length * 2));
            }

            double y = (rowH - pillH) / 2.0;
            DistributeRowSpace(items, contentWidths, 0, items.Length, w, y, pillH, effectivePadX, gap, dpiScale, dc, typeface);
        }

        private void DistributeRowSpace(string[] items, double[] contentWidths, int start, int end, double width,
            double y, double pillH, double basePadX, double gap, double dpiScale, DrawingContext dc, Typeface typeface)
        {
            int count = end - start;
            if (count == 0) return;

            double pillR = pillH / 2.0;
            double totalContentWidth = 0;
            for (int i = start; i < end; i++)
                totalContentWidth += contentWidths[i];

            double totalPadding = count * basePadX * 2;
            double totalGaps = (count - 1) * gap;
            double totalNeeded = totalContentWidth + totalPadding + totalGaps;

            double padX = basePadX;
            if (totalNeeded < width)
            {
                double extra = (width - totalNeeded) / count;
                padX = basePadX + extra / 2;
            }

            double x = 0;
            for (int i = start; i < end; i++)
            {
                double pillW = contentWidths[i] + padX * 2;
                var rect = new Rect(x, y, pillW, pillH);
                _pillRects.Add(rect);

                bool selected = (i == SelectedIndex);
                bool hovered = (i == _hoverIndex && !selected);

                // When ItemColors are provided, the selected pill fills with its item's
                // mode-color — so the color the user sees on the pill matches what shows up
                // in the status bar and tray icon. Without ItemColors, fall back to accent
                // (for generic non-mode pill selectors like Fan Control or Color Gamut).
                var itemColors = ItemColors;
                Color pillFill = (itemColors != null && i < itemColors.Length) ? itemColors[i] : AccentColor;

                Brush bg;
                if (selected)
                    bg = new SolidColorBrush(Color.FromArgb(200, pillFill.R, pillFill.G, pillFill.B));
                else if (hovered)
                    bg = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                else
                    bg = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));

                var geo = CreatePillGeometry(rect, pillR);
                dc.DrawGeometry(bg, null, geo);

                if (!selected)
                {
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
                    dc.DrawGeometry(null, borderPen, geo);
                }

                Color textColor = selected ? Colors.White : Color.FromRgb(0xC0, 0xC0, 0xC0);
                var textBrush = new SolidColorBrush(textColor);

                var icons = Icons;
                bool hasIcon = icons != null && i < icons.Length && !string.IsNullOrEmpty(icons[i]);

                if (hasIcon)
                {
                    var iconFace = new Typeface(new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    var iconFt = new FormattedText(icons![i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, iconFace, 12, textBrush, dpiScale);
                    var labelFt = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, typeface, 11.5, textBrush, dpiScale);
                    double totalW = iconFt.Width + 5 + labelFt.Width;
                    double startX = x + (pillW - totalW) / 2;
                    double iconY = y + (pillH - iconFt.Height) / 2;
                    double labelY = y + (pillH - labelFt.Height) / 2;
                    dc.DrawText(iconFt, new Point(startX, iconY));
                    dc.DrawText(labelFt, new Point(startX + iconFt.Width + 5, labelY));
                }
                else
                {
                    var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture,
                        System.Windows.FlowDirection.LeftToRight, typeface,
                        11.5, textBrush, dpiScale);
                    double textX = x + (pillW - ft.Width) / 2;
                    double textY = y + (pillH - ft.Height) / 2;
                    dc.DrawText(ft, new Point(textX, textY));
                }

                x += pillW + gap;
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
            _hoverIndex = -1;
            InvalidateVisual();
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
            for (int i = 0; i < _pillRects.Count; i++)
                if (_pillRects[i].Contains(pt)) return i;
            return -1;
        }

        private static Geometry CreatePillGeometry(Rect rect, double radius)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                double r = Math.Min(radius, Math.Min(rect.Height, rect.Width) / 2);
                ctx.BeginFigure(new Point(rect.Left + r, rect.Top), true, true);
                ctx.LineTo(new Point(rect.Right - r, rect.Top), true, false);
                ctx.ArcTo(new Point(rect.Right, rect.Top + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Right, rect.Bottom - r), true, false);
                ctx.ArcTo(new Point(rect.Right - r, rect.Bottom), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Left + r, rect.Bottom), true, false);
                ctx.ArcTo(new Point(rect.Left, rect.Bottom - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(rect.Left, rect.Top + r), true, false);
                ctx.ArcTo(new Point(rect.Left + r, rect.Top), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }
    }
}
