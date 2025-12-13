using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Core.Shaders;

/// <summary>
/// Loads and compiles shader effects
/// </summary>
public static class ShaderLoader
{
    /// <summary>
    /// Load effect from file path (supports .xnb via ContentManager, .mgfxo, and .fx)
    /// </summary>
    public static Effect? LoadEffect(GraphicsDevice device, string shaderPath)
    {
        try
        {
            // Check if path is absolute (user shader) or relative (Content shader)
            bool isAbsolutePath = Path.IsPathRooted(shaderPath);
            
            // Only try ContentManager for relative paths starting with "Content/"
            if (!isAbsolutePath && shaderPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
            {
                // Create isolated ContentManager specifically for MaterialEditor
                var services = new Microsoft.Xna.Framework.GameServiceContainer();
                services.AddService(typeof(IGraphicsDeviceService), new SimpleGraphicsDeviceService(device));
                var materialEditorContent = new Microsoft.Xna.Framework.Content.ContentManager(services, "Content");
                
                try
                {
                    // Remove "Content/" prefix and extension
                    string contentPath = Path.ChangeExtension(shaderPath, null);
                    contentPath = contentPath.Substring("Content/".Length);
                    
                    var effect = materialEditorContent.Load<Effect>(contentPath);
                    ConsoleViewModel.Log($"[ShaderLoader] Loaded shader via isolated ContentManager: {contentPath}");
                    return effect;
                }
                catch (Exception ex)
                {
                    ConsoleViewModel.Log($"[ShaderLoader] Isolated ContentManager load failed: {ex.Message}");
                    materialEditorContent.Dispose();
                }
            }
            
            // For absolute paths or when ContentManager fails, try compiled shader (.mgfxo)
            string mgfxoPath = Path.ChangeExtension(shaderPath, ".mgfxo");
            
            if (File.Exists(mgfxoPath))
            {
                byte[] bytecode = File.ReadAllBytes(mgfxoPath);
                var effect = new Effect(device, bytecode);
                ConsoleViewModel.Log($"[ShaderLoader] Loaded compiled shader: {mgfxoPath}");
                return effect;
            }
            else
            {
                ConsoleViewModel.Log($"[ShaderLoader] Compiled shader not found: {mgfxoPath}");
            }
            
            // .fx source files require pre-compilation with mgfxc tool
            if (File.Exists(shaderPath) && shaderPath.EndsWith(".fx", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleViewModel.Log($"[ShaderLoader] ERROR: .fx source file found but not compiled!");
                ConsoleViewModel.Log($"[ShaderLoader] Please compile '{Path.GetFileName(shaderPath)}' to .mgfxo using mgfxc tool");
                ConsoleViewModel.Log($"[ShaderLoader] Command: mgfxc \"{shaderPath}\" \"{mgfxoPath}\"");
                return null;
            }
            
            ConsoleViewModel.Log($"[ShaderLoader] Shader not found: {shaderPath}");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleViewModel.Log($"[ShaderLoader] Failed to load shader: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Load effect and discover its properties
    /// </summary>
    public static ShaderAsset? LoadShaderAsset(GraphicsDevice device, string shaderPath, string name)
    {
        var effect = LoadEffect(device, shaderPath);
        if (effect == null)
            return null;
            
        var asset = new ShaderAsset
        {
            Name = name,
            ShaderPath = shaderPath
        };
        
        asset.DiscoverProperties(effect);
        effect.Dispose();
        
        return asset;
    }
}
