using System.Drawing;
using System.Windows;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Services
{
    public class TrayIconService : IDisposable
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static TrayIconService? Instance { get; private set; }

        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private System.Windows.Forms.ContextMenuStrip? _contextMenu;

        // Menu items that need checked-state updates
        private System.Windows.Forms.ToolStripMenuItem? _menuSilent;
        private System.Windows.Forms.ToolStripMenuItem? _menuBalanced;
        private System.Windows.Forms.ToolStripMenuItem? _menuTurbo;
        private System.Windows.Forms.ToolStripMenuItem? _menuEco;
        private System.Windows.Forms.ToolStripMenuItem? _menuStandard;
        private System.Windows.Forms.ToolStripMenuItem? _menuOptimized;

        public void Initialize()
        {
            Instance = this;
            _contextMenu = new System.Windows.Forms.ContextMenuStrip();
            _contextMenu.ShowCheckMargin = false;
            _contextMenu.ShowImageMargin = false;

            var itemFont = new Font("Segoe UI Variable", 9.5f);
            var headerFont = new Font("Segoe UI Variable", 8f, System.Drawing.FontStyle.Bold);

            // Performance modes (ACPI: 0=Balanced, 1=Turbo, 2=Silent)
            var perfHeader = new System.Windows.Forms.ToolStripLabel("PERFORMANCE") { Font = headerFont, ForeColor = System.Drawing.Color.FromArgb(110, 110, 120) };
            perfHeader.Padding = new System.Windows.Forms.Padding(8, 6, 0, 2);
            _contextMenu.Items.Add(perfHeader);

            _menuSilent = new System.Windows.Forms.ToolStripMenuItem("  Silent", null, (s, e) => SetPerformanceMode(2)) { Font = itemFont };
            _menuBalanced = new System.Windows.Forms.ToolStripMenuItem("  Balanced", null, (s, e) => SetPerformanceMode(0)) { Font = itemFont };
            _menuTurbo = new System.Windows.Forms.ToolStripMenuItem("  Turbo", null, (s, e) => SetPerformanceMode(1)) { Font = itemFont };
            _contextMenu.Items.Add(_menuSilent);
            _contextMenu.Items.Add(_menuBalanced);
            _contextMenu.Items.Add(_menuTurbo);

            _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // GPU modes
            var gpuHeader = new System.Windows.Forms.ToolStripLabel("GPU MODE") { Font = headerFont, ForeColor = System.Drawing.Color.FromArgb(110, 110, 120) };
            gpuHeader.Padding = new System.Windows.Forms.Padding(8, 4, 0, 2);
            _contextMenu.Items.Add(gpuHeader);

            _menuEco = new System.Windows.Forms.ToolStripMenuItem("  Eco", null, (s, e) => SetGpuMode(AsusACPI.GPUModeEco)) { Font = itemFont };
            _menuStandard = new System.Windows.Forms.ToolStripMenuItem("  Standard", null, (s, e) => SetGpuMode(AsusACPI.GPUModeStandard)) { Font = itemFont };
            _menuOptimized = new System.Windows.Forms.ToolStripMenuItem("  Optimized", null, (s, e) => SetGpuMode(AsusACPI.GPUModeStandard, auto: true)) { Font = itemFont };
            _contextMenu.Items.Add(_menuEco);
            _contextMenu.Items.Add(_menuStandard);
            _contextMenu.Items.Add(_menuOptimized);

            _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var quitItem = new System.Windows.Forms.ToolStripMenuItem("  Quit", null, (s, e) => QuitApplication()) { Font = itemFont };
            _contextMenu.Items.Add(quitItem);

            // Update checks when menu opens
            _contextMenu.Opening += (s, e) => RefreshMenuChecks();

            // Style
            _contextMenu.BackColor = System.Drawing.Color.FromArgb(24, 24, 30);
            _contextMenu.ForeColor = System.Drawing.Color.White;
            _contextMenu.Padding = new System.Windows.Forms.Padding(4, 6, 4, 6);
            _contextMenu.Renderer = new DarkMenuRenderer();

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "G-Aether",
                Icon = CreateDefaultIcon(),
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _trayIcon.MouseClick += OnTrayClick;
        }

        private void RefreshMenuChecks()
        {
            int perfMode = AppConfig.Get("performance_mode");
            int gpuMode = AppConfig.Get("gpu_mode");
            bool gpuAuto = AppConfig.Is("gpu_auto");

            // Performance: ACPI 0=Balanced, 1=Turbo, 2=Silent
            if (_menuSilent != null) _menuSilent.Checked = perfMode == 2;
            if (_menuBalanced != null) _menuBalanced.Checked = perfMode == 0;
            if (_menuTurbo != null) _menuTurbo.Checked = perfMode == 1;

            // GPU
            if (_menuEco != null) _menuEco.Checked = gpuMode == AsusACPI.GPUModeEco;
            if (_menuStandard != null) _menuStandard.Checked = gpuMode == AsusACPI.GPUModeStandard && !gpuAuto;
            if (_menuOptimized != null) _menuOptimized.Checked = gpuMode == AsusACPI.GPUModeStandard && gpuAuto;
        }

        private void OnTrayClick(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    if (mainWindow.IsVisible)
                    {
                        mainWindow.Hide();
                    }
                    else
                    {
                        mainWindow.Show();
                        mainWindow.Activate();
                        if (mainWindow.WindowState == WindowState.Minimized)
                            mainWindow.WindowState = WindowState.Normal;
                    }
                }
            }
        }

        private void SetPerformanceMode(int mode)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    AppConfig.Set("performance_mode", mode);
                    Program.modeControl?.SetPerformanceMode(mode);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Tray mode error: " + ex.Message);
                }
            });
        }

        private void SetGpuMode(int gpuMode, bool auto = false)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    AppConfig.Set("gpu_auto", auto ? 1 : 0);
                    AppConfig.Set("gpu_mode", gpuMode);
                    Program.gpuControl?.SetGPUMode(gpuMode, auto ? 1 : 0);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Tray GPU error: " + ex.Message);
                }
            });
        }

        private void QuitApplication()
        {
            Dispose();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Application.Current?.Shutdown();
            });
        }

        // Performance mode colors indexed by ACPI base: 0=Balanced, 1=Turbo, 2=Silent
        private static readonly System.Drawing.Color[] PerfColors =
        [
            System.Drawing.Color.FromArgb(96, 205, 255),    // 0: Balanced - blue
            System.Drawing.Color.FromArgb(255, 107, 53),   // 1: Turbo - orange
            System.Drawing.Color.FromArgb(167, 139, 250),  // 2: Silent - purple
        ];

        // GPU mode colors: Eco(green), Standard(blue), Ultimate(orange), Optimized(purple)
        private static readonly Dictionary<int, System.Drawing.Color> GpuColors = new()
        {
            { AsusACPI.GPUModeEco,      System.Drawing.Color.FromArgb(76, 201, 94) },    // Eco: green
            { AsusACPI.GPUModeStandard, System.Drawing.Color.FromArgb(96, 205, 255) },   // Standard: blue
            { AsusACPI.GPUModeUltimate, System.Drawing.Color.FromArgb(255, 107, 53) },   // Ultimate: orange
        };

        private int _lastPerfMode = -1;
        private int _lastGpuMode = -1;
        private bool _lastGpuAuto;

        private static readonly System.Drawing.Color OptimizedColor = System.Drawing.Color.FromArgb(171, 124, 255); // purple

        /// <summary>
        /// Update the tray icon to reflect current performance and GPU modes.
        /// Left half = Performance mode color, Right half = GPU mode color.
        /// </summary>
        public void UpdateIcon(int perfMode, int gpuMode)
        {
            if (_trayIcon == null) return;

            bool isAuto = AppConfig.Is("gpu_auto");

            // Skip if nothing changed
            if (perfMode == _lastPerfMode && gpuMode == _lastGpuMode && isAuto == _lastGpuAuto) return;
            _lastPerfMode = perfMode;
            _lastGpuMode = gpuMode;
            _lastGpuAuto = isAuto;

            var perfColor = perfMode >= 0 && perfMode < PerfColors.Length
                ? PerfColors[perfMode]
                : PerfColors[0];

            System.Drawing.Color gpuColor;
            if (isAuto && gpuMode == AsusACPI.GPUModeStandard)
                gpuColor = OptimizedColor;
            else if (GpuColors.TryGetValue(gpuMode, out var gc))
                gpuColor = gc;
            else
                gpuColor = System.Drawing.Color.FromArgb(96, 205, 255);

            var newIcon = CreateIcon(perfColor, gpuColor);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = newIcon;
            oldIcon?.Dispose();

            // Update tooltip
            // ACPI base: 0=Balanced, 1=Turbo, 2=Silent
            string perfName = perfMode switch { 0 => "Balanced", 1 => "Turbo", 2 => "Silent", _ => "Unknown" };
            string gpuName = gpuMode switch
            {
                AsusACPI.GPUModeEco => "Eco",
                AsusACPI.GPUModeUltimate => "Ultimate",
                _ => isAuto ? "Optimized" : "Standard"
            };
            _trayIcon.Text = $"G-Aether — {perfName} / {gpuName}";
        }

        private static Icon CreateIcon(System.Drawing.Color perfColor, System.Drawing.Color gpuColor)
        {
            int size = 32;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                int mid = size / 2;

                // Left half — Performance mode
                g.SetClip(new Rectangle(0, 0, mid, size));
                using (var leftBrush = new SolidBrush(perfColor))
                    g.FillEllipse(leftBrush, 0, 0, size - 1, size - 1);

                // Right half — GPU mode
                g.SetClip(new Rectangle(mid, 0, size - mid, size));
                using (var rightBrush = new SolidBrush(gpuColor))
                    g.FillEllipse(rightBrush, 0, 0, size - 1, size - 1);

                // Subtle divider line when colors differ
                g.ResetClip();
                if (perfColor != gpuColor)
                {
                    using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 0, 0, 0), 1f);
                    g.DrawLine(pen, mid, 3, mid, size - 4);
                }
            }
            var hIcon = bmp.GetHicon();
            var icon = Icon.FromHandle(hIcon);
            var clonedIcon = (Icon)icon.Clone();
            DestroyIcon(hIcon);
            return clonedIcon;
        }

        private static Icon CreateDefaultIcon()
        {
            return CreateIcon(
                System.Drawing.Color.FromArgb(96, 205, 255),   // Balanced blue (left)
                System.Drawing.Color.FromArgb(96, 205, 255)    // Standard blue (right)
            );
        }

        public void Dispose()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _contextMenu?.Dispose();
            _contextMenu = null;
        }
    }

    internal class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        // Accent colors for checked items by name
        private static readonly Dictionary<string, System.Drawing.Color> AccentMap = new()
        {
            { "Silent", System.Drawing.Color.FromArgb(167, 139, 250) },
            { "Balanced", System.Drawing.Color.FromArgb(96, 205, 255) },
            { "Turbo", System.Drawing.Color.FromArgb(255, 107, 53) },
            { "Eco", System.Drawing.Color.FromArgb(76, 201, 94) },
            { "Standard", System.Drawing.Color.FromArgb(96, 205, 255) },
            { "Optimized", System.Drawing.Color.FromArgb(171, 124, 255) },
        };

        public DarkMenuRenderer() : base(new DarkMenuColors()) { }

        protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is System.Windows.Forms.ToolStripMenuItem mi && mi.Checked)
            {
                string key = mi.Text.Trim();
                e.TextColor = AccentMap.TryGetValue(key, out var c) ? c : System.Drawing.Color.FromArgb(96, 205, 255);
            }
            else
            {
                e.TextColor = e.Item is System.Windows.Forms.ToolStripLabel
                    ? System.Drawing.Color.FromArgb(110, 110, 120)
                    : System.Drawing.Color.FromArgb(210, 210, 215);
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
        {
            var rect = new System.Drawing.Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

            if (e.Item.Selected && e.Item.Enabled)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = RoundedRect(rect, 6);
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(35, 255, 255, 255));
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 255, 255, 255));
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
        {
            // Draw rounded border
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedRect(new System.Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 10);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(50, 255, 255, 255));
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderToolStripBackground(System.Windows.Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedRect(new System.Drawing.Rectangle(0, 0, e.ToolStrip.Width, e.ToolStrip.Height), 10);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(24, 24, 30));
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemCheck(System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
        {
            // Draw colored dot instead of checkmark
            if (e.Item is System.Windows.Forms.ToolStripMenuItem mi)
            {
                string key = mi.Text.Trim();
                var color = AccentMap.TryGetValue(key, out var c) ? c : System.Drawing.Color.FromArgb(96, 205, 255);

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int dotSize = 7;
                int x = e.ImageRectangle.X + (e.ImageRectangle.Width - dotSize) / 2 + 6;
                int y = e.ImageRectangle.Y + (e.ImageRectangle.Height - dotSize) / 2;
                using var brush = new SolidBrush(color);
                e.Graphics.FillEllipse(brush, x, y, dotSize, dotSize);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(System.Drawing.Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal class DarkMenuColors : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(50, 50, 55);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.Transparent;
        public override System.Drawing.Color MenuStripGradientBegin => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color MenuStripGradientEnd => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.Transparent;
        public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.Transparent;
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(40, 40, 48);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(40, 40, 48);
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(24, 24, 30);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.Transparent;
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.Transparent;
        public override System.Drawing.Color CheckBackground => System.Drawing.Color.Transparent;
        public override System.Drawing.Color CheckPressedBackground => System.Drawing.Color.Transparent;
        public override System.Drawing.Color CheckSelectedBackground => System.Drawing.Color.Transparent;
    }
}
