using System.Windows;
using System.Windows.Input;
using GHelper.WPF.Services;

namespace GHelper.WPF.Views
{
    /// <summary>
    /// Modal key-combo capture dialog. Listens for key events on the window,
    /// builds up a binding while the user holds modifiers + presses a trigger
    /// key, then enables Accept once both a trigger key and at least one
    /// modifier are present (modifiers alone are not a hotkey).
    /// </summary>
    public partial class HotkeyCaptureWindow : Window
    {
        public HotkeyBinding? Result { get; private set; }

        private HotkeyBinding _pending = new();

        public HotkeyCaptureWindow(HotkeyAction action)
        {
            InitializeComponent();
            _pending.Action = action;
            ActionLabel.Text = GlobalHotkeyService.ActionLabel(action);
            PreviewKeyDown += OnPreviewKeyDown;

            // Release every global hotkey while we're capturing so the keypress
            // we're trying to record doesn't trigger its own handler. Restored
            // on close regardless of accept/cancel.
            Loaded += (_, _) => GlobalHotkeyService.Suspend();
            Closed += (_, _) => GlobalHotkeyService.Resume();
        }

        /// <summary>
        /// Opens the dialog and returns the captured binding, or null if the
        /// user cancelled or the combo couldn't be registered.
        /// </summary>
        public static HotkeyBinding? Capture(Window? owner, HotkeyAction action)
        {
            var win = new HotkeyCaptureWindow(action);
            if (owner != null) win.Owner = owner;
            return win.ShowDialog() == true ? win.Result : null;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // Esc cancels — but only if we've not yet captured anything. After
            // first keystroke the user has to use Cancel button (avoids accidents).
            if (e.Key == Key.Escape && _pending.VirtualKey == 0)
            {
                DialogResult = false;
                Close();
                return;
            }

            // Read modifiers live from Keyboard so Shift/Ctrl held for other
            // purposes don't pollute the binding. WPF's ModifierKeys.Windows is
            // unreliable (the OS often consumes the Win key state before we see
            // it), so we check the physical LWin/RWin keys directly.
            var mods = HotkeyModifiers.None;
            var wpfMods = Keyboard.Modifiers;
            if ((wpfMods & System.Windows.Input.ModifierKeys.Control) != 0) mods |= HotkeyModifiers.Control;
            if ((wpfMods & System.Windows.Input.ModifierKeys.Alt) != 0) mods |= HotkeyModifiers.Alt;
            if ((wpfMods & System.Windows.Input.ModifierKeys.Shift) != 0) mods |= HotkeyModifiers.Shift;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= HotkeyModifiers.Win;

            _pending.Modifiers = mods;

            // The trigger key has to be a non-modifier key. Ignore presses that
            // are themselves modifier keys (the user is still chording).
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifierKey(actualKey))
            {
                UpdateDisplay();
                return;
            }

            _pending.VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(actualKey);
            UpdateDisplay();
        }

        private static bool IsModifierKey(Key key) => key switch
        {
            Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin
                or Key.System => true,
            _ => false,
        };

        private void UpdateDisplay()
        {
            string? label = GlobalHotkeyService.FormatBinding(_pending);
            if (label == null)
            {
                // Only modifiers so far.
                ComboText.Text = "Press a combination now";
                AcceptButton.IsEnabled = false;
                return;
            }
            ComboText.Text = label;
            ComboText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            // Require at least one modifier so users can't accidentally assign a
            // bare letter globally (would swallow the key everywhere).
            AcceptButton.IsEnabled = _pending.Modifiers != HotkeyModifiers.None;
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            Result = _pending;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
