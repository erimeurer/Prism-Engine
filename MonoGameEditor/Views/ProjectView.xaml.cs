using System.Windows;
using System.Windows.Controls;

namespace MonoGameEditor.Views
{
    public partial class ProjectView : UserControl
    {
        public ProjectView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MonoGameEditor.ViewModels.ProjectViewModel vm && e.NewValue is MonoGameEditor.ViewModels.DirectoryItemViewModel dir)
            {
                vm.SelectedDirectory = dir;
            }
        }
    }

    public static class FocusExtension
    {
        public static readonly DependencyProperty IsFocusedProperty =
            DependencyProperty.RegisterAttached("IsFocused", typeof(bool), typeof(FocusExtension), new UIPropertyMetadata(false, OnIsFocusedChanged));

        public static bool GetIsFocused(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsFocusedProperty);
        }

        public static void SetIsFocused(DependencyObject obj, bool value)
        {
            obj.SetValue(IsFocusedProperty, value);
        }

        private static void OnIsFocusedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                if (d is TextBox textBox)
                {
                    textBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    }));
                }
                else if (d is Control control)
                {
                    control.Focus();
                }
            }
        }
    }
}
