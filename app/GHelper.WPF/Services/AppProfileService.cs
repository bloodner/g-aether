using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// One user-defined rule: when <see cref="ProcessName"/> gains foreground focus,
    /// apply the scene named <see cref="SceneName"/>. Process matching is
    /// case-insensitive and tolerant of a trailing ".exe".
    /// </summary>
    public class AppProfileRule
    {
        [JsonPropertyName("process")]
        public string ProcessName { get; set; } = "";

        [JsonPropertyName("scene")]
        public string SceneName { get; set; } = "";
    }

    /// <summary>
    /// Per-app profile automation.
    ///
    /// Subscribes to the Windows foreground-window change event. When a window
    /// owned by a process matching one of the user's rules becomes focused,
    /// applies the associated scene and snapshots the prior mode state so it
    /// can be restored when the user switches away to an unprofiled app.
    ///
    /// Snapshot behavior:
    /// - Unprofiled → profiled: snapshot prior state, apply scene.
    /// - Profiled → different profiled: apply new scene, keep original snapshot.
    /// - Profiled → unprofiled: restore the snapshot, clear it.
    /// </summary>
    public static class AppProfileService
    {
        private const string ConfigKey = "app_profiles";
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        // Keep the delegate instance alive — if it's GC'd while the hook is
        // active, callbacks crash the app.
        private static WinEventDelegate? _winEventDelegate;
        private static IntPtr _hookHandle = IntPtr.Zero;
        private static List<AppProfileRule> _rules = new();

        // Snapshot + active-rule tracking for the revert-on-blur logic.
        private static AppProfileRule? _activeRule;
        private static ModeSnapshot? _snapshot;

        public static IReadOnlyList<AppProfileRule> Rules => _rules;

        /// <summary>Called once from AppHost.Initialize.</summary>
        public static void Initialize()
        {
            LoadRules();

            _winEventDelegate = new WinEventDelegate(OnForegroundChanged);
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate,
                0, 0, WINEVENT_OUTOFCONTEXT);

            if (_hookHandle == IntPtr.Zero)
                Logger.WriteLine("AppProfileService: SetWinEventHook failed — per-app profiles disabled this session");
            else
                Logger.WriteLine($"AppProfileService: initialized with {_rules.Count} rule(s)");
        }

        public static void Shutdown()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
            _winEventDelegate = null;
        }

        public static void SetRules(IEnumerable<AppProfileRule> rules)
        {
            _rules = rules.ToList();
            SaveRules();
        }

        // --- Foreground hook -------------------------------------------------

        private static void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint thread, uint time)
        {
            try
            {
                string? processName = GetProcessNameForWindow(hwnd);
                if (processName == null) return;

                AppProfileRule? match = FindMatchingRule(processName);

                if (match == null)
                {
                    // Unprofiled foreground — revert if we were holding an active profile.
                    if (_activeRule != null)
                    {
                        RestoreSnapshot();
                        Logger.WriteLine($"AppProfile: {_activeRule.ProcessName} → left focus, restored snapshot");
                        _activeRule = null;
                        _snapshot = null;
                        ShowOsd("Restored", "\uE72C");
                    }
                    return;
                }

                // Same rule still active — no-op (e.g., user clicked a different window
                // within the same process).
                if (_activeRule != null && _activeRule.ProcessName.Equals(match.ProcessName, StringComparison.OrdinalIgnoreCase))
                    return;

                // Transition: unprofiled → profiled, snapshot the baseline first.
                if (_activeRule == null)
                    _snapshot = ModeSnapshot.Capture();

                _activeRule = match;
                ApplyScene(match.SceneName);
                Logger.WriteLine($"AppProfile: {match.ProcessName} focused → applied scene '{match.SceneName}'");
                ShowOsd($"{ShortName(match.ProcessName)} → {match.SceneName}", "\uE8AB");
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AppProfile foreground-change error: " + ex.Message);
            }
        }

        private static string? GetProcessNameForWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            try
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return null;
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
            catch { return null; }
        }

        private static AppProfileRule? FindMatchingRule(string processName)
        {
            // Tolerant match: user may have typed "blender.exe" or "blender".
            string normProc = processName.Trim();
            string normProcNoExt = normProc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? normProc.Substring(0, normProc.Length - 4)
                : normProc;

            foreach (var r in _rules)
            {
                string rule = r.ProcessName.Trim();
                string ruleNoExt = rule.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? rule.Substring(0, rule.Length - 4)
                    : rule;

                if (normProcNoExt.Equals(ruleNoExt, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return null;
        }

        // --- Scene apply + revert --------------------------------------------

        private static void ApplyScene(string sceneName)
        {
            var scene = SceneService.BuiltInScenes.FirstOrDefault(s =>
                s.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
            if (scene == null) return;

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current?.MainWindow?.DataContext
                    is ViewModels.MainViewModel vm)
                    scene.Apply(vm);
            });
        }

        private static void RestoreSnapshot()
        {
            var snap = _snapshot;
            if (snap == null) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current?.MainWindow?.DataContext
                    is ViewModels.MainViewModel vm)
                    snap.RestoreTo(vm);
            });
        }

        private static void ShowOsd(string message, string icon)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try { ToastService.ShowOsdOnly(message, icon, ThemeService.AccentColor); }
                catch { }
            });
        }

        private static string ShortName(string processName)
        {
            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4)
                : processName;
        }

        // --- Persistence -----------------------------------------------------

        private static void LoadRules()
        {
            try
            {
                string? json = AppConfig.GetString(ConfigKey);
                if (string.IsNullOrWhiteSpace(json)) { _rules = new(); return; }

                _rules = JsonSerializer.Deserialize<List<AppProfileRule>>(json) ?? new();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AppProfileService.LoadRules error: " + ex.Message);
                _rules = new();
            }
        }

        private static void SaveRules()
        {
            try
            {
                string json = JsonSerializer.Serialize(_rules);
                AppConfig.Set(ConfigKey, json);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("AppProfileService.SaveRules error: " + ex.Message);
            }
        }

        // --- P/Invoke --------------------------------------------------------

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }

    /// <summary>
    /// Captures the three most visible user-facing mode settings so they can
    /// be restored when a per-app profile deactivates. Deliberately narrow —
    /// we don't snapshot every setting the user touched, only the ones our
    /// scenes actually change, so restoring doesn't stomp unrelated tweaks.
    /// </summary>
    internal class ModeSnapshot
    {
        public int PerfIndex { get; set; }
        public int GpuIndex { get; set; }
        public int FreqIndex { get; set; }

        public static ModeSnapshot Capture()
        {
            var snap = new ModeSnapshot();
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Application.Current?.MainWindow?.DataContext
                    is not ViewModels.MainViewModel vm) return;
                snap.PerfIndex = vm.Performance.SelectedModeIndex;
                snap.GpuIndex = vm.Gpu.SelectedModeIndex;
                snap.FreqIndex = vm.Visual.SelectedFreqIndex;
            });
            return snap;
        }

        public void RestoreTo(ViewModels.MainViewModel vm)
        {
            // Clamp each index — between snapshot and restore the options might
            // have changed (e.g., a monitor was unplugged, shrinking the refresh
            // rate list). Silently skip anything that no longer fits.
            if (PerfIndex >= 0 && PerfIndex < vm.Performance.ModeLabels.Length)
                vm.Performance.SelectedModeIndex = PerfIndex;
            if (GpuIndex >= 0 && GpuIndex < vm.Gpu.ModeLabels.Length)
                vm.Gpu.SelectedModeIndex = GpuIndex;
            if (FreqIndex >= 0 && FreqIndex < vm.Visual.FrequencyLabels.Length)
                vm.Visual.SelectedFreqIndex = FreqIndex;
        }
    }
}
