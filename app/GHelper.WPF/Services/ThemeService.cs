using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace GHelper.WPF.Services
{
    public static class ThemeService
    {
        public static Color AccentColor { get; private set; } = Color.FromRgb(0x00, 0x78, 0xD4);

        // ===== Canonical mode-color palette =====
        // Any surface that needs to communicate "which mode am I in" at a glance
        // (tray icon, status bar badges, mode-change toasts) pulls from here so
        // we define the meaning once and every channel stays in sync.
        //
        // Chrome surfaces (section headers, selected pills, mode arcs) should keep
        // using AccentBrush — mode colors are for at-a-glance identity, not decoration.

        // Performance modes
        public static Color ColorSilent { get; } = Color.FromRgb(0x4B, 0xD8, 0xC8);  // cool cyan-teal — quiet
        public static Color ColorBalanced => AccentColor;                              // default → Windows accent
        public static Color ColorTurbo { get; } = Color.FromRgb(0xFF, 0x6B, 0x35);   // hot orange

        // GPU modes
        public static Color ColorEco { get; } = Color.FromRgb(0x4C, 0xC9, 0x5E);     // power-saving green
        public static Color ColorStandard => AccentColor;                              // default → Windows accent
        public static Color ColorUltimate { get; } = Color.FromRgb(0xFF, 0x6B, 0x35); // same hot as Turbo (both = max perf)
        public static Color ColorOptimized { get; } = Color.FromRgb(0xC6, 0x78, 0xF0); // adaptive magenta-violet

        // Status / warnings
        public static Color ColorFansPower { get; } = Color.FromRgb(0xFF, 0x44, 0x44); // Red

        public static void Initialize()
        {
            ReadAccentColor();
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                    ReadAccentColor();
            };
        }

        private static void ReadAccentColor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is int argb)
                {
                    byte a = (byte)(argb >> 24);
                    byte b = (byte)(argb >> 16);
                    byte g = (byte)(argb >> 8);
                    byte r = (byte)(argb);
                    AccentColor = Color.FromArgb(a, r, g, b);
                }
            }
            catch { }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (Application.Current?.Resources != null)
                {
                    var accentDim = Color.FromArgb(0x30, AccentColor.R, AccentColor.G, AccentColor.B);
                    Application.Current.Resources["AccentColor"] = AccentColor;
                    Application.Current.Resources["AccentDimColor"] = accentDim;
                    Application.Current.Resources["AccentBrush"] = new SolidColorBrush(AccentColor);
                    Application.Current.Resources["AccentDimBrush"] = new SolidColorBrush(accentDim);
                }
            });
        }

        public static bool IsDarkTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int v)
                    return v == 0;
            }
            catch { }
            return true;
        }
    }
}
