using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Controls;

namespace MonoGameEditor.Core.Gizmos
{
    public class DirectionalLightGizmo : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private VertexPositionColor[] _sunVertices;
        
        public DirectionalLightGizmo(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            CreateSunGeometry();
        }

        private void CreateSunGeometry()
        {
            var color = new Color(255, 200, 50); // Sun Yellow
            int segments = 24;
            float radius = 0.5f;
            _sunVertices = new VertexPositionColor[segments * 2 + 16]; // Circle + 8 rays
            
            // Circle loop
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * MathHelper.TwoPi / segments;
                float angle2 = (i + 1) * MathHelper.TwoPi / segments;
                
                _sunVertices[i * 2] = new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle1) * radius, (float)Math.Sin(angle1) * radius, 0), 
                    color);
                _sunVertices[i * 2 + 1] = new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle2) * radius, (float)Math.Sin(angle2) * radius, 0), 
                    color);
            }
            
            // Decorative rays around the sun disk
            float rayInner = radius * 1.2f;
            float rayOuter = radius * 1.6f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathHelper.TwoPi / 8;
                int idx = segments * 2 + i * 2;
                _sunVertices[idx] = new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle) * rayInner, (float)Math.Sin(angle) * rayInner, 0),
                    color);
                _sunVertices[idx + 1] = new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle) * rayOuter, (float)Math.Sin(angle) * rayOuter, 0),
                    color);
            }
        }

        public void Draw(EditorCamera camera, Transform transform, float intensity)
        {
            if (_effect == null || _effect.IsDisposed) return;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            
            // 1. Draw Sun Icon (Billboard)
            // It should always face the camera but be located at the light's position
            var pos = transform.Position;
            _effect.World = Matrix.CreateBillboard(pos, camera.Position, camera.Up, camera.Forward);
            
            // Draw Icon
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _sunVertices, 0, _sunVertices.Length / 2);
            }

            // 2. Draw Direction Arrow
            // This needs to be in World space, pointing in the light's direction
            Vector3 direction = transform.Forward;
            direction.Normalize();
            
            float length = 2.0f + (intensity * 0.2f); // Scale slightly with intensity
            length = MathHelper.Clamp(length, 2f, 10f); // Cap length
            
            var arrowColor = Color.Yellow;
            var arrowEnd = pos + direction * length;

            var arrowVertices = new VertexPositionColor[]
            {
                // Main line
                new VertexPositionColor(pos, arrowColor),
                new VertexPositionColor(arrowEnd, arrowColor),
            };

            // Draw Line in World Space
            _effect.World = Matrix.Identity; 
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, arrowVertices, 0, 1);
            }
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }
}
