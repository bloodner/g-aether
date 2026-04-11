using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GHelper.UI
{
    public class RForm : Form
    {

        public static Color colorEco = Color.FromArgb(255, 6, 180, 138);
        public static Color colorStandard = Color.FromArgb(255, 58, 174, 239);
        public static Color colorTurbo = Color.FromArgb(255, 255, 32, 32);
        public static Color colorCustom = Color.FromArgb(255, 255, 128, 0);
        public static Color colorGray = Color.FromArgb(255, 168, 168, 168);


        public static Color buttonMain;
        public static Color buttonSecond;

        public static Color formBack;
        public static Color foreMain;
        public static Color borderMain;
        public static Color chartMain;
        public static Color chartGrid;

        public static Color cardBackground;
        public static Color cardBackgroundSecondary;

        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool CheckSystemDarkModeStatus();

        [DllImport("DwmApi")]
        private static extern int DwmSetWindowAttribute(nint hwnd, int attr, int[] attrValue, int attrSize);

        [DllImport("DwmApi")]
        private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int Left, Right, Top, Bottom; }

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public static bool IsMicaSupported => Environment.OSVersion.Version.Build >= 22000;
        public bool MicaEnabled { get; private set; } = false;

        public bool darkTheme = false;
        protected override CreateParams CreateParams
        {
            get
            {
                var parms = base.CreateParams;
                parms.Style &= ~0x02000000;  // Turn off WS_CLIPCHILDREN
                parms.ClassStyle &= ~0x00020000;
                return parms;
            }
        }
        public static void InitColors(bool darkTheme)
        {
            if (darkTheme)
            {
                buttonMain = Color.FromArgb(255, 55, 55, 55);
                buttonSecond = Color.FromArgb(255, 38, 38, 38);

                formBack = Color.FromArgb(255, 28, 28, 28);
                foreMain = Color.FromArgb(255, 240, 240, 240);
                borderMain = Color.FromArgb(255, 50, 50, 50);

                chartMain = Color.FromArgb(255, 35, 35, 35);
                chartGrid = Color.FromArgb(255, 70, 70, 70);

                cardBackground = Color.FromArgb(255, 32, 32, 32);
                cardBackgroundSecondary = Color.FromArgb(255, 30, 30, 30);
            }
            else
            {
                buttonMain = SystemColors.ControlLightLight;
                buttonSecond = SystemColors.ControlLight;

                formBack = SystemColors.Control;
                foreMain = SystemColors.ControlText;
                borderMain = Color.LightGray;

                chartMain = SystemColors.ControlLightLight;
                chartGrid = Color.LightGray;

                cardBackground = Color.FromArgb(255, 251, 251, 251);
                cardBackgroundSecondary = Color.FromArgb(255, 245, 245, 245);
            }
        }

        protected void EnableMica()
        {
            if (!IsMicaSupported) return;
            try
            {
                // Mica backdrop on the title bar
                DwmSetWindowAttribute(Handle, DWMWA_SYSTEMBACKDROP_TYPE, new[] { 2 }, 4);
                MicaEnabled = true;
            }
            catch
            {
                MicaEnabled = false;
            }
        }

        private static bool IsDarkTheme()
        {
            string? uiMode = AppConfig.GetString("ui_mode");

            if (uiMode is not null && uiMode.ToLower() == "dark")
            {
                return true;
            }

            if (uiMode is not null && uiMode.ToLower() == "light")
            {
                return false;
            }

            if (uiMode is not null && uiMode.ToLower() == "windows")
            {
                return CheckSystemDarkModeStatus();
            }

            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var registryValueObject = key?.GetValue("AppsUseLightTheme");

            if (registryValueObject == null) return false;
            return (int)registryValueObject <= 0;
        }

        public bool InitTheme(bool setDPI = false)
        {
            bool newDarkTheme = IsDarkTheme();
            bool changed = darkTheme != newDarkTheme;
            darkTheme = newDarkTheme;

            InitColors(darkTheme);

            if (setDPI)
            {
                ControlHelper.Resize(this);
                DwmSetWindowAttribute(Handle, 20, new[] { darkTheme ? 1 : 0 }, 4);
                EnableMica();
                ControlHelper.Adjust(this, true);
                this.Invalidate();
            }
            else if (changed)
            {
                DwmSetWindowAttribute(Handle, 20, new[] { darkTheme ? 1 : 0 }, 4);
                EnableMica();
                ControlHelper.Adjust(this, changed);
                this.Invalidate();
            }


            return changed;

        }

    }
}
