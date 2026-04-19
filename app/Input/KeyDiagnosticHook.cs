using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GHelper.Input
{
    /// <summary>
    /// Optional low-level keyboard hook for diagnosing what key combos a hardware key
    /// actually sends (e.g. the Copilot key). Every keystroke is logged with its virtual
    /// key code and modifier state so we can identify unknown keys.
    ///
    /// Enable by setting "key_debug" = 1 in the AppConfig. The hook observes only —
    /// it never swallows events.
    /// </summary>
    public static class KeyDiagnosticHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;

        private static IntPtr _hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc? _proc;
        private static bool _active;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static void Start()
        {
            if (_active) return;
            _proc = HookCallback;
            _hookId = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
            if (_hookId == IntPtr.Zero)
            {
                Logger.WriteLine("[KeyDiag] Failed to install low-level hook, err=" + Marshal.GetLastWin32Error());
                return;
            }
            _active = true;
            Logger.WriteLine("[KeyDiag] Diagnostic hook active — every keystroke will be logged. Disable with key_debug=0 in config.");
        }

        public static void Stop()
        {
            if (!_active) return;
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _active = false;
            _proc = null;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    try
                    {
                        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                        bool win = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                        bool shift = (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
                        bool ctrl = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
                        bool alt = (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 || (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;

                        var mods = new System.Text.StringBuilder();
                        if (win) mods.Append("Win+");
                        if (shift) mods.Append("Shift+");
                        if (ctrl) mods.Append("Ctrl+");
                        if (alt) mods.Append("Alt+");

                        string keyName = ((System.Windows.Forms.Keys)data.vkCode).ToString();
                        Logger.WriteLine($"[KeyDiag] vk=0x{data.vkCode:X2} ({data.vkCode}) scan=0x{data.scanCode:X2} flags=0x{data.flags:X2} {mods}{keyName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine("[KeyDiag] hook callback error: " + ex.Message);
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
