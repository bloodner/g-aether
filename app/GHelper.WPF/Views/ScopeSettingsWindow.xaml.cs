using System.Windows;
using System.Windows.Input;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views
{
    /// <summary>
    /// Modal page for configuring the G-Scope panel. Mirrors GadgetSettingsWindow
    /// but uses its own ScopeSettingsViewModel instance — gadget and panel
    /// settings are independent by design.
    /// </summary>
    public partial class ScopeSettingsWindow : Window
    {
        public ScopeSettingsWindow()
        {
            InitializeComponent();
        }

        public static void Show(Window? owner)
        {
            var win = new ScopeSettingsWindow { DataContext = new ScopeSettingsViewModel() };
            if (owner != null) win.Owner = owner;
            win.ShowDialog();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
