using System.Windows;
using System.Windows.Controls;
using GHelper.WPF.Services;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views
{
    public partial class MainWindow : Window
    {
        private readonly UIElement[] _panels;
        private readonly System.Windows.Controls.RadioButton[] _navButtons;
        private readonly TrayIconService _trayService = new();

        public MainWindow()
        {
            InitializeComponent();

            ThemeService.Initialize();
            DataContext = new MainViewModel();

            _panels = [PanelMonitor, PanelPerformance, PanelFans, PanelGpu, PanelBattery, PanelDisplay, PanelKeyboard, PanelKeyBindings, PanelExtra];
            _navButtons = [NavMonitor, NavPerformance, null!, NavGpu, NavBattery, NavDisplay, NavLighting, NavKeyBindings, NavExtra];

            // Restore pinned tab (skip removed tabs)
            int pinned = AppConfig.Get("wpf_pinned_tab");
            if (pinned >= 0 && pinned < _navButtons.Length && _navButtons[pinned] != null)
                _navButtons[pinned].IsChecked = true;

            // Initialize toast overlay on the root grid
            var mainGrid = (Grid)((System.Windows.Controls.Border)Content).Child;
            ToastService.Initialize(mainGrid);

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
                    _panels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;

                // Save as pinned tab
                AppConfig.Set("wpf_pinned_tab", index);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
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
    }
}
