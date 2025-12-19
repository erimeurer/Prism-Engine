using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Graphics
{
    public class ToneMapRenderer : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private Effect _toneMapEffect = null!;
        private VertexPositionTexture[] _fullScreenQuad = null!;
        
        public float Exposure { get; set; } = 1.0f;  // Standard exposure value
        public bool UseAA { get; set; } = false;
        public bool UsePRAA { get; set; } = false;
        public bool UseMotionBlur { get; set; } = false;
        public float MotionBlurIntensity { get; set; } = 1.0f;
        public Vector2 BlurDirection { get; set; } = Vector2.Zero;

        public ToneMapRenderer(GraphicsDevice device)
        {
            _graphicsDevice = device;
            InitializeQuad();
        }

        public void Initialize(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            if (content != null)
            {
                try
                {
                    _toneMapEffect = content.Load<Effect>("Shaders/ToneMap");
                    Logger.Log("[ToneMapRenderer] Shader loaded successfully via ContentManager.");
                }
                catch (Exception ex)
                {
                    Logger.Log($"[ToneMapRenderer] Failed to load via ContentManager: {ex.Message}. Trying fallback...");
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
                string shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Shaders", "ToneMap.mgfxo");
                if (File.Exists(shaderPath))
                {
                    var bytes = File.ReadAllBytes(shaderPath);
                    _toneMapEffect = new Effect(_graphicsDevice, bytes);
                    Logger.Log("[ToneMapRenderer] Shader loaded via fallback (.mgfxo).");
                }
                else
                {
                    Logger.Log("[ToneMapRenderer] Warning: ToneMap shader not found (no .xnb or .mgfxo). PP will be disabled.");
                }
            }
            catch (Exception ex)
            {
                 Logger.Log($"[ToneMapRenderer] Fallback load failed: {ex.Message}");
            }
        }

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
