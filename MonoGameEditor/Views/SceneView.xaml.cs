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
    }
}
