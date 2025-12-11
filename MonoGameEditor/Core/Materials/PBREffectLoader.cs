using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;

namespace MonoGameEditor.Core.Materials
{
    /// <summary>
    /// Helper to load and cache the PBR Effect shader
    /// </summary>
    public static class PBREffectLoader
    {
        private static Effect _cachedEffect;

        public static Effect Load(GraphicsDevice device)
        {
            if (_cachedEffect != null && !_cachedEffect.IsDisposed)
                return _cachedEffect;

            try
            {
                // Try to load compiled shader from Content folder
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "PBREffect.mgfxo");
                
                if (File.Exists(shaderPath))
                {
                    byte[] shaderBytes = File.ReadAllBytes(shaderPath);
                    _cachedEffect = new Effect(device, shaderBytes);
                    return _cachedEffect;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load PBR shader: {ex.Message}");
            }

            // Fallback: return null to use BasicEffect
            return null;
        }

        public static void Dispose()
        {
            _cachedEffect?.Dispose();
            _cachedEffect = null;
        }
    }
}
