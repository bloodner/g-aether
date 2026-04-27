using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Lifecycle for the G-Scope panel's runtime config. Parallel to
    /// GadgetService but lighter — there's no window to own; the panel is
    /// embedded in the main window. Provides a single ApplySettings() that
    /// MonitorViewModel listens for to re-read its scope_* config keys.
    /// </summary>
    public static class ScopeService
    {
        private static MonitorViewModel? _monitor;

        /// <summary>
        /// Called once from MainWindow after the MainViewModel is built.
        /// Hands the panel's MonitorViewModel a reference so ApplySettings
        /// can poke it later.
        /// </summary>
        public static void Configure(MonitorViewModel monitor)
        {
            _monitor = monitor;
            _monitor.ReloadScopeSettings();
        }

        /// <summary>
        /// Tells the MonitorViewModel to re-read every scope_* AppConfig
        /// key and update its observable display properties. Called from
        /// ScopeSettingsViewModel whenever any setting changes.
        /// </summary>
        public static void ApplySettings()
        {
            _monitor?.ReloadScopeSettings();
        }
    }
}
