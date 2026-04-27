using System.Windows;
using System.Windows.Controls;
using GHelper.WPF.Views;

namespace GHelper.WPF.Views.Panels
{
    public partial class MonitorPanel : UserControl
    {
        public MonitorPanel()
        {
            InitializeComponent();
        }

        private void ScopeSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ScopeSettingsWindow.Show(Window.GetWindow(this));
        }
    }
}
