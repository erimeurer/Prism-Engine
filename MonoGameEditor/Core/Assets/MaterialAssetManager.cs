using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MonoGameEditor.Core.Materials;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Assets
{
    /// <summary>
    /// Manages material assets - discovery, default material, and material creation
    /// </summary>
    public class MaterialAssetManager
    {
        private static MaterialAssetManager _instance = null!;
        public static MaterialAssetManager Instance => _instance ??= new MaterialAssetManager();

        private string _defaultMaterialPath = null!;
        private List<string> _cachedMaterialPaths = new();

        private MaterialAssetManager()
        {
            // Private constructor for singleton
        }

        /// <summary>
        /// Gets the path to the default material, creating it if necessary
        /// </summary>
        public string GetDefaultMaterialPath()
        {
            if (string.IsNullOrEmpty(_defaultMaterialPath))
            {
                CreateDefaultMaterialIfNeeded();
            }
            return _defaultMaterialPath;
        }

        /// <summary>
        /// Creates the default material if it doesn't exist
        /// </summary>
        public void CreateDefaultMaterialIfNeeded()
        {
            // Default material is stored in editor's Content/Materials directory
            string editorPath = AppDomain.CurrentDomain.BaseDirectory;
            string materialsDir = Path.Combine(editorPath, "Content", "Materials");
            
            if (!Directory.Exists(materialsDir))
            {
                Directory.CreateDirectory(materialsDir);
            }

            _defaultMaterialPath = Path.Combine(materialsDir, "DefaultMaterial.mat");

            if (!File.Exists(_defaultMaterialPath))
            {
                // Create default material
                var defaultMaterial = new
                {
                    name = "Default",
                    albedoColor = new[] { 0.7f, 0.7f, 0.7f },
                    metallic = 0.0f,
                    roughness = 0.5f,
                    ambientOcclusion = 1.0f,
                    shaderPath = "Content/Shaders/PBREffect.fx",
                    customProperties = new Dictionary<string, object>()
                };

                string json = JsonSerializer.Serialize(defaultMaterial, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_defaultMaterialPath, json);

                Logger.Log($"[MaterialAssets] Created default material at: {_defaultMaterialPath}");
            }
        }

        /// <summary>
        /// Discovers all material files (.mat) in the project
        /// </summary>
        public List<string> DiscoverMaterials(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
            {
                _cachedMaterialPaths.Clear();
                return _cachedMaterialPaths;
            }

            try
            {
                var materialFiles = Directory.GetFiles(projectPath, "*.mat", SearchOption.AllDirectories);
                _cachedMaterialPaths = materialFiles.ToList();

                Logger.Log($"[MaterialAssets] Found {_cachedMaterialPaths.Count} materials in project");
                return _cachedMaterialPaths;
            }
            catch (Exception ex)
            {
                Logger.Log($"[MaterialAssets] Error discovering materials: {ex.Message}");
                _cachedMaterialPaths.Clear();
                return _cachedMaterialPaths;
            }
        }

        /// <summary>
        /// Gets all discovered material paths
        /// </summary>
        public List<string> GetAvailableMaterials()
        {
            return new List<string>(_cachedMaterialPaths);
        }

        /// <summary>
        /// Refreshes the material cache
        /// </summary>
        public void RefreshMaterialCache()
        {
            var projectPath = ProjectManager.Instance.ProjectPath;
            if (!string.IsNullOrEmpty(projectPath))
            {
                DiscoverMaterials(projectPath);
            }
        }
    }
}
