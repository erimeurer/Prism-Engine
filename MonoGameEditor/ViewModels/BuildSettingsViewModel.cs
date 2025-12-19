using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MonoGameEditor.Core;

namespace MonoGameEditor.ViewModels
{
    public class BuildSettingsViewModel : ViewModelBase
    {
        public ObservableCollection<SceneInBuildItem> Scenes { get; } = new ObservableCollection<SceneInBuildItem>();

        public ICommand AddCurrentSceneCommand { get; }
        public ICommand RemoveSceneCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand BuildCommand { get; }

        public BuildSettingsViewModel()
        {
            LoadScenes();

            AddCurrentSceneCommand = new RelayCommand(_ => AddCurrentScene());
            RemoveSceneCommand = new RelayCommand(param => RemoveScene(param as SceneInBuildItem));
            MoveUpCommand = new RelayCommand(param => MoveUp(param as SceneInBuildItem));
            MoveDownCommand = new RelayCommand(param => MoveDown(param as SceneInBuildItem));
            BuildCommand = new RelayCommand(_ => Build());
        }

        private void Build()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select Output Folder for Build";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    BuildManager.Instance.BuildProject(dialog.SelectedPath);
                }
            }
        }

        private void LoadScenes()
        {
            Scenes.Clear();
            var settings = ProjectManager.Instance.CurrentSettings;
            foreach (var scenePath in settings.ScenesInBuild)
            {
                Scenes.Add(new SceneInBuildItem { Path = scenePath, Name = Path.GetFileNameWithoutExtension(scenePath) });
            }
        }

        private void SaveScenes()
        {
            var settings = ProjectManager.Instance.CurrentSettings;
            settings.ScenesInBuild = Scenes.Select(s => s.Path).ToList();
            ProjectManager.Instance.SaveSettings();
        }

        private void AddCurrentScene()
        {
            // This is a bit tricky as MainViewModel holds the current scene path
            // We can access it via Singleton if available
            var mainVm = MainViewModel.Instance;
            // Assuming we have a way to get current scene path from MainViewModel (currently private _currentScenePath)
            // I will implement a public getter in MainViewModel or use recent history
            
            // For now, let's use a FolderBrowser or just the logic to add by path
            // Better yet, let's add a "Browse" for scenes
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "World Files|*.world",
                InitialDirectory = ProjectManager.Instance.ScenesPath,
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    string relativePath = file;
                    if (file.StartsWith(ProjectManager.Instance.ProjectPath))
                    {
                        relativePath = Path.GetRelativePath(ProjectManager.Instance.ProjectPath, file).Replace("\\", "/");
                    }

                    if (!Scenes.Any(s => s.Path == relativePath))
                    {
                        Scenes.Add(new SceneInBuildItem { Path = relativePath, Name = Path.GetFileNameWithoutExtension(file) });
                    }
                }
                SaveScenes();
            }
        }

        private void RemoveScene(SceneInBuildItem item)
        {
            if (item != null)
            {
                Scenes.Remove(item);
                SaveScenes();
            }
        }

        private void MoveUp(SceneInBuildItem item)
        {
            int index = Scenes.IndexOf(item);
            if (index > 0)
            {
                Scenes.Move(index, index - 1);
                SaveScenes();
            }
        }

        private void MoveDown(SceneInBuildItem item)
        {
            int index = Scenes.IndexOf(item);
            if (index < Scenes.Count - 1)
            {
                Scenes.Move(index, index + 1);
                SaveScenes();
            }
        }
    }

    public class SceneInBuildItem : ViewModelBase
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
