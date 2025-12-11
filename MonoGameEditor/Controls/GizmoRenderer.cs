using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Renders gizmos for GameObjects in the scene (camera, light icons, etc.)
    /// </summary>
    public class GizmoRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;

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

        public void Draw(GraphicsDevice graphicsDevice, EditorCamera camera)
        {
            if (_effect == null || _effect.IsDisposed)
            {
                CreateEffect();
            }

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            foreach (var go in SceneManager.Instance.RootObjects)
            {
                DrawGameObjectGizmo(graphicsDevice, go, camera);
                DrawChildren(graphicsDevice, go, camera);
            }
        }

        private void DrawChildren(GraphicsDevice graphicsDevice, GameObject parent, EditorCamera camera)
        {
            foreach (var child in parent.Children)
            {
                DrawGameObjectGizmo(graphicsDevice, child, camera);
                DrawChildren(graphicsDevice, child, camera);
            }
        }

        private void DrawGameObjectGizmo(GraphicsDevice graphicsDevice, GameObject go, EditorCamera camera)
        {
            if (!go.IsActive) return;

            switch (go.ObjectType)
            {
                case GameObjectType.Camera:
                    DrawCameraGizmo(graphicsDevice, go);
                    break;
                case GameObjectType.Light:
                    DrawLightGizmo(graphicsDevice, go);
                    break;
            }

            // Draw selection outline if selected
            if (go.IsSelected)
            {
                DrawSelectionGizmo(graphicsDevice, go);
            }
        }

        private void DrawCameraGizmo(GraphicsDevice graphicsDevice, GameObject go)
        {
            var pos = go.Transform.Position;
            var rotation = go.Transform.Rotation;
            var component = go.GetComponent<MonoGameEditor.Core.Components.CameraComponent>();
            var color = new Color(100, 149, 237); // Cornflower blue

            // 1. Draw Frustum if CameraComponent exists and object is selected
            if (component != null && go.IsSelected)
            {
                // Calculate frustum corners based on camera properties
                float fov = MathHelper.ToRadians(component.FieldOfView);
                float aspect = graphicsDevice.Viewport.AspectRatio; // Use current viewport aspect
                float near = component.NearClip;
                float far = component.FarClip;

                // Create a temporary view matrix from object transform
                Matrix view = Matrix.CreateLookAt(pos, pos + go.Transform.WorldMatrix.Forward, go.Transform.WorldMatrix.Up);
                Matrix projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, near, far);
                BoundingFrustum frustum = new BoundingFrustum(view * projection);

                var corners = frustum.GetCorners();

                // Draw frustum lines
                var vertices = new VertexPositionColor[]
                {
                    // Near plane
                    new VertexPositionColor(corners[0], color), new VertexPositionColor(corners[1], color),
                    new VertexPositionColor(corners[1], color), new VertexPositionColor(corners[2], color),
                    new VertexPositionColor(corners[2], color), new VertexPositionColor(corners[3], color),
                    new VertexPositionColor(corners[3], color), new VertexPositionColor(corners[0], color),
                    
                    // Far plane
                    new VertexPositionColor(corners[4], color), new VertexPositionColor(corners[5], color),
                    new VertexPositionColor(corners[5], color), new VertexPositionColor(corners[6], color),
                    new VertexPositionColor(corners[6], color), new VertexPositionColor(corners[7], color),
                    new VertexPositionColor(corners[7], color), new VertexPositionColor(corners[4], color),
                    
                    // Connecting lines
                    new VertexPositionColor(corners[0], color), new VertexPositionColor(corners[4], color),
                    new VertexPositionColor(corners[1], color), new VertexPositionColor(corners[5], color),
                    new VertexPositionColor(corners[2], color), new VertexPositionColor(corners[6], color),
                    new VertexPositionColor(corners[3], color), new VertexPositionColor(corners[7], color),
                };

                _effect.World = Matrix.Identity;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
                }
            }

            // 2. Draw 2D Camera Icon (Billboard)
            // Simplified vector camera icon
            float iconScale = 0.5f;
            var iconVertices = new VertexPositionColor[]
            {
                // Body box
                new VertexPositionColor(new Vector3(-0.4f, -0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0.4f, -0.25f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0.4f, -0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0.4f, 0.25f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0.4f, 0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(-0.4f, 0.25f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(-0.4f, 0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(-0.4f, -0.25f, 0) * iconScale, color),
                
                // Lens triangle
                new VertexPositionColor(new Vector3(0.4f, 0.1f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0.6f, 0.2f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0.6f, 0.2f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0.6f, -0.2f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0.6f, -0.2f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0.4f, -0.1f, 0) * iconScale, color),
                
                // Tape reels (circles simplified)
                new VertexPositionColor(new Vector3(-0.2f, 0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(-0.2f, 0.45f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(-0.2f, 0.45f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0, 0.45f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0, 0.45f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0, 0.25f, 0) * iconScale, color),
            };

            // Billboard transform
            _effect.World = Matrix.CreateBillboard(pos, _effect.View.Translation, Vector3.Up, null);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, iconVertices, 0, iconVertices.Length / 2);
            }
        }

        private void DrawLightGizmo(GraphicsDevice graphicsDevice, GameObject go)
        {
            var pos = go.Transform.Position;
            var color = new Color(255, 200, 50); // Yellow/orange
            
            // Sun icon - circle with rays
            int segments = 16;
            float radius = 0.4f;
            var vertices = new VertexPositionColor[segments * 2 + 16]; // Circle + 8 rays
            
            // Circle
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * MathHelper.TwoPi / segments;
                float angle2 = (i + 1) * MathHelper.TwoPi / segments;
                
                vertices[i * 2] = new VertexPositionColor(
                    new Vector3((float)System.Math.Cos(angle1) * radius, (float)System.Math.Sin(angle1) * radius, 0), 
                    color);
                vertices[i * 2 + 1] = new VertexPositionColor(
                    new Vector3((float)System.Math.Cos(angle2) * radius, (float)System.Math.Sin(angle2) * radius, 0), 
                    color);
            }
            
            // Rays
            float rayInner = radius * 1.3f;
            float rayOuter = radius * 2f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathHelper.TwoPi / 8;
                int idx = segments * 2 + i * 2;
                vertices[idx] = new VertexPositionColor(
                    new Vector3((float)System.Math.Cos(angle) * rayInner, (float)System.Math.Sin(angle) * rayInner, 0),
                    color);
                vertices[idx + 1] = new VertexPositionColor(
                    new Vector3((float)System.Math.Cos(angle) * rayOuter, (float)System.Math.Sin(angle) * rayOuter, 0),
                    color);
            }

            // Billboard effect - face camera
            Vector3 forward = Vector3.Normalize(pos - _effect.View.Translation);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
            Vector3 up = Vector3.Cross(forward, right);
            
            _effect.World = Matrix.CreateBillboard(pos, _effect.View.Translation, Vector3.Up, null);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
            }
        }

        private void DrawSelectionGizmo(GraphicsDevice graphicsDevice, GameObject go)
        {
            var pos = go.Transform.Position;
            var color = new Color(255, 165, 0); // Orange selection
            
            // Simple axis lines from position
            float size = 1f;
            var vertices = new VertexPositionColor[]
            {
                // X axis (red)
                new VertexPositionColor(pos, new Color(255, 80, 80)),
                new VertexPositionColor(pos + Vector3.UnitX * size, new Color(255, 80, 80)),
                // Y axis (green)
                new VertexPositionColor(pos, new Color(80, 255, 80)),
                new VertexPositionColor(pos + Vector3.UnitY * size, new Color(80, 255, 80)),
                // Z axis (blue)
                new VertexPositionColor(pos, new Color(80, 80, 255)),
                new VertexPositionColor(pos + Vector3.UnitZ * size, new Color(80, 80, 255)),
            };

            _effect.World = Matrix.Identity;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 3);
            }
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }
}
