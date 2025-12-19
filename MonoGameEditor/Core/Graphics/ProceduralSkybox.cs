using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MonoGameEditor.Core.Graphics
{
    public class ProceduralSkybox : IDisposable
    {
        private GraphicsDevice _graphicsDevice = null!;
        private BasicEffect _effect = null!;
        private Texture2D _skyTexture = null!;
        private VertexPositionTexture[] _vertices = null!;
        private short[] _indices = null!;

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
                
                if (t <= 0.5f)
                {
                    // Top (0.0) -> Horizon (0.5)
                    float localT = t / 0.5f; 
                    localT = (float)Math.Pow(localT, 4.0f); 
                    data[y] = Color.Lerp(TopColor, HorizonColor, localT);
                }
                else
                {
                    // Horizon (0.5) -> Bottom (1.0)
                    float localT = (t - 0.5f) / 0.5f;
                    localT = (float)Math.Pow(localT, 0.25f); 
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
                float phi = MathHelper.Pi * i / stacks; // 0 to Pi
                float v = (float)i / stacks; 

                for (int j = 0; j <= slices; j++)
                {
                    float theta = MathHelper.TwoPi * j / slices;
                    float u = (float)j / slices;

                    float x = radius * (float)Math.Sin(phi) * (float)Math.Cos(theta);
                    float y = radius * (float)Math.Cos(phi);
                    float z = radius * (float)Math.Sin(phi) * (float)Math.Sin(theta);

                    _vertices[vIndex++] = new VertexPositionTexture(
                        new Vector3(x, y, z), 
                        new Vector2(u, v));
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

                    short v0 = (short)(i * stride + j);
                    short v1 = (short)(i * stride + nextJ);
                    short v2 = (short)(nextI * stride + j);
                    short v3 = (short)(nextI * stride + nextJ);

                    _indices[iIndex++] = v0;
                    _indices[iIndex++] = v1;
                    _indices[iIndex++] = v2;

                    _indices[iIndex++] = v1;
                    _indices[iIndex++] = v3;
                    _indices[iIndex++] = v2;
                }
            }
        }

        public void Draw(Matrix view, Matrix projection, Vector3 position, float farPlane)
        {
            if (_effect == null) return;

            var oldState = _graphicsDevice.DepthStencilState;
            var oldRaster = _graphicsDevice.RasterizerState;
            var oldSampler = _graphicsDevice.SamplerStates[0];

            _graphicsDevice.DepthStencilState = DepthStencilState.None;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

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
