using System.Windows;

namespace GHelper.WPF.Views
{
    /// <summary>
    /// Attached property used by the mode-badge strip to mark the pill whose
    /// panel is currently displayed. The button template reacts via a trigger
    /// to keep the underline lit and tint the background.
    /// </summary>
    public static class ModeBadgeProps
    {
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.RegisterAttached(
                "IsActive",
                typeof(bool),
                typeof(ModeBadgeProps),
                new PropertyMetadata(false));

        public static bool GetIsActive(DependencyObject d) => (bool)d.GetValue(IsActiveProperty);
        public static void SetIsActive(DependencyObject d, bool value) => d.SetValue(IsActiveProperty, value);
    }
}
