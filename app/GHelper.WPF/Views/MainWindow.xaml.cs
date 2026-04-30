using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GHelper.WPF.Services;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views
{
    public partial class MainWindow : Window
    {
        private readonly UIElement[] _panels;
        private readonly System.Windows.Controls.RadioButton[] _navButtons;
        private readonly TrayIconService _trayService = new();

        // Suppresses the Nav_Checked AppConfig.Set while the ctor is applying
        // the initial tab. Without it, a cold-boot override to Monitor would
        // clobber the user's saved pinned tab.
        private bool _restoringTab;

        public MainWindow()
        {
            InitializeComponent();

            ThemeService.Initialize();
            var mainVm = new MainViewModel();
            DataContext = mainVm;

            // Hand the gadget service the MonitorViewModel it binds to, and
            // show the gadget if the user had it on last session.
            GadgetService.Configure(mainVm.Monitor, mainVm.ModeStrip);
            ScopeService.Configure(mainVm.Monitor);

            // Slot 9 (App Profiles) is nulled out — App Profiles now lives inside the
            // Settings panel. Keeping the slot preserves pinned-tab indices for Processes (10).
            _panels = [PanelMonitor, PanelPerformance, PanelFans, PanelGpu, PanelBattery, PanelDisplay, PanelKeyboard, PanelKeyBindings, PanelExtra, null!, PanelProcesses];
            _navButtons = [NavMonitor, NavPerformance, null!, NavGpu, NavBattery, NavDisplay, NavLighting, NavKeyBindings, NavExtra, null!, NavProcesses];

            // Restore pinned tab (skip removed tabs). On a cold boot — system
            // uptime under 2 minutes, i.e. launched by Task Scheduler right after
            // login — always land on Monitor regardless of the saved tab. That's
            // the dashboard view; opening scrolled somewhere mid-Settings is
            // disorienting when the machine has just booted.
            _restoringTab = true;
            int pinned = AppConfig.Get("wpf_pinned_tab");
            bool coldBoot = Environment.TickCount64 < 2 * 60 * 1000;
            if (coldBoot) pinned = 0;
            if (pinned >= 0 && pinned < _navButtons.Length && _navButtons[pinned] != null)
                _navButtons[pinned].IsChecked = true;
            _restoringTab = false;

            // Initialize toast overlay on the root grid
            var mainGrid = (Grid)((System.Windows.Controls.Border)Content).Child;
            ToastService.Initialize(mainGrid);

            // Notify the user when an update is discovered. Fires at most once per
            // launch (UpdateNotifier publishes once); cached value covers the case
            // where the background check completed before this constructor ran.
            if (UpdateNotifier.Latest is { Status: UpdateStatus.UpdateAvailable } cached)
                ShowUpdateToast(cached);
            UpdateNotifier.UpdateDiscovered += result =>
            {
                if (result.Status == UpdateStatus.UpdateAvailable)
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => ShowUpdateToast(result)));
            };

            Loaded += OnLoaded;
            Closed += (s, e) => _trayService.Dispose();
            _trayService.Initialize();

            // Apply topmost setting
            if (AppConfig.Is("topmost"))
                Topmost = true;

            // Hide GPU nav when no dGPU
            if (DataContext is MainViewModel vm)
            {
                vm.Gpu.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(GpuViewModel.IsVisible))
                        NavGpu.Visibility = vm.Gpu.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                };
            }
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (_panels == null) return;
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag && int.TryParse(tag, out int index))
            {
                for (int i = 0; i < _panels.Length; i++)
                    if (_panels[i] != null)
                        _panels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

                // Save as pinned tab, except while the ctor is applying the
                // initial selection (so a cold-boot override to Monitor doesn't
                // clobber the user's saved preference).
                if (!_restoringTab) AppConfig.Set("wpf_pinned_tab", index);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            // Global hotkeys need the main window's hwnd to receive WM_HOTKEY messages.
            try { GlobalHotkeyService.Initialize(this); }
            catch (Exception ex) { Logger.WriteLine("GlobalHotkeyService init error: " + ex.Message); }
        }

        private void PositionWindow()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;

            var workArea = screen.WorkingArea;
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            double waWidth = workArea.Width * dpiX;
            double waHeight = workArea.Height * dpiY;
            double waLeft = workArea.Left * dpiX;
            double waTop = workArea.Top * dpiY;

            double maxH = waHeight * 0.85;
            if (ActualHeight > maxH)
                Height = maxH;

            Left = waLeft + waWidth - ActualWidth - 10;
            Top = waTop + waHeight - ActualHeight - 10;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ModeBadge_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag && int.TryParse(tag, out int index))
            {
                if (index >= 0 && index < _navButtons.Length && _navButtons[index] != null)
                    _navButtons[index].IsChecked = true;
            }
        }

        /// <summary>
        /// Slows touchpad scroll to a usable rate. Default WPF scroll speed (1 wheel
        /// notch ≈ 120 delta = 3 lines × the OS multiplier) is fine for mice but
        /// becomes a runaway when laptop touchpads send continuous deltas. Dividing
        /// by 3 brings it to a comfortable rate while keeping mouse wheels usable.
        /// </summary>
        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Fires a one-shot OSD-style toast announcing an available update. The
        /// user discovers the update without having to open the Settings panel.
        /// </summary>
        private static void ShowUpdateToast(UpdateCheckResult result)
        {
            string version = result.LatestVersion ?? "";
            string message = string.IsNullOrEmpty(version)
                ? "Update available"
                : $"Update available — {version}";
            //  is the MDL2 Update glyph (upward arrow into a tray)
            ToastService.ShowOsdOnly(message, "",
                System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF));
        }
    }
}
