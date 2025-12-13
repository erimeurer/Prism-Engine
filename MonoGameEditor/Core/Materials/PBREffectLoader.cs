using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;

namespace MonoGameEditor.Core.Materials
{
    /// <summary>
    /// Helper to load and cache the PBR Effect shader
    /// </summary>
    public static class PBREffectLoader
    {
        // Cache per ContentManager to avoid conflicts between MonoGameControl and GameControl
        private static Dictionary<Microsoft.Xna.Framework.Content.ContentManager, Effect> _effectCache = new();

        public static Effect Load(GraphicsDevice device, Microsoft.Xna.Framework.Content.ContentManager contentManager = null)
        {
            try
            {
                // If a ContentManager was provided directly, use it first
                if (contentManager != null)
                {
                    // Check if we have cached Effect for this ContentManager
                    if (_effectCache.TryGetValue(contentManager, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = contentManager.Load<Effect>("Shaders/PBREffect");
                        _effectCache[contentManager] = effect; // Cache it
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded via provided ContentManager!");
                        return effect;
                    }
                    catch (Exception ex)
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] Provided ContentManager load failed: {ex.Message}");
                    }
                }
                
                // Try MonoGameControl's ContentManager (for SceneView)
                var monoGameContent = MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
                if (monoGameContent != null)
                {
                    // Check cache
                    if (_effectCache.TryGetValue(monoGameContent, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = monoGameContent.Load<Effect>("Shaders/PBREffect");
                        _effectCache[monoGameContent] = effect;
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded via MonoGameControl.OwnContentManager!");
                        return effect;
                    }
                    catch (Exception ex)
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] MonoGameControl ContentManager load failed: {ex.Message}");
                    }
                }
                
                // Try to use shared ContentManager from GameControl
                var sharedContent = MonoGameEditor.Controls.GameControl.SharedContent;
                if (sharedContent != null)
                {
                    // Check cache for SharedContent
                    if (_effectCache.TryGetValue(sharedContent, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = sharedContent.Load<Effect>("Shaders/PBREffect");
                        _effectCache[sharedContent] = effect; // Cache it
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded via SharedContent!");
                        return effect;
                    }
                    catch (Exception ex)
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] SharedContent load failed: {ex.Message}");
                    }
                }
                else
                {
                    MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] SharedContent not available yet");
                }

                // Fallback: Try mgfxo format
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "PBREffect.mgfxo");
                
                if (File.Exists(shaderPath))
                {
                    byte[] shaderBytes = File.ReadAllBytes(shaderPath);
                    var effect = new Effect(device, shaderBytes);
                    MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded from mgfxo!");
                    return effect;
                }
                else
                {
                    MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] Shader not found at: {shaderPath}");
                }
            }
            catch (Exception ex)
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] Failed to load PBR shader: {ex.Message}");
            }

            // Fallback: return null to use BasicEffect
            MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] Using BasicEffect fallback");
            return null;
        }

        public static void Dispose()
        {
            foreach (var effect in _effectCache.Values)
            {
                effect?.Dispose();
            }
            _effectCache.Clear();
        }
    }
}
