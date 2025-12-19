using System.Windows;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Views
{
    public partial class ProjectSettingsWindow : Window
    {
        public ProjectSettingsWindow()
        {
            InitializeComponent();
            DataContext = new ProjectSettingsViewModel();
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            // The command is handled by the ViewModel, we just close if everything is fine
            // Since it's a settings window, we can just say "Applied"
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
