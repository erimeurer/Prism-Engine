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

        private void OnBrowseIconClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Icon files (*.ico)|*.ico|All files (*.*)|*.*",
                Title = "Select Executable Icon"
            };

            if (dialog.ShowDialog() == true)
            {
                if (DataContext is ProjectSettingsViewModel vm)
                {
                    vm.IconPath = dialog.FileName;
                }
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
