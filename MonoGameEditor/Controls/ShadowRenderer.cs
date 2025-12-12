using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using System.Linq;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Manages Shadow Map generation
    /// </summary>
    public class ShadowRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private RenderTarget2D _shadowMap;
        private Effect _shadowDepthEffect;
        private int _resolution = 4096; // High resolution for Unity-quality shadows

        public RenderTarget2D ShadowMap => _shadowMap;
        public Matrix LightViewProjection { get; private set; }

        public ShadowRenderer(GraphicsDevice graphicsDevice, Microsoft.Xna.Framework.Content.ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            LoadEffect(content);
            ResizeShadowMap(_resolution);
        }

        private void LoadEffect(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            try
            {
                // Load XNB via ContentManager
                _shadowDepthEffect = content.Load<Effect>("Shaders/ShadowDepth");
                MonoGameEditor.ViewModels.ConsoleViewModel.Log("[ShadowRenderer] ShadowDepth shader loaded successfully (XNB)");
            }
            catch (System.Exception ex) 
            { 
                 MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] Exception loading shader: {ex.Message}");
            }

        }

        public void UpdateResolution(int newSize)
        {
            if (_resolution != newSize)
            {
                _resolution = newSize;
                ResizeShadowMap(_resolution);
            }
        }

        private void ResizeShadowMap(int size)
        {
            _shadowMap?.Dispose();
            // R32F is best for precision, but Single is often SurfaceFormat.Single
            _shadowMap = new RenderTarget2D(_graphicsDevice, size, size, false, SurfaceFormat.Single, DepthFormat.Depth24);
        }

        public void BeginPass(LightComponent light, Vector3 cameraPosition, Vector3 cameraForward)
        {
            if (light == null) return;
            
            Vector3 lightDir = Vector3.Normalize(light.GameObject.Transform.Forward);
            
            // CENTER ON CAMERA: Shadows follow your view (like Unity's cascaded shadows)
            // Offset forward so shadows appear where you're looking
            Vector3 worldCenter = cameraPosition + (cameraForward * 20f);
            
            // CRITICAL: Snap worldCenter to a coarse grid to prevent shadow swimming
            // When camera moves/rotates, shadows only update in discrete 10-unit steps
            // This makes the movement invisible while keeping shadows near the player
            float gridSize = 10f;
            worldCenter.X = (float)Math.Round(worldCenter.X / gridSize) * gridSize;
            worldCenter.Y = (float)Math.Round(worldCenter.Y / gridSize) * gridSize;
            worldCenter.Z = (float)Math.Round(worldCenter.Z / gridSize) * gridSize;
            
            // Position light far from center to avoid near-plane clipping
            Vector3 lightPos = worldCenter - (lightDir * 200f);
            
            // Optimized view size: 100 units covers typical scene well
            // 4096 / 100 = 40.96 texels/unit = 2.4cm precision (excellent!)
            float viewSize = 100f;
            
            // Robust Up vector selection
            Vector3 up = Vector3.Up;
            if (Math.Abs(Vector3.Dot(lightDir, up)) > 0.99f)
                up = Vector3.Forward;

            Matrix view = Matrix.CreateLookAt(lightPos, worldCenter, up);
            Matrix projection = Matrix.CreateOrthographic(viewSize, viewSize, -500f, 500f);
            
            // TEXEL SNAPPING - Fix shadow swimming/shimmering
            Matrix shadowMatrix = view * projection;
            
            // Transform origin to shadow space
            Vector4 shadowOrigin = Vector4.Transform(Vector4.Zero, shadowMatrix);
            shadowOrigin *= (_resolution / 2f);
            
            // Round to nearest texel
            Vector4 roundedOrigin = new Vector4(
                (float)Math.Round(shadowOrigin.X),
                (float)Math.Round(shadowOrigin.Y),
                shadowOrigin.Z,
                shadowOrigin.W
            );
            
            // Calculate offset
            Vector4 roundOffset = roundedOrigin - shadowOrigin;
            roundOffset *= (2f / _resolution);
            roundOffset.Z = 0;
            roundOffset.W = 0;
            
            // Apply offset to projection matrix
            projection.M41 += roundOffset.X;
            projection.M42 += roundOffset.Y;
            
            LightViewProjection = view * projection;
            
            // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] LightPos: {lightPos} Dir: {lightDir}");
            // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] View: {view.Forward}"); 
            // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] Proj: {projection.M11}");

            // 2. Setup Render Target
            _graphicsDevice.SetRenderTarget(_shadowMap);
            _graphicsDevice.Clear(Color.White); // Depth 1.0 = Far

            // 3. Set States strictly
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullClockwise; // Backface culling = minimal bias!
            
            // 3. Setup Effect
            if (_shadowDepthEffect != null)
            {
                _shadowDepthEffect.Parameters["LightViewProjection"]?.SetValue(LightViewProjection);
            }
        }
        
        public void DrawObject(GameObject obj, ModelRendererComponent renderer)
        {
            if (_shadowDepthEffect == null) 
            {
                 // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] Skip {obj.Name}: Start NULL SHADER"); 
                 return;
            }
            if (renderer == null) return;
            
            if (renderer.CastShadows == ShadowMode.Off) 
            {
                 // MonoGameEditor.ViewModels.ConsoleViewModel.Log($"[ShadowRenderer] Skip {obj.Name}: CastShadows is OFF");
                 return;
            }
            
            // Render logic
            // We need to bypass the ModelRenderer's standard Draw because we want to use OUR shader (ShadowDepth)
            // But ModelRenderer usually draws itself. 
            // We'll implemented a dedicated "DrawDepth" helper or use local logic if we can access buffers.
            // Since ModelRendererComponent encapsulates buffers privately, let's assume we add a "DrawWithEffect" method or similar.
            
            // For now, let's use a public method on ModelRendererComponent if we add one, or reflection, 
            // OR simpler: Add DrawDepth(Effect effect) to ModelRendererComponent.
            
            renderer.DrawWithCustomEffect(_shadowDepthEffect, LightViewProjection);
        }

        public void EndPass()
        {
            _graphicsDevice.SetRenderTarget(null);
        }
    }
}
