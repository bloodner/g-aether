using System.Windows;
using System.Windows.Input;

namespace GHelper.WPF.Views
{
    /// <summary>
    /// Modal page for configuring the floating gadget. Hosts the visibility
    /// toggles, appearance controls, and behavior settings that used to live
    /// inline in the main Settings panel. Reuses the parent's
    /// ExtraSettingsViewModel as DataContext so all the bindings just work.
    /// </summary>
    public partial class GadgetSettingsWindow : Window
    {
        public GadgetSettingsWindow()
        {
            InitializeComponent();
        }

        public static void Show(Window? owner, object viewModel)
        {
            var win = new GadgetSettingsWindow { DataContext = viewModel };
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
