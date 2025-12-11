using System.IO;
using System.Windows;

namespace MonoGameEditor.Views
{
    public partial class NewProjectWindow : Window
    {
        public string ResultPath { get; private set; } = string.Empty;
        public string ResultName { get; private set; } = string.Empty;

        public NewProjectWindow()
        {
            InitializeComponent();
            LocationTextBox.Text = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        }

        private void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select Parent Folder";
                dialog.UseDescriptionForTitle = true;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    LocationTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OnCreateClick(object sender, RoutedEventArgs e)
        {
            string name = ProjectNameTextBox.Text.Trim();
            string location = LocationTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ErrorTextBlock.Text = "Project Name cannot be empty.";
                return;
            }

            if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
            {
                ErrorTextBlock.Text = "Please select a valid location.";
                return;
            }

            // Check for invalid chars
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ErrorTextBlock.Text = "Project Name contains invalid characters.";
                return;
            }

            string fullPath = Path.Combine(location, name);
            if (Directory.Exists(fullPath))
            {
                ErrorTextBlock.Text = $"Folder already exists: {fullPath}";
                return;
            }

            ResultName = name;
            ResultPath = fullPath;
            DialogResult = true;
            Close();
        }
    }
}
