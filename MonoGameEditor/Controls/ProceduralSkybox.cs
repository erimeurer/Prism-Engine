using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MonoGameEditor.Controls
{
    public class ProceduralSkybox : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Texture2D _skyTexture;
        private VertexPositionTexture[] _vertices;
        private short[] _indices;

        // Unity-like Skybox Colors
        // Zenith: Rich Blue
        public Color TopColor { get; set; } = new Color(0.2f, 0.4f, 0.7f); 
        // Horizon: Hazy White
        public Color HorizonColor { get; set; } = new Color(0.9f, 0.9f, 0.95f); 
        // Bottom: Dark Gray
        public Color BottomColor { get; set; } = new Color(0.2f, 0.2f, 0.2f);

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            // Create Effect
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = true,
                LightingEnabled = false,
                VertexColorEnabled = false,
                FogEnabled = false
            };

            GenerateGradientTexture();
            BuildGeometry();
        }

        private void GenerateGradientTexture()
        {
            int height = 128;
            int width = 1;
            
            Color[] data = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                // t goes from 0 (Top) to 1 (Bottom)
                float t = (float)y / (height - 1);
                
                // Tight gradient logic:
                // We want the "Horizon Color" to be very concentrated around 0.5.
                // We can use a power function to push colors towards the poles (0 and 1).
                
                if (t <= 0.5f)
                {
                    // Top (0.0) -> Horizon (0.5)
                    // Normalize t to [0, 1] relative to this half
                    float localT = t / 0.5f; 
                    
                    // Power function > 1 makes the curve "stay" at 0 longer then spike to 1.
                    // effectively pushing the Top color down closer to the horizon.
                    // e.g. 0.9^4 = 0.65, so at 90% of the way down, it's still only 65% faded.
                    localT = (float)Math.Pow(localT, 4.0f); 
                    
                    data[y] = Color.Lerp(TopColor, HorizonColor, localT);
                }
                else
                {
                    // Horizon (0.5) -> Bottom (1.0)
                    // Normalize t to [0, 1]
                    float localT = (t - 0.5f) / 0.5f;
                    
                    // Invert behavior: We want it to stay Horizon-ish briefly then go dark.
                    // Actually same logic applies, we want to stay near 0 (Horizon) then ramp to 1 (Bottom)?
                    // No, localT goes 0->1. 0 is HorizonColor.
                    // If we use Pow, it stays near 0 (Horizon) longer.
                    // But user wants "short swap near horizon".
                    // So we want rapid change FROM horizon.
                    // So we need Inverse Pow? Or distinct logic.
                    // Let's use similar logic: Stay "Horizon" only very briefly.
                    // So we want the curve to shoot up to 1 quickly.
                    // y = x^(1/p) where p > 1 (Root).
                    
                    localT = (float)Math.Pow(localT, 0.25f); // 4th root. 0.1 becomes 0.56. Rapid ascent.
                    
                    data[y] = Color.Lerp(HorizonColor, BottomColor, localT);
                }
            }

            _skyTexture?.Dispose();
            _skyTexture = new Texture2D(_graphicsDevice, width, height);
            _skyTexture.SetData(data);
            
            _effect.Texture = _skyTexture;
        }

        private void BuildGeometry()
        {
            // Build a Sphere (Tessellated)
            int stacks = 16;
            int slices = 16;
            float radius = 1f;

            int vertexCount = (stacks + 1) * (slices + 1);
            int indexCount = stacks * slices * 6;

            _vertices = new VertexPositionTexture[vertexCount];
            _indices = new short[indexCount];

            int vIndex = 0;
            for (int i = 0; i <= stacks; i++)
            {
                float phi = MathHelper.Pi * i / stacks; // 0 to Pi (Top to Bottom)
                // UV Y: 0 at Top, 1 at Bottom
                float v = (float)i / stacks; 

                for (int j = 0; j <= slices; j++)
                {
                    float theta = MathHelper.TwoPi * j / slices; // 0 to 2Pi
                    float u = (float)j / slices;

                    // Spherical to Cartesian
                    // X = r sin(phi) cos(theta)
                    // Z = r sin(phi) sin(theta) (Swapped Y/Z for Z-up? No, MonoGame is Y-up usually)
                    // Standard Y-up: Y = r cos(phi)
                    float x = radius * (float)Math.Sin(phi) * (float)Math.Cos(theta);
                    float y = radius * (float)Math.Cos(phi);
                    float z = radius * (float)Math.Sin(phi) * (float)Math.Sin(theta);

                    _vertices[vIndex++] = new VertexPositionTexture(
                        new Vector3(x, y, z), 
                        new Vector2(u, v)); // We only care about V for gradient
                }
            }

            int iIndex = 0;
            for (int i = 0; i < stacks; i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    int stride = slices + 1;
                    int nextI = i + 1;
                    int nextJ = j + 1;

                    // Quad indices
                    short v0 = (short)(i * stride + j);
                    short v1 = (short)(i * stride + nextJ);
                    short v2 = (short)(nextI * stride + j);
                    short v3 = (short)(nextI * stride + nextJ);

                    // Triangle 1
                    _indices[iIndex++] = v0;
                    _indices[iIndex++] = v1;
                    _indices[iIndex++] = v2;

                    // Triangle 2
                    _indices[iIndex++] = v1;
                    _indices[iIndex++] = v3;
                    _indices[iIndex++] = v2;
                }
            }
        }

        public void Draw(EditorCamera camera)
        {
            Draw(camera.View, camera.Projection, camera.Position, camera.FarPlane);
        }

        public void Draw(Matrix view, Matrix projection, Vector3 position, float farPlane)
        {
            if (_effect == null) return;

            // Disable Depth Write so we draw behind everything 
            // (but we must draw FIRST or AFTER with special state)
            // Best practice for Skybox: Draw FIRST with DepthWrite disabled.
            
            var oldState = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];

            _graphicsDevice.DepthStencilState = DepthStencilState.None; // Or DepthRead
            _graphicsDevice.RasterizerState = RasterizerState.CullNone; // Draw from inside
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp; // Smooth gradient

            // Center cube on camera
            // Scale must be small enough to keep corners inside FarPlane. 
            // Cube diagonal is sqrt(3) * scale. So scale < FarPlane / sqrt(3) (~0.577).
            // We use 0.5f to be safe.
            Matrix world = Matrix.CreateScale(farPlane * 0.5f) * Matrix.CreateTranslation(position);
            
            _effect.World = world;
            _effect.View = view;
            _effect.Projection = projection;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList, 
                    _vertices, 
                    0, 
                    _vertices.Length, 
                    _indices, 
                    0, 
                    _indices.Length / 3);
            }

            // Restore states
            _graphicsDevice.DepthStencilState = oldState;
            _graphicsDevice.RasterizerState = oldRaster;
            _graphicsDevice.SamplerStates[0] = oldSampler;
        }

        public void Dispose()
        {
            _effect?.Dispose();
            _skyTexture?.Dispose();
        }
    }
}
