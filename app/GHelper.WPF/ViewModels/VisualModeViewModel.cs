using CommunityToolkit.Mvvm.ComponentModel;
using GHelper.Display;

namespace GHelper.WPF.ViewModels
{
    public partial class VisualModeViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _screenFrequency;

        [ObservableProperty]
        private int _maxFrequency;

        [ObservableProperty]
        private bool _screenAuto;

        [ObservableProperty]
        private string _currentRateText = "";

        partial void OnScreenFrequencyChanged(int value)
        {
            CurrentRateText = value > 0 ? value.ToString() : "";
        }

        [ObservableProperty]
        private int _colorTempIndex = 3; // Neutral (index 3 of 7)

        [ObservableProperty]
        private string[] _colorTempLabels = ["Warmest", "Warmer", "Warm", "Neutral", "Cold", "Colder", "Coldest"];

        [ObservableProperty]
        private string[] _frequencyLabels = ["Auto"];

        [ObservableProperty]
        private int _selectedFreqIndex;

        [ObservableProperty]
        private bool _isAutoFreqMode;

        [ObservableProperty]
        private string _gamutName = "Native";

        [ObservableProperty]
        private string[] _gamutOptions = ["Native"];

        [ObservableProperty]
        private int _selectedGamutIndex;

        [ObservableProperty]
        private bool _isOverdriveEnabled;

        [ObservableProperty]
        private bool _isScreenEnabled = true;

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isOverdriveToggle;

        [ObservableProperty]
        private bool _hasOverdrive;

        [ObservableProperty]
        private bool _hasMiniled;

        [ObservableProperty]
        private int _miniledMode;

        [ObservableProperty]
        private string[] _miniledLabels = ["Off", "On"];

        [ObservableProperty]
        private int _selectedMiniledIndex;

        private bool _ignoreChange;

        private readonly int[] _colorTempValues = [0, 15, 30, 50, 70, 85, 100];

        partial void OnScreenAutoChanged(bool value)
        {
            // Kept for backward compat — driven by SelectedFreqIndex now
        }

        partial void OnColorTempIndexChanged(int value)
        {
            if (_ignoreChange) return;
            if (value < 0 || value >= _colorTempValues.Length) return;

            int temp = _colorTempValues[value];
            int visual = AppConfig.Get("visual");
            Task.Run(() =>
            {
                try
                {
                    VisualControl.SetVisual((SplendidCommand)visual, temp);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Color temp error: " + ex.Message);
                }
            });
        }

        partial void OnSelectedGamutIndexChanged(int value)
        {
            if (_ignoreChange) return;
            // Map index to actual gamut enum values based on available modes
            var modes = VisualControl.GetGamutModes();
            if (modes.Count == 0) return;

            var keys = new List<SplendidGamut>(modes.Keys);
            if (value >= 0 && value < keys.Count)
            {
                Task.Run(() =>
                {
                    try
                    {
                        VisualControl.SetGamut((int)keys[value]);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("Gamut error: " + ex.Message);
                    }
                });
            }
        }

        partial void OnIsOverdriveToggleChanged(bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("no_overdrive", value ? 0 : 1);
            Task.Run(() =>
            {
                try
                {
                    ScreenControl.AutoScreen(true);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Overdrive toggle error: " + ex.Message);
                }
            });
        }

