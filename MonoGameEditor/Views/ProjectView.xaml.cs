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

        private System.Windows.Point _startPoint = new System.Windows.Point();

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MonoGameEditor.ViewModels.ProjectViewModel vm && e.NewValue is MonoGameEditor.ViewModels.DirectoryItemViewModel dir)
            {
                vm.SelectedDirectory = dir;
            }
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void ListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                System.Windows.Point mousePos = e.GetPosition(null);
                System.Windows.Vector diff = _startPoint - mousePos;

                if (System.Math.Abs(diff.X) > System.Windows.SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > System.Windows.SystemParameters.MinimumVerticalDragDistance)
                {
                    System.Windows.Controls.ListBox listBox = sender as System.Windows.Controls.ListBox;
                    if (listBox != null && listBox.SelectedItem is MonoGameEditor.ViewModels.FileItemViewModel fileItem)
                    {
                        var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new string[] { fileItem.FullPath });
                        System.Windows.DragDrop.DoDragDrop(listBox, data, System.Windows.DragDropEffects.Copy);
                    }
                }
            }
        }

        private void ListBox_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Select on mouse UP, not DOWN - prevents Inspector changes during drag
            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox == null) return;

            var item = GetItemFromPoint(listBox, e.GetPosition(listBox));
            if (item != null && DataContext is MonoGameEditor.ViewModels.ProjectViewModel vm)
            {
                vm.SelectedGridItem = item;
            }
        }

        private object GetItemFromPoint(System.Windows.Controls.ListBox listBox, System.Windows.Point point)
        {
            var element = listBox.InputHitTest(point) as UIElement;
            while (element != null)
            {
                if (element is System.Windows.Controls.ListBoxItem)
                    return ((System.Windows.Controls.ListBoxItem)element).DataContext;
                element = System.Windows.Media.VisualTreeHelper.GetParent(element) as UIElement;
            }
            return null;
        }
        
        private void ListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is ViewModels.FileItemViewModel fileItem)
            {
                // Check if it's a C# script file
                if (fileItem.FullPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Open as docked document in main editor
                    ViewModels.MainViewModel.Instance?.OpenScriptEditor(fileItem.FullPath);
                    e.Handled = true;
                }
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
                if ( d is Control control)
                {
                    control.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new System.Action(() =>
                    {
                        control.Focus();
                        if (control is TextBox textBox)
                        {
                            textBox.SelectAll();
                        }
                    }));
                }
            }
        }
    }
}
