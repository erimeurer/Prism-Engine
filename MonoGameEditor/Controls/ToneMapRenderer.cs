using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.ViewModels;

namespace MonoGameEditor.Controls
{
    public class ToneMapRenderer : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private Effect _toneMapEffect;
        private VertexPositionTexture[] _fullScreenQuad;
        
        public float Exposure { get; set; } = 1.0f;

        public ToneMapRenderer(GraphicsDevice device)
        {
            _graphicsDevice = device;
            InitializeQuad();
            LoadEffect();
        }

        private void InitializeQuad()
        {
            _fullScreenQuad = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                
                new VertexPositionTexture(new Vector3(1, 1, 0), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(1, -1, 0), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-1, -1, 0), new Vector2(0, 1)),
            };
        }

        private void LoadEffect()
        {
            try 
            {
               // Try to load compiled shader if existing
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "ToneMap.mgfxo");
                if (File.Exists(shaderPath))
                {
                    var bytes = File.ReadAllBytes(shaderPath);
                    _toneMapEffect = new Effect(_graphicsDevice, bytes);
                }
                else
                {
                    // If no shader found, we can't do custom tonemapping easily without runtime compiler.
                    // Fallback to null - Host will handle using a simple SpriteBatch draw (Gamma only or raw)
                    ConsoleViewModel.Log("[ToneMapRenderer] Warning: ToneMap.mgfxo not found. HDR visuals may be incorrect.");
                }
            }
            catch (Exception ex)
            {
                 ConsoleViewModel.Log($"[ToneMapRenderer] Failed to load shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws the content of sourceTexture to the currently bound render target (or backbuffer)
        /// using ACES ToneMapping.
        /// </summary>
        public void Draw(Texture2D sourceTexture)
        {
            if (_toneMapEffect != null)
            {
                _toneMapEffect.Parameters["ScreenTexture"]?.SetValue(sourceTexture);
                _toneMapEffect.Parameters["Exposure"]?.SetValue(Exposure);

                foreach (var pass in _toneMapEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _fullScreenQuad, 0, 2);
                }
            }
            else
            {
                // Fallback: Just draw the texture linearly (linear -> clamp).
                // This will look "washed out" or "clipped" but it works.
                using (var batch = new SpriteBatch(_graphicsDevice))
                {
                    batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
                    batch.Draw(sourceTexture, _graphicsDevice.Viewport.Bounds, Color.White);
                    batch.End();
                }
            }
        }

        public void Dispose()
        {
            _toneMapEffect?.Dispose();
        }
    }
}
