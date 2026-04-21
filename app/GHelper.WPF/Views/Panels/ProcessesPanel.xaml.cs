using System.Windows;
using System.Windows.Controls;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views.Panels
{
    public partial class ProcessesPanel : UserControl
    {
        public ProcessesPanel()
        {
            InitializeComponent();
            // Start/stop the 2-second refresh ticker based on panel visibility so
            // we don't enumerate processes when a different nav page is showing.
            IsVisibleChanged += OnVisibleChanged;
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is not ProcessesViewModel vm) return;
            if ((bool)e.NewValue) vm.Start();
            else vm.Stop();
        }
    }
}
