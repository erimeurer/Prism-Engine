using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core.Assets;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Core
{
    /// <summary>
    /// Manages script discovery, compilation, and execution
    /// </summary>
    public class ScriptManager
    {
        private static ScriptManager? _instance;
        public static ScriptManager Instance => _instance ??= new ScriptManager();

        private Dictionary<string, ScriptAsset> _scriptAssets = new();
        private Assembly? _compiledScriptsAssembly;

        /// <summary>
        /// Get all available script assets
        /// </summary>
        public IReadOnlyDictionary<string, ScriptAsset> ScriptAssets => _scriptAssets;

        /// <summary>
        /// Scans the project's Scripts folder for .cs files and compiles them
        /// </summary>
        public void DiscoverAndCompileScripts()
        {
            _scriptAssets.Clear();

            string projectPath = ProjectManager.Instance.ProjectPath;
            if (string.IsNullOrEmpty(projectPath))
            {
                ViewModels.ConsoleViewModel.Log("[ScriptManager] No project loaded, skipping script discovery");
                return;
            }

            string scriptsFolder = Path.Combine(projectPath, "Assets", "Scripts");
            if (!Directory.Exists(scriptsFolder))
            {
                ViewModels.ConsoleViewModel.Log($"[ScriptManager] Creating Scripts folder at {scriptsFolder}");
                Directory.CreateDirectory(scriptsFolder);
                return;
            }

            var scriptFiles = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories);
            ViewModels.ConsoleViewModel.Log($"[ScriptManager] Found {scriptFiles.Length} script file(s) in {scriptsFolder}");

            if (scriptFiles.Length == 0)
                return;

            // Compile all scripts into a single assembly
            CompileScripts(scriptFiles);
        }

        private void CompileScripts(string[] scriptFiles)
        {
            try
            {
                var syntaxTrees = new List<SyntaxTree>();

                foreach (var file in scriptFiles)
                {
                    string code = File.ReadAllText(file);
                    var syntaxTree = CSharpSyntaxTree.ParseText(code, path: file);
                    syntaxTrees.Add(syntaxTree);
                }

                // Get references to necessary assemblies
                var references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Component).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Vector3).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                };

                // Add current assembly reference
                references.Add(MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location));

                var compilation = CSharpCompilation.Create(
                    "DynamicScripts",
                    syntaxTrees: syntaxTrees,
                    references: references,
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using (var ms = new MemoryStream())
                {
                    var result = compilation.Emit(ms);

                    if (!result.Success)
                    {
                        var failures = result.Diagnostics.Where(diagnostic =>
                            diagnostic.IsWarningAsError ||
                            diagnostic.Severity == DiagnosticSeverity.Error);

                        foreach (var diagnostic in failures)
                        {
                            ViewModels.ConsoleViewModel.LogError($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                        }
                        return;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    _compiledScriptsAssembly = Assembly.Load(ms.ToArray());

                    ViewModels.ConsoleViewModel.Log($"[ScriptManager] Successfully compiled {scriptFiles.Length} script(s)");

                    // Register all ScriptComponent types
                    RegisterScriptTypes(scriptFiles);
                }
            }
            catch (Exception ex)
            {
                ViewModels.ConsoleViewModel.Log($"[ScriptManager] Compilation error: {ex.Message}");
            }
        }

        private void RegisterScriptTypes(string[] scriptFiles)
        {
            if (_compiledScriptsAssembly == null)
                return;

            var scriptTypes = _compiledScriptsAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ScriptComponent)));

            foreach (var type in scriptTypes)
            {
                // Find the source file for this type
                string? sourceFile = scriptFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f) == type.Name);

                var asset = new ScriptAsset
                {
                    Name = type.Name,
                    FilePath = sourceFile ?? string.Empty,
                    CompiledType = type
                };

                _scriptAssets[type.Name] = asset;
                ViewModels.ConsoleViewModel.Log($"[ScriptManager] Registered script: {type.Name}");
            }
            
            // Hot-reload: Replace old script instances with new ones
            HotReloadScripts();
        }
        
        /// <summary>
        /// Hot-reload: Replace all script instances in the scene with newly compiled versions
        /// </summary>
        private void HotReloadScripts()
        {
            if (_compiledScriptsAssembly == null)
                return;
                
            int reloadedCount = 0;
            
            ViewModels.ConsoleViewModel.Log("[ScriptManager] Starting hot-reload of scripts in scene...");
            
            // Recursively reload all scripts in the scene
            foreach (var rootObj in SceneManager.Instance.RootObjects)
            {
                reloadedCount += HotReloadScriptsRecursive(rootObj);
            }
            
            if (reloadedCount > 0)
            {
                ViewModels.ConsoleViewModel.Log($"[ScriptManager] ✅ Hot-reloaded {reloadedCount} script instance(s)");
            }
            else
            {
                ViewModels.ConsoleViewModel.Log("[ScriptManager] No script instances to reload");
            }
        }
        
        private int HotReloadScriptsRecursive(GameObject obj)
        {
            int count = 0;
            
            // Find all ScriptComponents on this GameObject
            var scriptsToReload = obj.Components
                .Where(c => c is ScriptComponent)
                .Cast<ScriptComponent>()
                .ToList(); // Create a copy to avoid modification during iteration
            
            foreach (var oldScript in scriptsToReload)
            {
                try
                {
                    var oldType = oldScript.GetType();
                    var typeName = oldType.Name;
                    
                    // Find the new version of this type
                    var newType = _compiledScriptsAssembly.GetType(oldType.FullName ?? oldType.Name);
                    if (newType == null)
                    {
                        // Try just by name
                        newType = _compiledScriptsAssembly.GetTypes()
                            .FirstOrDefault(t => t.Name == typeName);
                    }
                    
                    if (newType != null)
                    {
                        // Create new instance
                        var newScript = Activator.CreateInstance(newType) as ScriptComponent;
                        if (newScript != null)
                        {
                            // Copy public field/property values from old to new (preserve user data)
                            CopyScriptData(oldScript, newScript);
                            
                            // Replace component
                            obj.Components.Remove(oldScript);
                            obj.AddComponent(newScript);
                            
                            count++;
                            ViewModels.ConsoleViewModel.Log($"[ScriptManager]   ↻ Reloaded {typeName} on '{obj.Name}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewModels.ConsoleViewModel.LogError($"[ScriptManager] Failed to reload script on '{obj.Name}': {ex.Message}");
                }
            }
            
            // Recursively reload children
            foreach (var child in obj.Children)
            {
                count += HotReloadScriptsRecursive(child);
            }
            
            return count;
        }
        
        /// <summary>
        /// Copy field and property values from old script to new script (preserve data during hot-reload)
        /// </summary>
        private void CopyScriptData(ScriptComponent oldScript, ScriptComponent newScript)
        {
            var oldType = oldScript.GetType();
            var newType = newScript.GetType();
            
            // Copy public fields
            foreach (var field in oldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var newField = newType.GetField(field.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (newField != null && newField.FieldType == field.FieldType)
                    {
                        var value = field.GetValue(oldScript);
                        newField.SetValue(newScript, value);
                    }
                }
                catch { /* Ignore errors */ }
            }
            
            // Copy public properties
            foreach (var prop in oldType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    if (prop.CanRead && prop.CanWrite)
                    {
                        var newProp = newType.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (newProp != null && newProp.CanRead && newProp.CanWrite && newProp.PropertyType == prop.PropertyType)
                        {
                            var value = prop.GetValue(oldScript);
                            newProp.SetValue(newScript, value);
                        }
                    }
                }
                catch { /* Ignore errors */ }
            }
        }

        /// <summary>
        /// Get a Type from the compiled scripts assembly (for scene deserialization)
        /// </summary>
        public Type? GetCompiledType(string typeName)
        {
            if (_compiledScriptsAssembly == null)
                return null;

            try
            {
                var foundType = _compiledScriptsAssembly.GetType(typeName);
                if (foundType != null)
                    return foundType;

                foreach (var t in _compiledScriptsAssembly.GetTypes())
                {
                    if (t.FullName == typeName || t.Name == typeName)
                        return t;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Create an instance of a script by name
        /// </summary>
        public ScriptComponent? CreateScriptInstance(string scriptName)
        {
            if (_scriptAssets.TryGetValue(scriptName, out var asset) && asset.CompiledType != null)
            {
                try
                {
                    return Activator.CreateInstance(asset.CompiledType) as ScriptComponent;
                }
                catch (Exception ex)
                {
                    ViewModels.ConsoleViewModel.Log($"[ScriptManager] Failed to create instance of {scriptName}: {ex.Message}");
                }
            }
            return null;
        }

        /// <summary>
        /// Create an instance of a script by type name (for deserialization)
        /// </summary>
        public ScriptComponent? CreateScriptInstanceByTypeName(string typeName)
        {
            if (_compiledScriptsAssembly == null)
                return null;

            try
            {
                var type = _compiledScriptsAssembly.GetType(typeName);
                if (type != null && type.IsSubclassOf(typeof(ScriptComponent)))
                {
                    return Activator.CreateInstance(type) as ScriptComponent;
                }
            }
            catch (Exception ex)
            {
                ViewModels.ConsoleViewModel.Log($"[ScriptManager] Failed to create instance by type name {typeName}: {ex.Message}");
            }
            return null;
        }
       
        /// <summary>
        /// Update all active scripts in the scene
        /// </summary>
        public void UpdateScripts(GameTime gameTime)
        {
            UpdateScriptsRecursive(SceneManager.Instance.RootObjects, gameTime);
        }

        private void UpdateScriptsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects, GameTime gameTime)
        {
            foreach (var obj in objects)
            {
                if (obj.IsActive)
                {
                    foreach (var component in obj.Components)
                    {
                        if (component is ScriptComponent script)
                        {
                            try
                            {
                                script.InternalUpdate(gameTime);
                            }
                            catch (Exception ex)
                            {
                                ViewModels.ConsoleViewModel.Log($"[ScriptManager] Error in {script.ComponentName}.Update(): {ex.Message}");
                            }
                        }
                    }

                    UpdateScriptsRecursive(obj.Children, gameTime);
                }
            }
        }

        /// <summary>
        /// Reset all scripts' started flags (called when exiting Play mode)
        /// </summary>
        public void ResetAllScripts()
        {
            ResetScriptsRecursive(SceneManager.Instance.RootObjects);
        }

        private void ResetScriptsRecursive(System.Collections.ObjectModel.ObservableCollection<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                foreach (var component in obj.Components)
                {
                    if (component is ScriptComponent script)
                    {
                        script.ResetStartedFlag();
                    }
                }
                ResetScriptsRecursive(obj.Children);
            }
        }
    }
}
