using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GHelper.WPF.Services
{
    /// <summary>
    /// Actions a global hotkey can invoke. Each value is stable across
    /// versions — users' saved bindings are keyed on this id.
    /// </summary>
    public enum HotkeyAction
    {
        CyclePerformance = 1,
        CycleGpu = 2,
        SceneReading = 10,
        SceneFocus = 11,
        ScenePresent = 12,
        SceneNight = 13,
        SceneGame = 14,
        ToggleWindow = 20,
        ToggleGadget = 21,
    }

    [Flags]
    public enum HotkeyModifiers : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
    }

    /// <summary>
    /// One user-configured binding: an action plus the Windows key + modifier
    /// combination that triggers it.
    /// </summary>
    public class HotkeyBinding
    {
        [JsonPropertyName("action")] public HotkeyAction Action { get; set; }
        [JsonPropertyName("mods")] public HotkeyModifiers Modifiers { get; set; }
        /// <summary>Virtual-key code (<see cref="Key"/> values map to these).</summary>
        [JsonPropertyName("vk")] public uint VirtualKey { get; set; }
    }

    /// <summary>
    /// App-wide global-hotkey registration. Owns a hidden HwndSource hook on
    /// the main window that listens for WM_HOTKEY messages and dispatches to
    /// action handlers. Handles registration conflicts gracefully — if another
    /// app already owns the combo, we log and leave the entry unregistered
    /// rather than crashing.
    /// </summary>
    public static class GlobalHotkeyService
    {
        private const string ConfigKey = "global_hotkeys";
        private const int WM_HOTKEY = 0x0312;

        // Arbitrary base ID for our hotkeys; Windows requires unique ids per thread.
        private const int BaseHotkeyId = 0x9000;

        private static HwndSource? _source;
        private static readonly Dictionary<int, HotkeyAction> _activeById = new();
        private static List<HotkeyBinding> _bindings = new();

        public static IReadOnlyList<HotkeyBinding> Bindings => _bindings;

        /// <summary>Raised after bindings change — lets VMs refresh their list view.</summary>
        public static event Action? BindingsChanged;

        public static void Initialize(Window mainWindow)
        {
            LoadBindings();

            var helper = new WindowInteropHelper(mainWindow);
            _source = HwndSource.FromHwnd(helper.Handle);
            if (_source == null)
            {
                Logger.WriteLine("GlobalHotkeyService: HwndSource unavailable — hotkeys disabled");
                return;
            }
            _source.AddHook(WndProc);
            RegisterAll();
            Logger.WriteLine($"GlobalHotkeyService: initialized with {_bindings.Count} binding(s)");
            // VMs constructed before this point read an empty list; tell them
            // bindings are now available so labels like "Not set" can refresh.
            BindingsChanged?.Invoke();
        }

        public static void Shutdown()
        {
            UnregisterAll();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }

        /// <summary>
        /// Temporarily release all global hotkey registrations so a capture
        /// dialog can read keystrokes without our handlers stealing them. Pair
        /// with <see cref="Resume"/> on dialog close.
        /// </summary>
        public static void Suspend() => UnregisterAll();

        public static void Resume() => RegisterAll();

        /// <summary>Replace all bindings with a new set (called by the VM after add/clear).</summary>
        public static void SetBindings(IEnumerable<HotkeyBinding> bindings)
        {
            UnregisterAll();
            _bindings = bindings.ToList();
            SaveBindings();
            RegisterAll();
            BindingsChanged?.Invoke();
        }

        /// <summary>Attempt to register a binding live without persisting. Returns true on success.</summary>
        public static bool TryRegister(HotkeyBinding binding)
        {
            if (_source == null) return false;
            int id = BaseHotkeyId + (int)binding.Action;
            UnregisterHotKey(_source.Handle, id);
            _activeById.Remove(id);

            bool ok = RegisterHotKey(_source.Handle, id, (uint)binding.Modifiers, binding.VirtualKey);
            if (ok) _activeById[id] = binding.Action;
            return ok;
        }

        // --- Message pump ---------------------------------------------------

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_HOTKEY) return IntPtr.Zero;

            int id = wParam.ToInt32();
            if (!_activeById.TryGetValue(id, out var action)) return IntPtr.Zero;

            handled = true;
            try { DispatchAction(action); }
            catch (Exception ex) { Logger.WriteLine($"Global hotkey dispatch ({action}) error: {ex.Message}"); }
            return IntPtr.Zero;
        }

        private static void DispatchAction(HotkeyAction action)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(() =>
            {
                if (app.MainWindow?.DataContext is not ViewModels.MainViewModel vm) return;

                switch (action)
                {
                    case HotkeyAction.CyclePerformance:
                        CycleIndex(vm.Performance.ModeLabels.Length,
                                   () => vm.Performance.SelectedModeIndex,
                                   i => vm.Performance.SelectedModeIndex = i);
                        break;

                    case HotkeyAction.CycleGpu:
                        CycleIndex(vm.Gpu.ModeLabels.Length,
                                   () => vm.Gpu.SelectedModeIndex,
                                   i => vm.Gpu.SelectedModeIndex = i);
                        break;

                    case HotkeyAction.SceneReading: ApplySceneByName(vm, "Reading"); break;
                    case HotkeyAction.SceneFocus:   ApplySceneByName(vm, "Focus");   break;
                    case HotkeyAction.ScenePresent: ApplySceneByName(vm, "Present"); break;
                    case HotkeyAction.SceneNight:   ApplySceneByName(vm, "Night");   break;
                    case HotkeyAction.SceneGame:    ApplySceneByName(vm, "Game");    break;

                    case HotkeyAction.ToggleWindow:
                        var win = app.MainWindow;
                        if (win == null) return;
                        if (win.IsVisible) { win.Hide(); }
                        else { win.Show(); win.Activate(); }
                        break;

                    case HotkeyAction.ToggleGadget:
                        GadgetService.SetEnabled(!GadgetService.IsEnabled);
                        break;
                }
            });
        }

        private static void CycleIndex(int count, Func<int> getCurrent, Action<int> setNext)
        {
            if (count <= 0) return;
            int next = (getCurrent() + 1) % count;
            setNext(next);
        }

        private static void ApplySceneByName(ViewModels.MainViewModel vm, string name)
        {
            var scene = SceneService.BuiltInScenes.FirstOrDefault(s => s.Name == name);
            scene?.Apply(vm);
            if (scene != null)
                ToastService.ShowOsdOnly(scene.Name, scene.Icon, ThemeService.AccentColor);
        }

        // --- Registration helpers -------------------------------------------

        private static void RegisterAll()
        {
            if (_source == null) return;
            foreach (var b in _bindings)
            {
                int id = BaseHotkeyId + (int)b.Action;
                bool ok = RegisterHotKey(_source.Handle, id, (uint)b.Modifiers, b.VirtualKey);
                if (ok)
                {
                    _activeById[id] = b.Action;
                }
                else
                {
                    Logger.WriteLine($"Global hotkey registration failed for {b.Action} — combo likely in use by another app");
                }
            }
        }

        private static void UnregisterAll()
        {
            if (_source == null) { _activeById.Clear(); return; }
            foreach (var id in _activeById.Keys.ToArray())
                UnregisterHotKey(_source.Handle, id);
            _activeById.Clear();
        }

        // --- Persistence ----------------------------------------------------

        private static void LoadBindings()
        {
            try
            {
                string? json = AppConfig.GetString(ConfigKey);
                if (string.IsNullOrWhiteSpace(json)) { _bindings = new(); return; }
                _bindings = JsonSerializer.Deserialize<List<HotkeyBinding>>(json) ?? new();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("GlobalHotkeyService.LoadBindings error: " + ex.Message);
                _bindings = new();
            }
        }

        private static void SaveBindings()
        {
            try { AppConfig.Set(ConfigKey, JsonSerializer.Serialize(_bindings)); }
            catch (Exception ex) { Logger.WriteLine("GlobalHotkeyService.SaveBindings error: " + ex.Message); }
        }

        // --- Helpers for UI formatting --------------------------------------

        /// <summary>
        /// Human-readable label like "Ctrl + Alt + P" for a binding, or
        /// <c>null</c> if the binding is blank.
        /// </summary>
        public static string? FormatBinding(HotkeyBinding? b)
        {
            if (b == null || b.VirtualKey == 0) return null;
            // Win first — it's the most distinctive modifier and reads as the
            // anchor of the chord. Then Ctrl > Alt > Shift > trigger.
            var parts = new List<string>();
            if ((b.Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            if ((b.Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((b.Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((b.Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            parts.Add(KeyInterop.KeyFromVirtualKey((int)b.VirtualKey).ToString());
            return string.Join(" + ", parts);
        }

        public static string ActionLabel(HotkeyAction action) => action switch
        {
            HotkeyAction.CyclePerformance => "Cycle Performance Mode",
            HotkeyAction.CycleGpu => "Cycle GPU Mode",
            HotkeyAction.SceneReading => "Scene: Reading",
            HotkeyAction.SceneFocus => "Scene: Focus",
            HotkeyAction.ScenePresent => "Scene: Present",
            HotkeyAction.SceneNight => "Scene: Night",
            HotkeyAction.SceneGame => "Scene: Game",
            HotkeyAction.ToggleWindow => "Show / Hide G-Aether",
            HotkeyAction.ToggleGadget => "Show / Hide G-Scope Floating",
            _ => action.ToString(),
        };

        public static IReadOnlyList<HotkeyAction> AllActions => new[]
        {
            HotkeyAction.CyclePerformance,
            HotkeyAction.CycleGpu,
            HotkeyAction.SceneReading,
            HotkeyAction.SceneFocus,
            HotkeyAction.ScenePresent,
            HotkeyAction.SceneNight,
            HotkeyAction.SceneGame,
            HotkeyAction.ToggleWindow,
            HotkeyAction.ToggleGadget,
        };

        // --- P/Invoke -------------------------------------------------------

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
