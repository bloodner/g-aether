using System.Windows.Controls;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views.Panels
{
    public partial class KeyboardPanel : UserControl
    {
        public KeyboardPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AuraColorWheel.Color1Changed += (_, _) =>
            {
                if (DataContext is KeyboardViewModel vm)
                    vm.OnColor1Changed();
            };
            AuraColorWheel.Color2Changed += (_, _) =>
            {
                if (DataContext is KeyboardViewModel vm)
                    vm.OnColor2Changed();
            };
        }
    }
}
