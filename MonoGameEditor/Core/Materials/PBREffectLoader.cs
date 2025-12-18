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
                
                // CRITICAL FIX: Match device to ContentManager
                var gameDevice = MonoGameEditor.Controls.GameControl.SharedGraphicsDevice;
                var sceneDevice = MonoGameEditor.Controls.MonoGameControl.SharedGraphicsDevice;
                
                Microsoft.Xna.Framework.Content.ContentManager contentToTry = null;
                
                if (device == sceneDevice)
                {
                    contentToTry = MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
                }
                else if (device == gameDevice)
                {
                    contentToTry = MonoGameEditor.Controls.GameControl.SharedContent;
                }
                
                if (contentToTry != null)
                {
                    if (_effectCache.TryGetValue(contentToTry, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = contentToTry.Load<Effect>("Shaders/PBREffect");
                        _effectCache[contentToTry] = effect;
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] PBR shader loaded via matched ContentManager for device {device.GetHashCode()}!");
                        return effect;
                    }
                    catch (Exception ex)
                    {
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] Matched ContentManager load failed: {ex.Message}");
                    }
                }

                // Fallback: Try MonoGameControl's ContentManager (if not already tried)
                var monoGameContent = MonoGameEditor.Controls.MonoGameControl.OwnContentManager;
                if (monoGameContent != null && monoGameContent != contentToTry)
                {
                    if (_effectCache.TryGetValue(monoGameContent, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = monoGameContent.Load<Effect>("Shaders/PBREffect");
                        _effectCache[monoGameContent] = effect;
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded via MonoGameControl.OwnContentManager (Fallback)!");
                        return effect;
                    }
                    catch { }
                }
                
                // Fallback: Try shared ContentManager from GameControl (if not already tried)
                var sharedContent = MonoGameEditor.Controls.GameControl.SharedContent;
                if (sharedContent != null && sharedContent != contentToTry)
                {
                    if (_effectCache.TryGetValue(sharedContent, out var cachedEffect) && !cachedEffect.IsDisposed)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = sharedContent.Load<Effect>("Shaders/PBREffect");
                        _effectCache[sharedContent] = effect;
                        MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded via SharedContent (Fallback)!");
                        return effect;
                    }
                    catch { }
                }

                // Last Resort: Try mgfxo format
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "PBREffect.mgfxo");
                
                if (File.Exists(shaderPath))
                {
                    byte[] shaderBytes = File.ReadAllBytes(shaderPath);
                    var effect = new Effect(device, shaderBytes);
                    MonoGameEditor.ViewModels.ConsoleViewModel.Log("[PBRLoader] PBR shader loaded from mgfxo!");
                    return effect;
                }
            }
            catch (Exception ex)
            {
                MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[PBRLoader] Failed to load PBR shader: {ex.Message}");
            }

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
