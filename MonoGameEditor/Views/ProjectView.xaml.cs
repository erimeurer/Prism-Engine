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
}
