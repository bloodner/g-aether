using System.Windows;
using System.Windows.Media;

namespace GHelper.WPF.Controls
{
    /// <summary>
    /// Renders the shared "accent halo tile" chrome — darker-than-window fill,
    /// hairline border, soft radial halo in the top-right, and a left vertical
    /// accent stripe. Doesn't hold content; stack it behind a content panel in a
    /// Grid so any tile layout can borrow the SensorCard / SparklineChart look.
    ///
    /// Use when a tile has a meaningful "accent color" (a status, category, or
    /// mode) but the content is too custom to fit SparklineChart or SensorCard.
    /// </summary>
    public class AccentHaloBackground : FrameworkElement
    {
        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(AccentHaloBackground),
                new FrameworkPropertyMetadata(Color.FromRgb(0x60, 0xCD, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public Color AccentColor
        {
            get => (Color)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            Color accent = AccentColor;
            double cornerRadius = 10;

            var cardRect = new Rect(0, 0, w, h);
            var cardGeo = CreateRoundedRect(cardRect, cornerRadius);

            // Fill
            var bgBrush = new SolidColorBrush(Color.FromRgb(0x13, 0x14, 0x1B));
            bgBrush.Freeze();
            dc.DrawGeometry(bgBrush, null, cardGeo);

            // Halo
            var halo = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.92, 0.08),
                Center = new Point(0.92, 0.08),
                RadiusX = 0.95,
                RadiusY = 1.4
            };
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(0x32, accent.R, accent.G, accent.B), 0.0));
            halo.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, accent.R, accent.G, accent.B), 0.60));
            halo.Freeze();
            dc.DrawGeometry(halo, null, cardGeo);

            // Border
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0x25, 0x2E)), 1);
            dc.DrawGeometry(null, borderPen, cardGeo);

            // Left accent stripe
            var stripeRect = new Rect(0, 12, 3, h - 24);
            var stripeBrush = new SolidColorBrush(accent);
            stripeBrush.Freeze();
            dc.DrawRoundedRectangle(stripeBrush, null, stripeRect, 1.5, 1.5);
        }

        private static Geometry CreateRoundedRect(Rect rect, double r)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                r = Math.Min(r, Math.Min(rect.Height, rect.Width) / 2);
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
