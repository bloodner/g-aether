using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.Input;

namespace GHelper.WPF.ViewModels
{
    public partial class KeyBindingsViewModel : ObservableObject
    {
        private static readonly string[] ActionLabels =
        [
            "Disabled",
            "Open G-Aether",
            "Cycle Performance",
            "Cycle Aura Lighting",
            "Cycle Visual Mode",
            "Mute Microphone",
            "Volume Mute",
            "Media Play/Pause",
            "Screenshot",
            "Lock Screen",
            "Screen Off",
            "Toggle MiniLED",
            "Brightness Up",
            "Brightness Down",
            "Toggle Fn Lock",
            "Calculator",
        ];

        private static readonly string[] ActionValues =
        [
            "",
            "ghelper",
            "performance",
            "aura",
            "visual",
            "micmute",
            "mute",
            "play",
            "screenshot",
            "lock",
            "screen",
            "miniled",
            "brightness_up",
            "brightness_down",
            "fnlock",
            "calculator",
        ];

        public string[] Actions => ActionLabels;

        // M4 / ROG Key
        [ObservableProperty]
        private int _rogKeyIndex;

        // M3 / Aura Key
        [ObservableProperty]
        private int _auraKeyIndex;

        // Fn+F4
        [ObservableProperty]
        private int _fnF4Index;

        // Fn+V
        [ObservableProperty]
        private int _fnVIndex;

        // Fn+Numpad Enter
        [ObservableProperty]
        private int _fnEnterIndex;

        private bool _ignoreChange;

        partial void OnRogKeyIndexChanged(int value)
        {
            if (_ignoreChange) return;
            SaveKey("m4", value);
        }

        partial void OnAuraKeyIndexChanged(int value)
        {
            if (_ignoreChange) return;
            SaveKey("m3", value);
        }

        partial void OnFnF4IndexChanged(int value)
        {
            if (_ignoreChange) return;
            SaveKey("fnf4", value);
        }

        partial void OnFnVIndexChanged(int value)
        {
            if (_ignoreChange) return;
            SaveKey("fnv", value);
        }

        partial void OnFnEnterIndexChanged(int value)
        {
            if (_ignoreChange) return;
            SaveKey("fne", value);
        }

        private void SaveKey(string configKey, int index)
        {
            string actionValue = index >= 0 && index < ActionValues.Length ? ActionValues[index] : "";
            AppConfig.Set(configKey, actionValue);
            Task.Run(() =>
            {
                try
                {
                    Program.inputDispatcher?.RegisterKeys();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"Key rebind error: {ex.Message}");
                }
            });
        }

        private int FindActionIndex(string configKey, string defaultAction)
        {
            string action = AppConfig.GetString(configKey);
            if (string.IsNullOrEmpty(action)) action = defaultAction;
            int idx = Array.IndexOf(ActionValues, action);
            return idx >= 0 ? idx : 0;
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                RogKeyIndex = FindActionIndex("m4", "ghelper");
                AuraKeyIndex = FindActionIndex("m3", "aura");
                FnF4Index = FindActionIndex("fnf4", "micmute");
                FnVIndex = FindActionIndex("fnv", "visual");
                FnEnterIndex = FindActionIndex("fne", "calculator");
            }
            finally
            {
                _ignoreChange = false;
            }
        }
    }
}
