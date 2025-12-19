using System.Windows;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Views
{
    public partial class BuildSettingsWindow : Window
    {
        public BuildSettingsWindow()
        {
            InitializeComponent();
            DataContext = new BuildSettingsViewModel();
        }

        private void OnPlayerSettingsClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ProjectSettingsWindow();
            dialog.Owner = this; // Open relative to Build Settings
            dialog.ShowDialog();
        }
    }
}
