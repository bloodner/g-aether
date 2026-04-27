using System.Windows;
using GHelper.WPF.ViewModels;
using GHelper.WPF.Views;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Lifecycle for the floating telemetry gadget. Owns a single GadgetWindow
    /// instance and responds to the "gadget_enabled" config flag.
    ///
    /// DataContext for the gadget is a small wrapper around two VMs: the same
    /// MonitorViewModel the app uses for its 1s sensor tick (so tile values
    /// stay in lockstep), and the shared ModeStripViewModel so the gadget's
    /// strip mirrors the main window's badges.
    /// </summary>
    public static class GadgetService
    {
        private const string ConfigKey = "gadget_enabled";

        private static GadgetWindow? _window;
        private static MonitorViewModel? _monitor;
        private static ModeStripViewModel? _modeStrip;

        /// <summary>
        /// DataContext exposed to GadgetWindow. Bindings reach Monitor.*
        /// for tile values and ModeStrip.* for the mode strip.
        /// </summary>
        public sealed class GadgetDataContext
        {
            public MonitorViewModel Monitor { get; }
            public ModeStripViewModel ModeStrip { get; }

            public GadgetDataContext(MonitorViewModel monitor, ModeStripViewModel modeStrip)
            {
                Monitor = monitor;
                ModeStrip = modeStrip;
            }
        }

        /// <summary>
        /// Called once from MainWindow after the MainViewModel is constructed.
        /// Reads the saved enabled state and shows the gadget if it was on.
        /// </summary>
        public static void Configure(MonitorViewModel monitor, ModeStripViewModel modeStrip)
        {
            _monitor = monitor;
            _modeStrip = modeStrip;
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
        /// Re-applies runtime-tunable gadget settings to the live window
        /// without recreating it.
        /// </summary>
        public static void ApplySettings() => _window?.ApplySettings();

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
                Hide();
                if (IsEnabled) Show();
            }
        }

        private static void Show()
        {
            if (_window != null || _monitor == null || _modeStrip == null) return;
            _window = new GadgetWindow
            {
                DataContext = new GadgetDataContext(_monitor, _modeStrip),
            };
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
