using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Materials
{
    /// <summary>
    /// Helper to load and cache the PBR Effect shader
    /// </summary>
    public static class PBREffectLoader
    {
        // Cache per ContentManager to avoid conflicts between MonoGameControl and GameControl
        private static Dictionary<Microsoft.Xna.Framework.Content.ContentManager, Effect> _effectCache = new();

        public static Effect? Load(GraphicsDevice device, Microsoft.Xna.Framework.Content.ContentManager? contentManager = null)
        {
            try
            {
                // If no ContentManager provided, resolve from device
                var contentToTry = contentManager ?? GraphicsManager.GetContentManager(device) ?? GraphicsManager.ContentManager;
                
                if (contentToTry != null)
                {
                    if (_effectCache.TryGetValue(contentToTry, out var cachedEffect) && !cachedEffect.IsDisposed && cachedEffect.GraphicsDevice == device)
                    {
                        return cachedEffect;
                    }
                    
                    try
                    {
                        var effect = contentToTry.Load<Effect>("Shaders/PBREffect");
                        _effectCache[contentToTry] = effect;
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
                    return effect;
                }
            }
            catch (Exception ex)
            {
                // Only log if it's a real unexpected error, not just a load failure
                if (!(ex is Microsoft.Xna.Framework.Content.ContentLoadException))
                    Logger.LogError($"[PBRLoader] Unexpected error: {ex.Message}");
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
