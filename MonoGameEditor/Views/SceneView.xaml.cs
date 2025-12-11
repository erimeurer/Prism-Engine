using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MonoGameEditor.Views
{
    public partial class SceneView : UserControl
    {
        public SceneView()
        {
            InitializeComponent();
        }

        private void GridToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                GameHost.ShowGrid = toggle.IsChecked == true;
            }
        }

        // Visibility handled internally by MonoGameControl

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MonoGameEditor.ViewModels.MainViewModel vm)
            {
                vm.PropertyChanged += Vm_PropertyChanged;
                // Init initial state
                GameHost.ShowSkybox = vm.IsSkyboxVisible;
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
             if (DataContext is MonoGameEditor.ViewModels.MainViewModel vm)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MonoGameEditor.ViewModels.MainViewModel.IsSkyboxVisible))
            {
                if (DataContext is MonoGameEditor.ViewModels.MainViewModel vm)
                {
                    GameHost.ShowSkybox = vm.IsSkyboxVisible;
                }
            }
        }
    }
}
