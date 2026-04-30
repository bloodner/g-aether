using System.Drawing;
using System.Windows;
using GHelper.WPF.ViewModels;
using GHelper.WPF.Views;

namespace GHelper.WPF.Services
{
    public class TrayIconService : IDisposable
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static TrayIconService? Instance { get; private set; }

        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private TrayMenuWindow? _menuWindow;

        public void Initialize()
        {
            Instance = this;

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "G-Aether",
                Icon = CreateDefaultIcon(),
                Visible = true,
                // No ContextMenuStrip — we handle right-click ourselves via MouseUp
            };

            _trayIcon.MouseUp += OnTrayMouseUp;

            // Update-available overlay — pick up cached result if the background check
            // already completed, and subscribe for late discovery.
            if (UpdateNotifier.Latest is { Status: UpdateStatus.UpdateAvailable })
                SetUpdateAvailable(true);
            UpdateNotifier.UpdateDiscovered += result =>
            {
                if (result.Status == UpdateStatus.UpdateAvailable) SetUpdateAvailable(true);
            };
        }

        /// <summary>
        /// Toggles the update-available overlay dot on the tray icon. Triggers a
        /// re-render only if the state actually changed.
        /// </summary>
        public void SetUpdateAvailable(bool available)
        {
            if (_updateAvailable == available) return;
            _updateAvailable = available;
            // Force a redraw using the last known perf/gpu modes.
            if (_lastPerfMode >= 0) UpdateIcon(_lastPerfMode, _lastGpuMode);
        }

        private void OnTrayMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ToggleMainWindow();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                ShowContextMenu(System.Windows.Forms.Cursor.Position);
            }
        }

        private void ToggleMainWindow()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return;
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

        private void ShowContextMenu(System.Drawing.Point cursorPhysicalPx)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_menuWindow == null)
                {
                    _menuWindow = new TrayMenuWindow();
                    _menuWindow.PerfSelected += SetPerformanceMode;
                    _menuWindow.GpuSelected += (mode, auto) => SetGpuMode(mode, auto);
                    _menuWindow.QuitSelected += QuitApplication;
                }

                int perfMode = AppConfig.Get("performance_mode");
                int gpuMode = AppConfig.Get("gpu_mode");
                bool gpuAuto = AppConfig.Is("gpu_auto");
                _menuWindow.Rebuild(perfMode, gpuMode, gpuAuto);
                _menuWindow.OpenAt(cursorPhysicalPx);
            });
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

        // All tray-icon colors pull from ThemeService so the icon, status bar, toasts,
        // and tray menu share one palette (Balanced/Standard follow the Windows accent).
        private static System.Drawing.Color ToDrawing(System.Windows.Media.Color c)
            => System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);

        // ACPI base: 0=Balanced, 1=Turbo, 2=Silent
        private static System.Drawing.Color PerfColor(int mode) => mode switch
        {
            1 => ToDrawing(ThemeService.ColorTurbo),
            2 => ToDrawing(ThemeService.ColorSilent),
            _ => ToDrawing(ThemeService.ColorBalanced),
        };

        private static System.Drawing.Color GpuColor(int mode) => mode switch
        {
            AsusACPI.GPUModeEco => ToDrawing(ThemeService.ColorEco),
            AsusACPI.GPUModeUltimate => ToDrawing(ThemeService.ColorUltimate),
            _ => ToDrawing(ThemeService.ColorStandard),
        };

        private int _lastPerfMode = -1;
        private int _lastGpuMode = -1;
        private bool _lastGpuAuto;
        private bool _lastBw;
        private bool _lastUpdateAvailable;
        private bool _updateAvailable;

        private static System.Drawing.Color ToGrayscale(System.Drawing.Color c)
        {
            // Luminosity formula; shift toward slightly brighter so the icon reads on dark taskbars
            int luma = (int)(0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B);
            int v = Math.Clamp(luma + 60, 0, 255);
            return System.Drawing.Color.FromArgb(c.A, v, v, v);
        }

        private static System.Drawing.Color OptimizedColor => ToDrawing(ThemeService.ColorOptimized);

        /// <summary>
        /// Update the tray icon to reflect current performance and GPU modes.
        /// Left half = Performance mode color, Right half = GPU mode color.
        /// </summary>
        public void UpdateIcon(int perfMode, int gpuMode)
        {
            if (_trayIcon == null) return;

            bool isAuto = AppConfig.Is("gpu_auto");
            bool bw = AppConfig.Is("bw_icon");

            // Skip if nothing changed
            bool updateAvailable = _updateAvailable;
            if (perfMode == _lastPerfMode && gpuMode == _lastGpuMode && isAuto == _lastGpuAuto && bw == _lastBw && updateAvailable == _lastUpdateAvailable) return;
            _lastPerfMode = perfMode;
            _lastGpuMode = gpuMode;
            _lastGpuAuto = isAuto;
            _lastBw = bw;
            _lastUpdateAvailable = updateAvailable;

            var perfColor = PerfColor(perfMode);

            System.Drawing.Color gpuColor;
            if (isAuto && gpuMode == AsusACPI.GPUModeStandard)
                gpuColor = OptimizedColor;
            else
                gpuColor = GpuColor(gpuMode);

            if (bw)
            {
                perfColor = ToGrayscale(perfColor);
                gpuColor = ToGrayscale(gpuColor);
            }

            var newIcon = CreateIcon(perfColor, gpuColor, updateAvailable);
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

        // Outer swoosh + tail accent (colored by perf mode); inner "A" arrowhead (colored by GPU mode).
        private static readonly Lazy<Bitmap?> _perfMask = new(() => LoadMask("app-mask-perf.png"));
        private static readonly Lazy<Bitmap?> _gpuMask = new(() => LoadMask("app-mask-gpu.png"));

        private static Bitmap? LoadMask(string name)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Resources/{name}");
                using var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream == null) return null;
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Tray mask load error ({name}): " + ex.Message);
                return null;
            }
        }

        // Recolor every non-transparent pixel to `color`, preserving the alpha channel
        // so antialiased edges stay smooth. Returns a new Bitmap owned by the caller.
        private static Bitmap TintMask(Bitmap src, System.Drawing.Color color)
        {
            var rect = new Rectangle(0, 0, src.Width, src.Height);
            var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var srcData = src.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var dstData = dst.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(srcData.Stride) * src.Height;
                byte[] buf = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, buf, 0, bytes);
                // Format32bppArgb byte order is B, G, R, A. The mask may have premultiplied RGB
                // from resvg, so write premultiplied (color * a / 255) to match.
                for (int i = 0; i < bytes; i += 4)
                {
                    byte a = buf[i + 3];
                    if (a == 0) { buf[i] = buf[i + 1] = buf[i + 2] = 0; continue; }
                    buf[i]     = (byte)(color.B * a / 255);
                    buf[i + 1] = (byte)(color.G * a / 255);
                    buf[i + 2] = (byte)(color.R * a / 255);
                }
                System.Runtime.InteropServices.Marshal.Copy(buf, 0, dstData.Scan0, bytes);
            }
            finally
            {
                src.UnlockBits(srcData);
                dst.UnlockBits(dstData);
            }
            return dst;
        }

        private static Icon CreateIcon(System.Drawing.Color perfColor, System.Drawing.Color gpuColor, bool updateAvailable = false)
        {
            int size = 32;
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                var perf = _perfMask.Value;
                var gpu = _gpuMask.Value;

                if (perf != null && gpu != null)
                {
                    // Z-order matches the source SVG: outer swoosh behind, inner A in front.
                    using var tintedPerf = TintMask(perf, perfColor);
                    using var tintedGpu = TintMask(gpu, gpuColor);
                    var rect = new Rectangle(0, 0, size, size);
                    g.DrawImage(tintedPerf, rect);
                    g.DrawImage(tintedGpu, rect);
                }
                else
                {
                    // Fallback if resources are missing: original half-circle design
                    int mid = size / 2;
                    g.SetClip(new Rectangle(0, 0, mid, size));
                    using (var l = new SolidBrush(perfColor)) g.FillEllipse(l, 0, 0, size - 1, size - 1);
                    g.SetClip(new Rectangle(mid, 0, size - mid, size));
                    using (var r = new SolidBrush(gpuColor)) g.FillEllipse(r, 0, 0, size - 1, size - 1);
                    g.ResetClip();
                }

                // Update-available overlay — small accent-blue dot in the bottom-right
                // corner. Discoverable without opening the main window.
                if (updateAvailable)
                {
                    int dotSize = 12;
                    int margin = 1;
                    int x = size - dotSize - margin;
                    int y = size - dotSize - margin;
                    var rectDot = new RectangleF(x, y, dotSize, dotSize);

                    // White outline so the dot reads against any tint underneath
                    using (var outline = new SolidBrush(System.Drawing.Color.FromArgb(220, 255, 255, 255)))
                        g.FillEllipse(outline, x - 1, y - 1, dotSize + 2, dotSize + 2);

                    // Accent-blue fill matches the nav-gear badge in the main window
                    using (var fill = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x60, 0xCD, 0xFF)))
                        g.FillEllipse(fill, rectDot);
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
            _menuWindow?.Close();
            _menuWindow = null;
        }
    }
}
