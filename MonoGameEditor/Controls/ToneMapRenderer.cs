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
        public bool UseAA { get; set; } = false;
        public bool UsePRAA { get; set; } = false;
        public bool UseMotionBlur { get; set; } = false;
        public float MotionBlurIntensity { get; set; } = 1.0f;
        public Vector2 BlurDirection { get; set; } = Vector2.Zero;

        public ToneMapRenderer(GraphicsDevice device)
        {
            _graphicsDevice = device;
            InitializeQuad();
            // We defer effect loading to Initialize(ContentManager) if possible
        }

        public void Initialize(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            if (content != null)
            {
                try
                {
                    _toneMapEffect = content.Load<Effect>("Shaders/ToneMap");
                    ConsoleViewModel.Log("[ToneMapRenderer] Shader loaded successfully via ContentManager.");
                }
                catch (Exception ex)
                {
                    ConsoleViewModel.Log($"[ToneMapRenderer] Failed to load via ContentManager: {ex.Message}. Trying fallback...");
                    LoadEffectFallback();
                }
            }
            else
            {
                LoadEffectFallback();
            }
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

        private void LoadEffectFallback()
        {
            try 
            {
               // Try to load compiled shader if existing
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "ToneMap.mgfxo");
                if (File.Exists(shaderPath))
                {
                    var bytes = File.ReadAllBytes(shaderPath);
                    _toneMapEffect = new Effect(_graphicsDevice, bytes);
                    ConsoleViewModel.Log("[ToneMapRenderer] Shader loaded via fallback (.mgfxo).");
                }
                else
                {
                    ConsoleViewModel.Log("[ToneMapRenderer] Warning: ToneMap shader not found (no .xnb or .mgfxo). PP will be disabled.");
                }
            }
            catch (Exception ex)
            {
                 ConsoleViewModel.Log($"[ToneMapRenderer] Fallback load failed: {ex.Message}");
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
                _toneMapEffect.Parameters["UseAA"]?.SetValue(UseAA);
                _toneMapEffect.Parameters["UsePRAA"]?.SetValue(UsePRAA);
                _toneMapEffect.Parameters["UseMotionBlur"]?.SetValue(UseMotionBlur);
                _toneMapEffect.Parameters["MotionBlurIntensity"]?.SetValue(MotionBlurIntensity);
                _toneMapEffect.Parameters["ScreenSize"]?.SetValue(new Vector2(sourceTexture.Width, sourceTexture.Height));
                _toneMapEffect.Parameters["BlurDirection"]?.SetValue(BlurDirection);

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
