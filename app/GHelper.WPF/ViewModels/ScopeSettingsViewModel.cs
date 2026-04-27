using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.WPF.Services;

namespace GHelper.WPF.ViewModels
{
    /// <summary>
    /// Backs the G-Scope Settings window. Independent from the gadget's
    /// settings on ExtraSettingsViewModel — Scope and Floating each have
    /// their own AppConfig namespace.
    /// </summary>
    public partial class ScopeSettingsViewModel : ObservableObject
    {
        private bool _ignoreChange;

        // ---- Visible tiles ----
        [ObservableProperty] private bool _showCpuTemp = true;
        [ObservableProperty] private bool _showDgpuTemp = true;
        [ObservableProperty] private bool _showCpuUse = true;
        [ObservableProperty] private bool _showDgpuUse = true;
        [ObservableProperty] private bool _showPower = true;
        [ObservableProperty] private bool _showBattery = true;
        [ObservableProperty] private bool _showCpuFan = true;
        [ObservableProperty] private bool _showGpuFan = true;

        // ---- Accent ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsAccentMulti), nameof(IsAccentBlue), nameof(IsAccentPurple),
            nameof(IsAccentGreen), nameof(IsAccentOrange), nameof(IsAccentRed),
            nameof(IsAccentWhite), nameof(IsAccentDark))]
        private string _scopeAccent = "multi";

        public bool IsAccentMulti => ScopeAccent == "multi";
        public bool IsAccentBlue => ScopeAccent == "blue";
        public bool IsAccentPurple => ScopeAccent == "purple";
        public bool IsAccentGreen => ScopeAccent == "green";
        public bool IsAccentOrange => ScopeAccent == "orange";
        public bool IsAccentRed => ScopeAccent == "red";
        public bool IsAccentWhite => ScopeAccent == "white";
        public bool IsAccentDark => ScopeAccent == "dark";

        // ---- Size ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsScopeSizeCompact), nameof(IsScopeSizeRegular), nameof(IsScopeSizeLarge))]
        private string _scopeSize = "regular";

        public bool IsScopeSizeCompact => ScopeSize == "compact";
        public bool IsScopeSizeRegular => ScopeSize == "regular";
        public bool IsScopeSizeLarge => ScopeSize == "large";

        // ---- History window ----
        [ObservableProperty]
        [NotifyPropertyChangedFor(
            nameof(IsHistory60s), nameof(IsHistory5min), nameof(IsHistory15min))]
        private string _scopeHistoryWindow = "60s";

        public bool IsHistory60s => ScopeHistoryWindow == "60s";
        public bool IsHistory5min => ScopeHistoryWindow == "5min";
        public bool IsHistory15min => ScopeHistoryWindow == "15min";

        public ScopeSettingsViewModel()
        {
            _ignoreChange = true;
            ShowCpuTemp = AppConfig.Get("scope_show_cpu_temp", 1) == 1;
            ShowDgpuTemp = AppConfig.Get("scope_show_dgpu_temp", 1) == 1;
            ShowCpuUse = AppConfig.Get("scope_show_cpu_use", 1) == 1;
            ShowDgpuUse = AppConfig.Get("scope_show_dgpu_use", 1) == 1;
            ShowPower = AppConfig.Get("scope_show_power", 1) == 1;
            ShowBattery = AppConfig.Get("scope_show_battery", 1) == 1;
            ShowCpuFan = AppConfig.Get("scope_show_cpu_fan", 1) == 1;
            ShowGpuFan = AppConfig.Get("scope_show_gpu_fan", 1) == 1;
            ScopeAccent = AppConfig.GetString("scope_accent") ?? "multi";
            ScopeSize = AppConfig.GetString("scope_size") ?? "regular";
            ScopeHistoryWindow = AppConfig.GetString("scope_history_window") ?? "60s";
            _ignoreChange = false;
        }

        // ---- Visible-tile change handlers ----
        partial void OnShowCpuTempChanged(bool value) => SaveBool("scope_show_cpu_temp", value);
        partial void OnShowDgpuTempChanged(bool value) => SaveBool("scope_show_dgpu_temp", value);
        partial void OnShowCpuUseChanged(bool value) => SaveBool("scope_show_cpu_use", value);
        partial void OnShowDgpuUseChanged(bool value) => SaveBool("scope_show_dgpu_use", value);
        partial void OnShowPowerChanged(bool value) => SaveBool("scope_show_power", value);
        partial void OnShowBatteryChanged(bool value) => SaveBool("scope_show_battery", value);
        partial void OnShowCpuFanChanged(bool value) => SaveBool("scope_show_cpu_fan", value);
        partial void OnShowGpuFanChanged(bool value) => SaveBool("scope_show_gpu_fan", value);

        partial void OnScopeAccentChanged(string value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("scope_accent", value);
            ScopeService.ApplySettings();
        }

        partial void OnScopeSizeChanged(string value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("scope_size", value);
            ScopeService.ApplySettings();
        }

        partial void OnScopeHistoryWindowChanged(string value)
        {
            if (_ignoreChange) return;
            AppConfig.Set("scope_history_window", value);
            ScopeService.ApplySettings();
        }

        private void SaveBool(string key, bool value)
        {
            if (_ignoreChange) return;
            AppConfig.Set(key, value ? 1 : 0);
            ScopeService.ApplySettings();
        }

        [RelayCommand]
        private void SetScopeAccent(string accent) => ScopeAccent = accent;

        [RelayCommand]
        private void SetScopeSize(string size) => ScopeSize = size;

        [RelayCommand]
        private void SetScopeHistory(string window) => ScopeHistoryWindow = window;
    }
}
