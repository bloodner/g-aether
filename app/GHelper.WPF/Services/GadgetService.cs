using System.Windows;
using GHelper.WPF.ViewModels;
using GHelper.WPF.Views;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Lifecycle for the floating telemetry gadget. Owns a single GadgetWindow
    /// instance and responds to the "gadget_enabled" config flag.
    ///
    /// Bound to the existing MonitorViewModel so tile values come from the same
    /// 2s sensor tick everything else in the app uses — no duplicate reads.
    /// </summary>
    public static class GadgetService
    {
        private const string ConfigKey = "gadget_enabled";

        private static GadgetWindow? _window;
        private static MonitorViewModel? _vm;

        /// <summary>
        /// Called once from MainWindow after the MainViewModel is constructed.
        /// Reads the saved enabled state and shows the gadget if it was on.
        /// </summary>
        public static void Configure(MonitorViewModel monitorVm)
        {
            _vm = monitorVm;
            if (AppConfig.Is(ConfigKey)) Show();
        }

        /// <summary>Toggle from Settings; also persists.</summary>
        public static void SetEnabled(bool enabled)
        {
            AppConfig.Set(ConfigKey, enabled ? 1 : 0);
            if (enabled) Show();
            else Hide();
        }

        public static bool IsEnabled => AppConfig.Is(ConfigKey);

        /// <summary>
        /// Re-applies runtime-tunable gadget settings (currently close-button
        /// visibility) to the live window without recreating it.
        /// </summary>
        public static void ApplySettings()
        {
            _window?.ApplySettings();
        }

        /// <summary>
        /// Forget the saved position so the gadget snaps back to the top-right
        /// of the primary screen. Useful when the gadget ended up off-screen
        /// (disconnected monitor, weird DPI, etc.).
        /// </summary>
        public static void ResetPosition()
        {
            AppConfig.Set("gadget_x", 0);
            AppConfig.Set("gadget_y", 0);
            if (_window != null)
            {
                // Re-show forces OnLoaded to recompute the corner.
                Hide();
                if (IsEnabled) Show();
            }
        }

        private static void Show()
        {
            if (_window != null || _vm == null) return;
            _window = new GadgetWindow { DataContext = _vm };
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }

        private static void Hide()
        {
            _window?.Close();
            _window = null;
        }
    }
}
