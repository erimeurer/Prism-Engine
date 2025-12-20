using System.Windows.Input;
using MonoGameEditor.Core;

namespace MonoGameEditor.ViewModels
{
    public class ProjectSettingsViewModel : ViewModelBase
    {
        private string _projectName;
        private string _version;
        private string _author;
        private string _description;
        private string _iconPath;

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string Version
        {
            get => _version;
            set => SetProperty(ref _version, value);
        }

        public string Author
        {
            get => _author;
            set => SetProperty(ref _author, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string IconPath
        {
            get => _iconPath;
            set => SetProperty(ref _iconPath, value);
        }

        public ICommand SaveCommand { get; }

        public ProjectSettingsViewModel()
        {
            var settings = ProjectManager.Instance.CurrentSettings;
            _projectName = settings.ProjectName;
            _version = settings.Version;
            _author = settings.Author;
            _description = settings.Description;
            _iconPath = settings.IconPath;

            SaveCommand = new RelayCommand(_ => Save());
        }

        private void Save()
        {
            var settings = ProjectManager.Instance.CurrentSettings;
            settings.ProjectName = ProjectName;
            settings.Version = Version;
            settings.Author = Author;
            settings.Description = Description;
            settings.IconPath = IconPath;

            ProjectManager.Instance.SaveSettings();
        }
    }
}
