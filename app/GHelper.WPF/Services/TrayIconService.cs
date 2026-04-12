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

            // Performance modes (ACPI: 0=Balanced, 1=Turbo, 2=Silent)
            var perfHeader = new System.Windows.Forms.ToolStripLabel("Performance");
            perfHeader.Font = new Font(perfHeader.Font, System.Drawing.FontStyle.Bold);
            perfHeader.ForeColor = System.Drawing.Color.Gray;
            _contextMenu.Items.Add(perfHeader);

            _menuSilent = new System.Windows.Forms.ToolStripMenuItem("Silent", null, (s, e) => SetPerformanceMode(2));
            _menuBalanced = new System.Windows.Forms.ToolStripMenuItem("Balanced", null, (s, e) => SetPerformanceMode(0));
            _menuTurbo = new System.Windows.Forms.ToolStripMenuItem("Turbo", null, (s, e) => SetPerformanceMode(1));
            _contextMenu.Items.Add(_menuSilent);
            _contextMenu.Items.Add(_menuBalanced);
            _contextMenu.Items.Add(_menuTurbo);

            _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // GPU modes
            var gpuHeader = new System.Windows.Forms.ToolStripLabel("GPU Mode");
            gpuHeader.Font = new Font(gpuHeader.Font, System.Drawing.FontStyle.Bold);
            gpuHeader.ForeColor = System.Drawing.Color.Gray;
            _contextMenu.Items.Add(gpuHeader);

            _menuEco = new System.Windows.Forms.ToolStripMenuItem("Eco", null, (s, e) => SetGpuMode(AsusACPI.GPUModeEco));
            _menuStandard = new System.Windows.Forms.ToolStripMenuItem("Standard", null, (s, e) => SetGpuMode(AsusACPI.GPUModeStandard));
            _menuOptimized = new System.Windows.Forms.ToolStripMenuItem("Optimized", null, (s, e) => SetGpuMode(AsusACPI.GPUModeStandard, auto: true));
            _contextMenu.Items.Add(_menuEco);
            _contextMenu.Items.Add(_menuStandard);
            _contextMenu.Items.Add(_menuOptimized);

            _contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _contextMenu.Items.Add("Quit", null, (s, e) => QuitApplication());

            // Update checkmarks when menu opens
            _contextMenu.Opening += (s, e) => RefreshMenuChecks();

            // Style the menu
            _contextMenu.BackColor = System.Drawing.Color.FromArgb(30, 30, 35);
            _contextMenu.ForeColor = System.Drawing.Color.White;
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

        /// <summary>
        /// Update the tray icon to reflect current performance and GPU modes.
        /// Left half = Performance mode color, Right half = GPU mode color.
        /// </summary>
        public void UpdateIcon(int perfMode, int gpuMode)
        {
            if (_trayIcon == null) return;

            // Skip if nothing changed
            if (perfMode == _lastPerfMode && gpuMode == _lastGpuMode) return;
            _lastPerfMode = perfMode;
            _lastGpuMode = gpuMode;

            Logger.WriteLine($"Tray icon update: perf={perfMode}, gpu={gpuMode}");

            var perfColor = perfMode >= 0 && perfMode < PerfColors.Length
                ? PerfColors[perfMode]
                : PerfColors[0]; // default to Balanced blue

            var gpuColor = GpuColors.TryGetValue(gpuMode, out var gc)
                ? gc
                : System.Drawing.Color.FromArgb(96, 205, 255); // default to Standard blue

            var newIcon = CreateIcon(perfColor, gpuColor);
            var oldIcon = _trayIcon.Icon;
            _trayIcon.Icon = newIcon;
            oldIcon?.Dispose();

            // Update tooltip
            // ACPI base: 0=Balanced, 1=Turbo, 2=Silent
            string perfName = perfMode switch { 0 => "Balanced", 1 => "Turbo", 2 => "Silent", _ => "Unknown" };
            bool isAuto = AppConfig.Is("gpu_auto");
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

    // Custom dark renderer for the context menu
    internal class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { }

        protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = System.Drawing.Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(50, 255, 255, 255));
                e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }

    internal class DarkMenuColors : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(60, 60, 65);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.Transparent;
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(50, 50, 55);
        public override System.Drawing.Color MenuStripGradientBegin => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color MenuStripGradientEnd => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color MenuItemSelectedGradientBegin => System.Drawing.Color.FromArgb(50, 50, 55);
        public override System.Drawing.Color MenuItemSelectedGradientEnd => System.Drawing.Color.FromArgb(50, 50, 55);
        public override System.Drawing.Color MenuItemPressedGradientBegin => System.Drawing.Color.FromArgb(60, 60, 65);
        public override System.Drawing.Color MenuItemPressedGradientEnd => System.Drawing.Color.FromArgb(60, 60, 65);
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(30, 30, 35);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(60, 60, 65);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(60, 60, 65);
    }
}
