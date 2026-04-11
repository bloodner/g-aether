using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace GHelper.WPF.Services
{
    public static class ThemeService
    {
        public static Color AccentColor { get; private set; } = Color.FromRgb(0x00, 0x78, 0xD4);

        // Mode colors matching the WinForms RForm static colors
        public static Color ColorSilent { get; } = Color.FromRgb(0x4B, 0xD8, 0xC8);   // Cyan
        public static Color ColorBalanced { get; } = Color.FromRgb(0x4B, 0xD8, 0x4B);  // Green
        public static Color ColorTurbo { get; } = Color.FromRgb(0xFF, 0xD8, 0x00);     // Yellow
        public static Color ColorFansPower { get; } = Color.FromRgb(0xFF, 0x44, 0x44); // Red
        public static Color ColorEco { get; } = Color.FromRgb(0x4B, 0xD8, 0x4B);
        public static Color ColorStandard { get; } = Color.FromRgb(0x4B, 0xC8, 0xFF);

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
