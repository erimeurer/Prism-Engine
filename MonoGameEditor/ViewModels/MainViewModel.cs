using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MonoGameEditor.ViewModels
{
    public enum TransformTool
    {
        None,
        Move,
        Rotate,
        Scale
    }

    public enum GizmoTransformMode
    {
        Pivot,
        Center
    }

    public enum GizmoOrientationMode
    {
        Global,
        Local
    }

    public class MainViewModel : ViewModelBase
    {
        public static MainViewModel Instance { get; private set; }

        public HierarchyViewModel Hierarchy { get; } = new HierarchyViewModel();
        public InspectorViewModel Inspector { get; } = new InspectorViewModel();
        public ProjectViewModel Project { get; } = new ProjectViewModel();
        public ConsoleViewModel Console => ConsoleViewModel.Instance;

        public ObservableCollection<DocumentViewModel> Documents { get; } = new ObservableCollection<DocumentViewModel>();

        private DocumentViewModel _activeDocument;
        public DocumentViewModel ActiveDocument
        {
            get => _activeDocument;
            set { _activeDocument = value; OnPropertyChanged(); }
        }

        private TransformTool _activeTool = TransformTool.Move; // Default to Move
        public TransformTool ActiveTool
        {
            get => _activeTool;
            set 
            {
                if (_activeTool != value)
                {
                    _activeTool = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMoveToolActive));
                    OnPropertyChanged(nameof(IsRotateToolActive));
                }
            }
        }

        public bool IsMoveToolActive
        {
            get => ActiveTool == TransformTool.Move;
            set 
            { 
                if (value) ActiveTool = TransformTool.Move;
                OnPropertyChanged();
            }
        }

        public bool IsRotateToolActive
        {
            get => ActiveTool == TransformTool.Rotate;
            set 
            { 
                if (value) ActiveTool = TransformTool.Rotate;
                OnPropertyChanged();
            }
        }

        private GizmoTransformMode _gizmoTransformMode = GizmoTransformMode.Pivot;
        public GizmoTransformMode GizmoTransformMode
        {
            get => _gizmoTransformMode;
            set
            {
                if (_gizmoTransformMode != value)
                {
                    _gizmoTransformMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPivotMode));
                }
            }
        }

        public bool IsPivotMode
        {
            get => GizmoTransformMode == GizmoTransformMode.Pivot;
            set
            {
                GizmoTransformMode = value ? GizmoTransformMode.Pivot : GizmoTransformMode.Center;
                OnPropertyChanged();
            }
        }

        private GizmoOrientationMode _gizmoOrientationMode = GizmoOrientationMode.Global;
        public GizmoOrientationMode GizmoOrientationMode
        {
            get => _gizmoOrientationMode;
            set
            {
                if (_gizmoOrientationMode != value)
                {
                    _gizmoOrientationMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLocalMode));
                }
            }
        }

        public bool IsLocalMode
        {
            get => GizmoOrientationMode == GizmoOrientationMode.Local;
            set
            {
                GizmoOrientationMode = value ? GizmoOrientationMode.Local : GizmoOrientationMode.Global;
                OnPropertyChanged();
            }
        }

        private bool _isSkyboxVisible = true;
        public bool IsSkyboxVisible
        {
            get => _isSkyboxVisible;
            set
            {
                if (_isSkyboxVisible != value)
                {
                    _isSkyboxVisible = value;
                    OnPropertyChanged();
                }
            }
        }
        
        public ICommand SelectToolCommand { get; }

        public SceneViewModel Scene { get; } = new SceneViewModel();
        public GameViewModel Game { get; } = new GameViewModel();

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }

        public ICommand NewSceneCommand { get; }
        public ICommand OpenSceneCommand { get; }
        public ICommand SaveSceneCommand { get; }
        public ICommand SaveSceneAsCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand NewProjectCommand { get; }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set { _isPaused = value; OnPropertyChanged(); }
        }

        private string _currentScenePath;

        public MainViewModel()
        {
            Instance = this;

            // Initialize Default Documents
            Documents.Add(Scene);
            Documents.Add(Game);
            
            // Set initial active document
            ActiveDocument = Scene;

            PlayCommand = new RelayCommand(_ => TogglePlay());
            PauseCommand = new RelayCommand(_ => TogglePause(), _ => IsPlaying);
            StopCommand = new RelayCommand(_ => Stop());

            NewSceneCommand = new RelayCommand(_ => NewScene());
            OpenSceneCommand = new RelayCommand(_ => OpenScene());
            SaveSceneCommand = new RelayCommand(_ => SaveScene());
            SaveSceneAsCommand = new RelayCommand(_ => SaveSceneAs());
            OpenProjectCommand = new RelayCommand(_ => OpenProject());
            NewProjectCommand = new RelayCommand(_ => NewProject());

            SelectToolCommand = new RelayCommand(param => 
            {
                if (param is TransformTool tool)
                {
                    ActiveTool = tool;
                    ConsoleViewModel.Log($"Tool Selected: {tool}");
                }
                else if (param is string toolName && Enum.TryParse(toolName, out TransformTool parsed))
                {
                    ActiveTool = parsed;
                    ConsoleViewModel.Log($"Tool Selected: {parsed}");
                }
            });
            // Auto-load last project
            var globalSettings = MonoGameEditor.Core.GlobalEditorSettings.Load();
            if (!string.IsNullOrEmpty(globalSettings.LastProjectPath) && System.IO.Directory.Exists(globalSettings.LastProjectPath))
            {
                OpenProject(globalSettings.LastProjectPath);
            }
        }

        private void TogglePlay()
        {
            IsPlaying = !IsPlaying;
            if (!IsPlaying) 
            {
                IsPaused = false;
                ActiveDocument = Scene; // Switch back to Scene on Stop
            }
            else
            {
                ActiveDocument = Game; // Switch to Game on Play
            }
            ConsoleViewModel.Log(IsPlaying ? "Play Mode Started" : "Play Mode Stopped");
        }

        private void TogglePause()
        {
            if (IsPlaying)
            {
                IsPaused = !IsPaused;
                ConsoleViewModel.Log(IsPaused ? "Game Paused" : "Game Resumed");
            }
        }

        private void Stop()
        {
            IsPlaying = false;
            IsPaused = false;
            ActiveDocument = Scene; // Switch back to Scene on Stop
            ConsoleViewModel.Log("Play Mode Stopped");
        }

        public string WindowTitle
        {
            get
            {
                var projectName = MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings?.ProjectName ?? "No Project";
                var sceneName = !string.IsNullOrEmpty(_currentScenePath) ? System.IO.Path.GetFileNameWithoutExtension(_currentScenePath) : "Untitled";
                return $"MonoGame Editor - {projectName} - {sceneName}";
            }
        }

        private void UpdateTitle()
        {
            OnPropertyChanged(nameof(WindowTitle));
        }

        private void NewScene()
        {
            MonoGameEditor.Core.SceneManager.Instance.CreateDefaultScene();
            _currentScenePath = null;
            
            // Update Settings
            MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.LastOpenedScene = "";
            MonoGameEditor.Core.ProjectManager.Instance.SaveSettings();
            
            UpdateTitle();
            ConsoleViewModel.Log("New Scene Created");
        }

        private void OpenScene()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "World Files (*.world)|*.world|All Files (*.*)|*.*",
                InitialDirectory = MonoGameEditor.Core.ProjectManager.Instance.ScenesPath
            };

            if (dialog.ShowDialog() == true)
            {
                OpenScene(dialog.FileName);
            }
        }

        public void OpenScene(string path)
        {
             MonoGameEditor.IO.SceneSerializer.LoadScene(path);
             _currentScenePath = path;
             
             // Update Settings
             MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.LastOpenedScene = path;
             MonoGameEditor.Core.ProjectManager.Instance.SaveSettings();
             
             UpdateTitle();
             ConsoleViewModel.Log($"Scene Loaded: {_currentScenePath}");
        }

        private void SaveScene()
        {
            if (string.IsNullOrEmpty(_currentScenePath))
            {
                SaveSceneAs();
            }
            else
            {
                MonoGameEditor.IO.SceneSerializer.SaveScene(_currentScenePath);
                
                // Update Settings just in case
                MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.LastOpenedScene = _currentScenePath;
                MonoGameEditor.Core.ProjectManager.Instance.SaveSettings();
                
                UpdateTitle();
                ConsoleViewModel.Log($"Scene Saved: {_currentScenePath}");
            }
        }

        private void SaveSceneAs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "World Files (*.world)|*.world|All Files (*.*)|*.*",
                InitialDirectory = MonoGameEditor.Core.ProjectManager.Instance.ScenesPath,
                FileName = "NewScene"
            };

            if (dialog.ShowDialog() == true)
            {
                _currentScenePath = dialog.FileName;
                MonoGameEditor.IO.SceneSerializer.SaveScene(_currentScenePath);

                 // Update Settings
                MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.LastOpenedScene = _currentScenePath;
                MonoGameEditor.Core.ProjectManager.Instance.SaveSettings();
                
                UpdateTitle();
                ConsoleViewModel.Log($"Scene Saved: {_currentScenePath}");
            }
        }

        private void OpenProject(string path)
        {
             // Reset Scene when opening a new project (safe default)
             MonoGameEditor.Core.SceneManager.Instance.CreateDefaultScene();
             _currentScenePath = null;
             
             MonoGameEditor.Core.ProjectManager.Instance.OpenProject(path);
             
             // Save Global Settings
             var globalSettings = MonoGameEditor.Core.GlobalEditorSettings.Load();
             globalSettings.LastProjectPath = path;
             globalSettings.Save();
             
             // Auto-load last scene
             var lastScene = MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.LastOpenedScene;
             if (!string.IsNullOrEmpty(lastScene) && System.IO.File.Exists(lastScene))
             {
                 OpenScene(lastScene);
             }
             else
             {
                  UpdateTitle();
             }

             ConsoleViewModel.Log($"Project Opened: {path}");
        }

        private void OpenProject()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select Project Folder";
                dialog.UseDescriptionForTitle = true;
                dialog.ShowNewFolderButton = true;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OpenProject(dialog.SelectedPath);
                }
            }
        }

        private void NewProject()
        {
            var dialog = new MonoGameEditor.Views.NewProjectWindow();
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                string newProjectPath = dialog.ResultPath;

                // Reset Scene and Create Defaults
                MonoGameEditor.Core.SceneManager.Instance.CreateDefaultScene();
                _currentScenePath = null;

                // Initialize Project (Creates Assets folder etc)
                MonoGameEditor.Core.ProjectManager.Instance.CreateProject(newProjectPath);
                
                // Save Global Settings
                var globalSettings = MonoGameEditor.Core.GlobalEditorSettings.Load();
                globalSettings.LastProjectPath = newProjectPath;
                globalSettings.Save();
                
                // Explicitly set Project Name from dialog 
                MonoGameEditor.Core.ProjectManager.Instance.CurrentSettings.ProjectName = dialog.ResultName;
                MonoGameEditor.Core.ProjectManager.Instance.SaveSettings();

                // Create a default Main.world scene
                var defaultScenePath = System.IO.Path.Combine(MonoGameEditor.Core.ProjectManager.Instance.ScenesPath, "Main.world");
                MonoGameEditor.IO.SceneSerializer.SaveScene(defaultScenePath);
                
                // Load it immediately (sets path and title)
                OpenScene(defaultScenePath);
                
                ConsoleViewModel.Log($"New Project Created: {newProjectPath}");
            }
        }

        public event EventHandler FocusRequested;
        public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);
    }
}
