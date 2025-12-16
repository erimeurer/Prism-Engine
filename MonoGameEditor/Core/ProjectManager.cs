using System;
using System.IO;
using MonoGameEditor.Core.Shaders;

namespace MonoGameEditor.Core
{
    public class ProjectManager
    {
        private static ProjectManager? _instance;
        public static ProjectManager Instance => _instance ??= new ProjectManager();

        public string ProjectPath { get; private set; } = string.Empty;
        public string AssetsPath => !string.IsNullOrEmpty(ProjectPath) ? Path.Combine(ProjectPath, "Assets") : string.Empty;
        public string ScenesPath => !string.IsNullOrEmpty(AssetsPath) ? Path.Combine(AssetsPath, "Scenes") : string.Empty;

        public event Action? ProjectLoaded;

        public ProjectSettings CurrentSettings { get; private set; } = new ProjectSettings();
        
        // Unity-style automatic shader compilation
        private ShaderCompilationService? _shaderCompiler;

        public void OpenProject(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Project path not found: {path}");
            }

            ProjectPath = path;
            EnsureStructure();
            LoadSettings();
            
            // Start automatic shader compilation
            _shaderCompiler?.StopMonitoring();
            _shaderCompiler = new ShaderCompilationService(ProjectPath);
            _shaderCompiler.StartMonitoring();
            
            ProjectLoaded?.Invoke();

            // Fix any script class names that don't match filenames
            Utilities.ScriptClassNameFixer.FixAllScriptsInProject();
            
            // Discover and compile scripts
            ScriptManager.Instance.DiscoverAndCompileScripts();
        }

        public void CreateProject(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            ProjectPath = path;
            EnsureStructure();
            
            // Default settings for new project
            CurrentSettings = new ProjectSettings
            {
                ProjectName = new DirectoryInfo(path).Name, // Use folder name as default
                LastOpenedScene = ""
            };
            SaveSettings();
            
            ProjectLoaded?.Invoke();
        }

        public void SaveSettings()
        {
            if (string.IsNullOrEmpty(ProjectPath)) return;

            string settingsPath = Path.Combine(ProjectPath, "ProjectSettings.json");
            string json = System.Text.Json.JsonSerializer.Serialize(CurrentSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }

        private void LoadSettings()
        {
            if (string.IsNullOrEmpty(ProjectPath)) return;

            string settingsPath = Path.Combine(ProjectPath, "ProjectSettings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<ProjectSettings>(json);
                    if (settings != null)
                    {
                        CurrentSettings = settings;
                        return;
                    }
                }
                catch
                {
                    // Fallback if corrupt
                }
            }

            // Fallback default
            CurrentSettings = new ProjectSettings
            {
                ProjectName = new DirectoryInfo(ProjectPath).Name
            };
        }

        private void EnsureStructure()
        {
            if (string.IsNullOrEmpty(ProjectPath)) return;

            if (!Directory.Exists(AssetsPath)) Directory.CreateDirectory(AssetsPath);
            if (!Directory.Exists(ScenesPath)) Directory.CreateDirectory(ScenesPath);
        }
    }
}
