using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Assimp;
using Microsoft.Xna.Framework;
using MonoGameEditor.Core.Materials;

namespace MonoGameEditor.Core.Assets
{
    public static class MaterialExtractor
    {
        public static void ExtractMaterials(string modelPath)
        {
            if (!File.Exists(modelPath)) return;

            string modelDir = Path.GetDirectoryName(modelPath) ?? "";
            string texturesDir = Path.Combine(modelDir, "Textures");
            string materialsDir = Path.Combine(modelDir, "Materials");

            if (!Directory.Exists(texturesDir)) Directory.CreateDirectory(texturesDir);
            if (!Directory.Exists(materialsDir)) Directory.CreateDirectory(materialsDir);

            try
            {
                using (var context = new AssimpContext())
                {
                    var scene = context.ImportFile(modelPath, PostProcessSteps.None);
                    if (scene == null) return;

                    Logger.Log($"[MaterialExtractor] Scene loaded. Materials: {scene.MaterialCount}, Textures: {scene.TextureCount}");

                    // 1. Extract Embedded Textures
                    var textureMap = new Dictionary<int, string>(); // Index in scene.Textures -> Local path
                    if (scene.HasTextures)
                    {
                        for (int i = 0; i < scene.TextureCount; i++)
                        {
                            var tex = scene.Textures[i];
                            string ext = tex.HasCompressedData ? tex.CompressedFormatHint : "png";
                            if (ext == "png" || ext == "jpg" || ext == "jpeg" || ext == "dds" || ext == "tga")
                            {
                                // Extension is probably fine
                            }
                            else
                            {
                                ext = "png"; // Fallback
                            }

                            string texName = $"tex_{i}";
                            string texPath = Path.Combine(texturesDir, $"{texName}.{ext}");

                            if (tex.HasCompressedData)
                            {
                                File.WriteAllBytes(texPath, tex.CompressedData);
                            }
                            else
                            {
                                // Uncompressed - Assimp doesn't give easy way to save raw pixel data 
                                // that is compatible with all formats without a graphics lib.
                                // For now we skip or log.
                                Logger.Log($"[MaterialExtractor] Skipped uncompressed embedded texture: {texName}");
                                continue;
                            }

                            textureMap[i] = Path.GetRelativePath(ProjectManager.Instance.ProjectPath ?? "", texPath).Replace("\\", "/");
                        }
                    }

                    // 2. Extract Materials
                    if (scene.HasMaterials)
                    {
                        for (int i = 0; i < scene.MaterialCount; i++)
                        {
                            var assimpMat = scene.Materials[i];
                            string matName = string.IsNullOrEmpty(assimpMat.Name) ? $"Material_{i}" : assimpMat.Name;
                            string matFileName = Path.Combine(materialsDir, $"{matName}.mat");

                            var matData = new
                            {
                                name = matName,
                                albedoColor = new float[] { assimpMat.ColorDiffuse.R, assimpMat.ColorDiffuse.G, assimpMat.ColorDiffuse.B },
                                metallic = assimpMat.HasColorSpecular ? assimpMat.ColorSpecular.R : 0.0f,
                                roughness = 0.5f,
                                ambientOcclusion = 1.0f,
                                shaderPath = "Content/Shaders/PBREffect.fx",
                                albedoMap = GetTexturePath(assimpMat, TextureType.Diffuse, textureMap, modelDir, texturesDir),
                                normalMap = GetTexturePath(assimpMat, TextureType.Normals, textureMap, modelDir, texturesDir),
                                metallicMap = GetTexturePath(assimpMat, TextureType.Specular, textureMap, modelDir, texturesDir),
                                roughnessMap = GetTexturePath(assimpMat, TextureType.Shininess, textureMap, modelDir, texturesDir),
                                aoMap = GetTexturePath(assimpMat, TextureType.Ambient, textureMap, modelDir, texturesDir),
                                customProperties = new Dictionary<string, object>()
                            };

                            string json = JsonSerializer.Serialize(matData, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(matFileName, json);
                            
                            Logger.Log($"[MaterialExtractor] Extracted material: {matName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[MaterialExtractor] Failed to extract from {modelPath}: {ex.Message}");
            }
        }

        private static string? GetTexturePath(Material mat, TextureType type, Dictionary<int, string> embeddedMap, string modelDir, string targetTexturesDir)
        {
            if (mat.GetMaterialTextureCount(type) <= 0) return null;
            
            if (mat.GetMaterialTexture(type, 0, out var texSlot))
            {
                string path = texSlot.FilePath;
                Logger.Log($"[MaterialExtractor] Slot {type} has path: {path}");
                if (string.IsNullOrEmpty(path)) return null;

                // 1. If it's an embedded texture (name starts with *)
                if (path.StartsWith("*"))
                {
                    if (int.TryParse(path.Substring(1), out int index) && embeddedMap.TryGetValue(index, out string? localPath))
                    {
                        return localPath;
                    }
                }

                // 2. Search for external texture in common locations
                string fileName = Path.GetFileName(path);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
                string[] commonExtensions = new string[] { ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp" };

                string[] searchFolders = new string[]
                {
                    Path.GetDirectoryName(path) ?? "",                  // Original folder
                    modelDir,                                           // Same folder as model
                    Path.Combine(modelDir, "Textures"),                 // Textures subfolder
                    Path.Combine(Path.GetDirectoryName(modelDir) ?? "", "Textures"), // Sibling Textures
                    Path.GetDirectoryName(modelDir) ?? ""               // Parent folder
                };

                foreach (var folder in searchFolders)
                {
                    if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;

                    // Try exact filename first
                    string candidate = Path.Combine(folder, fileName);
                    if (CheckAndCopyTexture(candidate, targetTexturesDir, out string? foundPath)) return foundPath;

                    // Try common extensions
                    foreach (var ext in commonExtensions)
                    {
                        candidate = Path.Combine(folder, fileNameNoExt + ext);
                        if (CheckAndCopyTexture(candidate, targetTexturesDir, out foundPath)) return foundPath;
                    }
                }

                // 3. Last Resort: Deep Search in the entire Assets folder
                string assetsRoot = ProjectManager.Instance.AssetsPath ?? "";
                if (Directory.Exists(assetsRoot))
                {
                    Logger.Log($"[MaterialExtractor] Texture not found in path or nearby. Starting Deep Search in Assets root...");
                    
                    // Search recursively for ANY file matching the name
                    var foundFiles = Directory.GetFiles(assetsRoot, fileNameNoExt + ".*", SearchOption.AllDirectories);
                    foreach (var file in foundFiles)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (commonExtensions.Contains(ext))
                        {
                            if (CheckAndCopyTexture(file, targetTexturesDir, out string? foundPath))
                            {
                                Logger.Log($"[MaterialExtractor] Deep Search found texture: {file}");
                                return foundPath;
                            }
                        }
                    }
                }

                Logger.Log($"[MaterialExtractor] Texture not found anywhere: {fileName}");
            }

            return null;
        }

        private static bool CheckAndCopyTexture(string candidate, string targetTexturesDir, out string? localPath)
        {
            localPath = null;
            if (File.Exists(candidate))
            {
                string fileName = Path.GetFileName(candidate);
                string destiny = Path.Combine(targetTexturesDir, fileName);
                try
                {
                    if (!File.Exists(destiny))
                    {
                        File.Copy(candidate, destiny);
                        Logger.Log($"[MaterialExtractor] Copied texture to local: {fileName}");
                    }
                    localPath = Path.GetRelativePath(ProjectManager.Instance.ProjectPath ?? "", destiny).Replace("\\", "/");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[MaterialExtractor] Error copying texture {fileName}: {ex.Message}");
                    localPath = Path.GetRelativePath(ProjectManager.Instance.ProjectPath ?? "", candidate).Replace("\\", "/");
                    return true;
                }
            }
            return false;
        }
    }
}
