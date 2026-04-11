using System.Windows.Controls;
using GHelper.WPF.ViewModels;

namespace GHelper.WPF.Views.Panels
{
    public partial class FansPowerPanel : UserControl
    {
        public FansPowerPanel()
        {
            InitializeComponent();

            CpuCurveEditor.CurveChanged += (s, e) =>
            {
                if (DataContext is FansPowerViewModel vm)
                    vm.OnCpuCurveChanged();
            };

            GpuCurveEditor.CurveChanged += (s, e) =>
            {
                if (DataContext is FansPowerViewModel vm)
                    vm.OnGpuCurveChanged();
            };
        }
    }
}
