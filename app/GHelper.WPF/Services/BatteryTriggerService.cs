namespace GHelper.WPF.Services
{
    /// <summary>
    /// Opt-in automation that reacts to battery thresholds while unplugged:
    /// at 20% it applies the Focus scene (Silent + Eco); at 10% it fires a
    /// one-shot toast to prompt the user to save their work.
    ///
    /// Hysteresis: each threshold fires once per "cycle" (going down). The
    /// trigger rearms only after the battery has risen back above a recovery
    /// threshold — so sitting at 19% for an hour doesn't keep re-firing, and
    /// a plug/unplug dance doesn't cause trigger spam.
    /// </summary>
    public static class BatteryTriggerService
    {
        private const string EnabledKey = "battery_triggers_enabled";

        private const int LowThreshold = 20;          // Apply Focus scene here
        private const int LowRearmThreshold = 35;     // Re-arm the low trigger here
        private const int CriticalThreshold = 10;     // Urgent toast here
        private const int CriticalRearmThreshold = 20;

        private static bool _lowFired;
        private static bool _criticalFired;

        public static bool IsEnabled
        {
            get => AppConfig.Is(EnabledKey);
            set => AppConfig.Set(EnabledKey, value ? 1 : 0);
        }

        /// <summary>
        /// Called every sensor tick from MonitorViewModel. No-op when disabled.
        /// Never throws — battery triggers are a convenience, not a hard requirement.
        /// </summary>
        public static void Check(int percent, bool onBattery)
        {
            try
            {
                if (!IsEnabled) return;

                // Rearm when battery has recovered enough — allows the trigger to fire
                // again if the user goes through another low cycle.
                if (_lowFired && percent >= LowRearmThreshold) _lowFired = false;
                if (_criticalFired && percent >= CriticalRearmThreshold) _criticalFired = false;

                // Only fire while on battery. Plug in = nothing to warn about.
                if (!onBattery) return;

                if (!_criticalFired && percent <= CriticalThreshold)
                {
                    _criticalFired = true;
                    FireCritical(percent);
                    // Critical implicitly means "we're definitely in low territory" — flag
                    // the low trigger too so we don't double-fire it right after.
                    _lowFired = true;
                    return;
                }

                if (!_lowFired && percent <= LowThreshold)
                {
                    _lowFired = true;
                    FireLow(percent);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("BatteryTriggerService.Check error: " + ex.Message);
            }
        }

        private static void FireLow(int percent)
        {
            Logger.WriteLine($"Battery trigger: LOW ({percent}%) — applying Focus scene");
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (System.Windows.Application.Current?.MainWindow?.DataContext
                        is not ViewModels.MainViewModel vm) return;

                    // Apply the built-in Focus scene (Silent + Eco). Reusing the existing
                    // scene infra keeps one apply path and teaches users the color vocabulary.
                    var focus = SceneService.BuiltInScenes.FirstOrDefault(s => s.Name == "Focus");
                    focus?.Apply(vm);

                    ToastService.ShowOsdOnly(
                        $"Low battery ({percent}%) — Focus engaged",
                        "\uEC31",          // PowerButton / low-battery glyph
                        ThemeService.ColorEco);
                }
                catch (Exception ex) { Logger.WriteLine("FireLow UI error: " + ex.Message); }
            });
        }

        private static void FireCritical(int percent)
        {
            Logger.WriteLine($"Battery trigger: CRITICAL ({percent}%) — save-work toast");
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    ToastService.ShowOsdOnly(
                        $"Battery critical ({percent}%) — save your work",
                        "\uE7BA",          // Warning glyph
                        ThemeService.ColorFansPower);  // Red — urgent
                }
                catch (Exception ex) { Logger.WriteLine("FireCritical UI error: " + ex.Message); }
            });
        }
    }
}