        partial void OnSelectedFreqIndexChanged(int value)
        {
            if (_ignoreChange) return;

            bool isAuto = (value == 0);
            IsAutoFreqMode = isAuto;

            if (isAuto)
            {
                // Auto: enable screen_auto and let the system manage refresh rate
                AppConfig.Set("screen_auto", 1);
                ScreenAuto = true;
                Task.Run(() =>
                {
                    try { ScreenControl.AutoScreen(true); }
                    catch (Exception ex) { Logger.WriteLine("Screen auto error: " + ex.Message); }
                });
            }
            else
            {
                // Manual: disable screen_auto, set the chosen rate
                AppConfig.Set("screen_auto", 0);
                ScreenAuto = false;

                // Parse the actual Hz value from the label (e.g., "120Hz" → 120, "240Hz+OD" → 240)
                int targetRate = ScreenControl.MAX_REFRESH;
                var labels = FrequencyLabels;
                if (value >= 0 && value < labels.Length)
                {
                    string label = labels[value].Replace("Hz", "").Replace("+OD", "").Trim();
                    if (int.TryParse(label, out int parsed) && parsed > 0)
                        targetRate = parsed;
                }

                bool overdrive = value == labels.Length - 1 && labels[value].Contains("+OD");
                Task.Run(() =>
                {
                    try { ScreenControl.SetScreen(targetRate, overdrive ? 1 : 0); }
                    catch (Exception ex) { Logger.WriteLine("Screen rate error: " + ex.Message); }
                });
            }
        }

        public void Initialize()
        {
            _ignoreChange = true;
            try
            {
                ScreenAuto = AppConfig.Is("screen_auto");
                RefreshCurrentRate();

                // Color temp
                int colorTemp = AppConfig.Get("color_temp", VisualControl.DefaultColorTemp);
                ColorTempIndex = FindClosestTempIndex(colorTemp);

                // Gamut modes (fast — reads hardware capability)
                var modes = VisualControl.GetGamutModes();
                if (modes.Count > 0)
                {
                    GamutOptions = modes.Values.Select(v =>
                        v.StartsWith("Gamut: ", StringComparison.OrdinalIgnoreCase) ? v.Substring(7) : v).ToArray();
                    int currentGamut = AppConfig.Get("gamut");
                    int idx = 0;
                    foreach (var kvp in modes)
                    {
                        if ((int)kvp.Key == currentGamut) { SelectedGamutIndex = idx; break; }
                        idx++;
                    }
                }
            }
            finally
            {
                _ignoreChange = false;
            }

            // Heavy: EnumDisplaySettings loop to list all refresh rates.
            // Run enumeration on background thread, marshal results back to UI.
            Task.Run(() => LoadFrequencyLabelsBackground());
        }

        private void LoadFrequencyLabelsBackground()
        {
            try
            {
                var current = new NativeDevMode();
                current.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeDevMode));
                if (!EnumDisplaySettings(null, -1, ref current)) return;

                int curWidth = current.dmPelsWidth;
                int curHeight = current.dmPelsHeight;

