using System.Runtime.InteropServices;

namespace GHelper.Input
{
    /// <summary>
    /// Low-level keyboard hook that intercepts and suppresses the Copilot key
    /// (Win+Shift+F23 and Win+Shift+LaunchApplication1) before Windows' shell
    /// can see it.
    ///
    /// Why: RegisterHotKey claims WM_HOTKEY for us, but Windows 11 24H2's built-in
    /// Copilot key handler ALSO runs in parallel — on rapid presses it treats the
    /// sequence as "user wants to configure" and opens ms-settings:keyboard-copilot.
    /// Eating the keystrokes at the LL hook layer prevents that without affecting
    /// any other key. On detection, KeyProcess("copilot") is dispatched with
    /// debouncing (F23 and LaunchApp1 fire within ~1ms of each other).
    /// </summary>
    public static class CopilotKeyHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_F23 = 0x86;
        private const int VK_LAUNCH_APP1 = 0xB6;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;

        private const int DEBOUNCE_MS = 250;

        private static IntPtr _hookId = IntPtr.Zero;
        private static LowLevelKeyboardProc? _proc;
        private static bool _active;
        private static long _lastActionMs;

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

        public static bool Start()
        {
            if (_active) return true;
            _proc = HookCallback;
            _hookId = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
            if (_hookId == IntPtr.Zero)
            {
                Logger.WriteLine("[Copilot] Failed to install low-level hook, err=" + Marshal.GetLastWin32Error());
                _proc = null;
                return false;
            }
            _active = true;
            Logger.WriteLine("[Copilot] LL hook active — intercepting Win+Shift+F23 / LaunchApp1");
            return true;
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
            if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vk = data.vkCode;

                if (vk != VK_F23 && vk != VK_LAUNCH_APP1)
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);

                bool win = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
                if (!win || !shift)
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);

                int msg = wParam.ToInt32();
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (now - _lastActionMs > DEBOUNCE_MS)
                    {
                        _lastActionMs = now;
                        // Dispatch off the hook thread — callbacks must return fast.
                        Task.Run(() => InputDispatcher.KeyProcess("copilot"));
                    }
                }

                // Suppress both down AND up for the Copilot VKs so Windows never sees them.
                return (IntPtr)1;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("[Copilot] hook error: " + ex.Message);
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
