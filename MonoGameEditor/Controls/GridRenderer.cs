using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Renders an infinite grid on the XZ plane that follows the camera
    /// </summary>
    public class GridRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private VertexPositionColor[] _vertices;
        
        // Grid settings
        public float GridSize { get; set; } = 200f;     // Grid radius around camera
        public float CellSize { get; set; } = 1f;
        public Color MajorLineColor { get; set; } = new Color(120, 120, 120);
        public Color MinorLineColor { get; set; } = new Color(80, 80, 80);
        public Color AxisXColor { get; set; } = new Color(200, 60, 60);
        public Color AxisZColor { get; set; } = new Color(60, 60, 200);
        public int MajorLineInterval { get; set; } = 10;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateEffect();
        }

        private void CreateEffect()
        {
            _effect?.Dispose();
            _effect = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void OnDeviceReset()
        {
            CreateEffect();
        }

        public void Draw(GraphicsDevice graphicsDevice, EditorCamera camera)
        {
            if (_effect == null || _effect.IsDisposed) 
            {
                CreateEffect();
            }

            // Build grid centered on camera XZ position (snapped to grid)
            float camX = (float)Math.Floor(camera.Position.X / CellSize) * CellSize;
            float camZ = (float)Math.Floor(camera.Position.Z / CellSize) * CellSize;
            
            BuildGridAroundPosition(camX, camZ);

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.Identity;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.LineList, 
                    _vertices, 
                    0, 
                    _vertices.Length / 2);
            }
        }

        private void BuildGridAroundPosition(float centerX, float centerZ)
        {
            int linesPerAxis = (int)(GridSize / CellSize) * 2 + 1;
            int totalLines = linesPerAxis * 2;
            
            if (_vertices == null || _vertices.Length != totalLines * 2)
            {
                _vertices = new VertexPositionColor[totalLines * 2];
            }
            
            int vertexIndex = 0;
            float halfSize = GridSize;

            // Lines parallel to Z axis (varying X)
            for (int i = 0; i < linesPerAxis; i++)
            {
                float x = centerX - halfSize + i * CellSize;
                Color color = GetLineColor(x, true);
                
                _vertices[vertexIndex++] = new VertexPositionColor(new Vector3(x, 0, centerZ - halfSize), color);
                _vertices[vertexIndex++] = new VertexPositionColor(new Vector3(x, 0, centerZ + halfSize), color);
            }

            // Lines parallel to X axis (varying Z)
            for (int i = 0; i < linesPerAxis; i++)
            {
                float z = centerZ - halfSize + i * CellSize;
                Color color = GetLineColor(z, false);
                
                _vertices[vertexIndex++] = new VertexPositionColor(new Vector3(centerX - halfSize, 0, z), color);
                _vertices[vertexIndex++] = new VertexPositionColor(new Vector3(centerX + halfSize, 0, z), color);
            }
        }

        private Color GetLineColor(float position, bool isXAxis)
        {
            // Check if this is the origin axis
            if (Math.Abs(position) < CellSize * 0.5f)
            {
                return isXAxis ? AxisZColor : AxisXColor;
            }

            // Check if this is a major line
            int gridPos = (int)Math.Round(position / CellSize);
            if (gridPos % MajorLineInterval == 0)
            {
                return MajorLineColor;
            }

            return MinorLineColor;
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }
}
