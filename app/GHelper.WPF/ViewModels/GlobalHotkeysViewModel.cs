using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHelper.WPF.Services;
using GHelper.WPF.Views;

namespace GHelper.WPF.ViewModels
{
    public partial class GlobalHotkeysViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<HotkeyRow> _rows = new();

        public GlobalHotkeysViewModel()
        {
            Refresh();
            GlobalHotkeyService.BindingsChanged += Refresh;
        }

        /// <summary>Rebuild rows from the service's current bindings.</summary>
        public void Refresh()
        {
            // Preserve warnings across rebuilds so "conflict" labels don't vanish when
            // the user opens the panel.
            var warnings = Rows.ToDictionary(r => r.Action, r => r.Warning);

            Rows.Clear();
            foreach (var action in GlobalHotkeyService.AllActions)
            {
                var binding = GlobalHotkeyService.Bindings.FirstOrDefault(b => b.Action == action);
                var row = new HotkeyRow(action, binding);
                if (warnings.TryGetValue(action, out var warn))
                    row.Warning = warn;
                Rows.Add(row);
            }
        }

        [RelayCommand]
        private void SetHotkey(HotkeyAction action)
        {
            var owner = Application.Current?.MainWindow;
            var binding = HotkeyCaptureWindow.Capture(owner, action);
            if (binding == null) return;

            // Take current bindings, drop any previous entry for this action, append the new one.
            var updated = GlobalHotkeyService.Bindings
                .Where(b => b.Action != action)
                .Append(binding)
                .ToList();

            GlobalHotkeyService.SetBindings(updated);

            // SetBindings re-registers everything and raises BindingsChanged which
            // rebuilds rows. Check whether our new combo actually registered — if
            // not, flag a collision warning inline so the user can pick something else.
            var row = Rows.FirstOrDefault(r => r.Action == action);
            if (row != null)
            {
                bool ok = GlobalHotkeyService.TryRegister(binding);
                // TryRegister is idempotent — re-registering an already-registered combo
                // succeeds. A false here means the OS refused: someone else owns it.
                row.Warning = ok ? null : "Combo in use by another app";
            }
        }

        [RelayCommand]
        private void ClearHotkey(HotkeyAction action)
        {
            var updated = GlobalHotkeyService.Bindings
                .Where(b => b.Action != action)
                .ToList();
            GlobalHotkeyService.SetBindings(updated);
        }
    }

    public partial class HotkeyRow : ObservableObject
    {
        [ObservableProperty] private string _actionLabel = "";
        [ObservableProperty] private string _formattedBinding = "Not set";
        [ObservableProperty] private bool _hasBinding;
        [ObservableProperty] private string? _warning;

        public HotkeyAction Action { get; }

        public HotkeyRow(HotkeyAction action, HotkeyBinding? binding)
        {
            Action = action;
            ActionLabel = GlobalHotkeyService.ActionLabel(action);
            string? label = GlobalHotkeyService.FormatBinding(binding);
            FormattedBinding = label ?? "Not set";
            HasBinding = label != null;
        }
    }
}
