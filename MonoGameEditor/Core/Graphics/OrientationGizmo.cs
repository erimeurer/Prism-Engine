using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEditor.Core.Graphics
{
    /// <summary>
    /// Renders an orientation gizmo (compass) in the corner showing X, Y, Z axes
    /// </summary>
    public class OrientationGizmo
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private VertexPositionColor[] _axisLines;
        
        public float Size { get; set; } = 60f;
        public int Padding { get; set; } = 20;
        
        public Color AxisXColor { get; set; } = new Color(220, 80, 80);
        public Color AxisYColor { get; set; } = new Color(80, 220, 80);
        public Color AxisZColor { get; set; } = new Color(80, 80, 220);

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateEffect();
            BuildAxes();
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

        private void BuildAxes()
        {
            _axisLines = new VertexPositionColor[6];
            _axisLines[0] = new VertexPositionColor(Vector3.Zero, AxisXColor);
            _axisLines[1] = new VertexPositionColor(Vector3.UnitX, AxisXColor);
            _axisLines[2] = new VertexPositionColor(Vector3.Zero, AxisYColor);
            _axisLines[3] = new VertexPositionColor(Vector3.UnitY, AxisYColor);
            _axisLines[4] = new VertexPositionColor(Vector3.Zero, AxisZColor);
            _axisLines[5] = new VertexPositionColor(Vector3.UnitZ, AxisZColor);
        }

        public void OnDeviceReset()
        {
            CreateEffect();
        }

        public void Draw(GraphicsDevice graphicsDevice, EditorCamera mainCamera)
        {
            if (_effect == null || _axisLines == null || _effect.IsDisposed)
            {
                CreateEffect();
            }

            var viewport = graphicsDevice.Viewport;
            float gizmoX = Padding + Size / 2;
            float gizmoY = viewport.Height - Padding - Size / 2;
            
            var oldViewport = graphicsDevice.Viewport;
            
            Vector3 cameraDirection = Vector3.Normalize(mainCamera.Position - mainCamera.Target);
            Vector3 gizmoEye = cameraDirection * 3f;
            
            _effect.View = Matrix.CreateLookAt(gizmoEye, Vector3.Zero, mainCamera.Up);
            _effect.Projection = Matrix.CreateOrthographic(2f, 2f, 0.1f, 10f);
            _effect.World = Matrix.Identity;
            
            graphicsDevice.Viewport = new Viewport(
                (int)(gizmoX - Size / 2), 
                (int)(gizmoY - Size / 2), 
                (int)Size, 
                (int)Size);

            var oldDepthState = graphicsDevice.DepthStencilState;
            graphicsDevice.DepthStencilState = DepthStencilState.None;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _axisLines, 0, 3);
            }

            graphicsDevice.DepthStencilState = oldDepthState;
            graphicsDevice.Viewport = oldViewport;
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }
}
