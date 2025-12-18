using System.Windows;
using System.Windows.Controls;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Views
{
    public partial class ConsoleView : UserControl
    {
        public ConsoleView()
        {
            InitializeComponent();
            DataContext = MonoGameEditor.ViewModels.ConsoleViewModel.Instance;
            Loaded += ConsoleView_Loaded;
        }

        private void ConsoleView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (LogListBox.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += (s, args) =>
                {
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    {
                        var scrollViewer = GetScrollViewer(LogListBox);
                        scrollViewer?.ScrollToBottom();
                    }
                };
            }
        }

        private ScrollViewer? GetScrollViewer(System.Windows.DependencyObject o)
        {
            if (o is ScrollViewer sv) return sv;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConsoleViewModel vm)
            {
                vm.Clear();
            }
        }
        
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConsoleViewModel vm)
            {
                vm.CopyToClipboard();
            }
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tag)
            {
                if (int.TryParse(tag, out int index))
                {
                    MonoGameEditor.ViewModels.ConsoleViewModel.Instance.SelectedTabIndex = index;
                }
            }
        }
    }
}