                var rates = new SortedSet<int>();
                var mode = new NativeDevMode();
                mode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeDevMode));

                for (int i = 0; EnumDisplaySettings(null, i, ref mode); i++)
                {
                    if (mode.dmPelsWidth == curWidth && mode.dmPelsHeight == curHeight && mode.dmDisplayFrequency > 0)
                        rates.Add(mode.dmDisplayFrequency);
                }

                if (rates.Count == 0) return;

                var labels = new List<string> { "Auto" };
                foreach (int rate in rates)
                    labels.Add($"{rate}Hz");

                int currentRate = ScreenFrequency > 0 ? ScreenFrequency : current.dmDisplayFrequency;
                bool screenAuto = ScreenAuto;

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _ignoreChange = true;
                    try
                    {
                        FrequencyLabels = labels.ToArray();
                        if (screenAuto)
                        {
                            SelectedFreqIndex = 0;
                            IsAutoFreqMode = true;
                        }
                        else
                        {
                            int bestIdx = 1;
                            for (int i = 1; i < labels.Count; i++)
                            {
                                string label = labels[i].Replace("Hz", "").Trim();
                                if (int.TryParse(label, out int hz) && hz == currentRate)
                                {
                                    bestIdx = i;
                                    break;
                                }
                            }
                            SelectedFreqIndex = bestIdx;
                            IsAutoFreqMode = false;
                        }
                    }
                    finally
                    {
                        _ignoreChange = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Frequency labels background load error: " + ex.Message);
            }
        }

        public void SetScreen(bool enabled, bool auto, int freq, int maxFreq, int overdrive, bool overdriveSetting)
        {
            _ignoreChange = true;
            try
            {
                IsScreenEnabled = enabled;
                ScreenAuto = auto;
                ScreenFrequency = freq;
                MaxFrequency = maxFreq;
                IsOverdriveEnabled = overdriveSetting && overdrive > 0;
                HasOverdrive = overdriveSetting;
                IsOverdriveToggle = !AppConfig.Is("no_overdrive");

                // Build frequency labels: Auto + manual rates
                int minRate = ScreenControl.MIN_RATE;
                var labels = new List<string> { "Auto" };

                if (maxFreq > minRate)
                {
                    labels.Add($"{minRate}Hz");
                    string maxLabel = overdriveSetting ? $"{maxFreq}Hz+OD" : $"{maxFreq}Hz";
                    labels.Add(maxLabel);
                }
                else if (maxFreq > 0)
                {
                    labels.Add($"{maxFreq}Hz");
                }

                FrequencyLabels = labels.ToArray();

                // Set selected index: Auto=0, minRate=1, maxRate=2
                if (auto)
                {
                    SelectedFreqIndex = 0;
                    IsAutoFreqMode = true;
                }
                else if (freq <= minRate)
                {
                    SelectedFreqIndex = 1;
                    IsAutoFreqMode = false;
                }
                else
                {
                    SelectedFreqIndex = labels.Count - 1;
                    IsAutoFreqMode = false;
                }
            }
            finally
            {
                _ignoreChange = false;
            }
        }

        public void RefreshCurrentRate()
        {
            try
            {
                // Read current display frequency from Windows API
                var devMode = new NativeDevMode();
                devMode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeDevMode));
                if (EnumDisplaySettings(null, -1, ref devMode)) // ENUM_CURRENT_SETTINGS = -1
                {
                    if (devMode.dmDisplayFrequency > 0)
                        ScreenFrequency = devMode.dmDisplayFrequency;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Refresh rate read error: " + ex.Message);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref NativeDevMode devMode);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private struct NativeDevMode
        {
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        private void BuildFrequencyLabels()
        {
            try
            {
                // Get current resolution
                var current = new NativeDevMode();
                current.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeDevMode));
                if (!EnumDisplaySettings(null, -1, ref current)) return;

                int curWidth = current.dmPelsWidth;
                int curHeight = current.dmPelsHeight;

                // Enumerate all modes to find available refresh rates at current resolution
                var rates = new SortedSet<int>();
                var mode = new NativeDevMode();
                mode.dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeDevMode));

                for (int i = 0; EnumDisplaySettings(null, i, ref mode); i++)
                {
                    if (mode.dmPelsWidth == curWidth && mode.dmPelsHeight == curHeight && mode.dmDisplayFrequency > 0)
                        rates.Add(mode.dmDisplayFrequency);
                }

                if (rates.Count == 0) return;

                // Build labels: Auto + each available rate
                var labels = new List<string> { "Auto" };
                foreach (int rate in rates)
                    labels.Add($"{rate}Hz");

                FrequencyLabels = labels.ToArray();

                // Set selected index based on current state
                if (ScreenAuto)
                {
                    SelectedFreqIndex = 0;
                    IsAutoFreqMode = true;
                }
                else
                {
                    int currentRate = ScreenFrequency > 0 ? ScreenFrequency : current.dmDisplayFrequency;
                    int bestIdx = 1; // default to first manual rate
                    for (int i = 1; i < labels.Count; i++)
                    {
                        string label = labels[i].Replace("Hz", "").Trim();
                        if (int.TryParse(label, out int hz) && hz == currentRate)
                        {
                            bestIdx = i;
                            break;
                        }
                    }
                    SelectedFreqIndex = bestIdx;
                    IsAutoFreqMode = false;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Build frequency labels error: " + ex.Message);
            }
        }

        private int FindClosestTempIndex(int temp)
        {
            int best = 0;
            int bestDist = int.MaxValue;
            for (int i = 0; i < _colorTempValues.Length; i++)
            {
                int dist = Math.Abs(_colorTempValues[i] - temp);
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }
    }
}
