using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;
using System.Linq;

namespace MonoGameEditor.Core.Graphics
{
    /// <summary>
    /// Renders gizmos for GameObjects in the scene (camera, light icons, etc.)
    /// </summary>
    public class GizmoRenderer
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Core.Gizmos.DirectionalLightGizmo _dirLightGizmo;
        private ColliderGizmoRenderer _colliderGizmoRenderer;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateEffect();
            _dirLightGizmo = new Core.Gizmos.DirectionalLightGizmo(graphicsDevice);
            _colliderGizmoRenderer = new ColliderGizmoRenderer(graphicsDevice);
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
                if (!go.IsActive) continue;
                DrawGameObjectGizmo(graphicsDevice, go, camera);
                DrawChildren(graphicsDevice, go, camera);
            }
        }

        private void DrawChildren(GraphicsDevice graphicsDevice, GameObject parent, EditorCamera camera)
        {
            foreach (var child in parent.Children)
            {
                if (!child.IsActive) continue;
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
                    DrawCameraGizmo(graphicsDevice, go, camera);
                    break;
                case GameObjectType.Light:
                    DrawLightGizmo(graphicsDevice, go, camera);
                    break;
            }

            // Draw Colliders
            DrawColliderGizmos(go, camera);

            // Draw selection outline if selected
            if (go.IsSelected)
            {
                DrawSelectionGizmo(graphicsDevice, go);
            }
        }

        private void DrawColliderGizmos(GameObject go, EditorCamera camera)
        {
            if (_colliderGizmoRenderer == null || !go.IsSelected) return;

            var world = go.Transform.WorldMatrix;

            // Box
            var box = go.GetComponent<BoxColliderComponent>();
            if (box != null && box.IsEnabled) _colliderGizmoRenderer.DrawBox(camera, world, box.Center, box.Size);

            // Sphere
            var sphere = go.GetComponent<SphereColliderComponent>();
            if (sphere != null && sphere.IsEnabled) _colliderGizmoRenderer.DrawSphere(camera, world, sphere.Center, sphere.Radius);

            // Capsule
            var capsule = go.GetComponent<CapsuleColliderComponent>();
            if (capsule != null && capsule.IsEnabled) _colliderGizmoRenderer.DrawCapsule(camera, world, capsule.Center, capsule.Radius, capsule.Height, capsule.Direction);
        }

        private void DrawCameraGizmo(GraphicsDevice graphicsDevice, GameObject go, EditorCamera camera)
        {
            var pos = go.Transform.Position;
            var component = go.GetComponent<CameraComponent>();
            if (component == null || !component.IsEnabled) return;

            var color = new Color(100, 149, 237); // Cornflower blue

            // 1. Draw Frustum if CameraComponent exists and object is selected
            if (go.IsSelected)
            {
                float fov = MathHelper.ToRadians(component.FieldOfView);
                float aspect = graphicsDevice.Viewport.AspectRatio; 
                float near = component.NearClip;
                float far = component.FarClip;

                Matrix view = Matrix.CreateLookAt(pos, pos + go.Transform.WorldMatrix.Forward, go.Transform.WorldMatrix.Up);
                Matrix projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, near, far);
                BoundingFrustum frustum = new BoundingFrustum(view * projection);

                var corners = frustum.GetCorners();

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
                
                // Tape reels
                new VertexPositionColor(new Vector3(-0.2f, 0.25f, 0) * iconScale, color), new VertexPositionColor(new Vector3(-0.2f, 0.45f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(-0.2f, 0.45f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0, 0.45f, 0) * iconScale, color),
                new VertexPositionColor(new Vector3(0, 0.45f, 0) * iconScale, color), new VertexPositionColor(new Vector3(0, 0.25f, 0) * iconScale, color),
            };

            _effect.World = Matrix.CreateBillboard(pos, camera.Position, Vector3.Up, null);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, iconVertices, 0, iconVertices.Length / 2);
            }
        }

        private void DrawLightGizmo(GraphicsDevice graphicsDevice, GameObject go, EditorCamera camera)
        {
            var lightComp = go.GetComponent<LightComponent>();
            if (lightComp == null || !lightComp.IsEnabled) return;

            if (lightComp.LightType == LightType.Directional && _dirLightGizmo != null)
            {
                _dirLightGizmo.Draw(camera, go.Transform, lightComp.Intensity);
            }
            else
            {
                var pos = go.Transform.Position;
                var color = new Color(255, 200, 50);

                var vertices = new VertexPositionColor[]
                {
                    new VertexPositionColor(pos + Vector3.UnitX * 0.5f, color), new VertexPositionColor(pos - Vector3.UnitX * 0.5f, color),
                    new VertexPositionColor(pos + Vector3.UnitY * 0.5f, color), new VertexPositionColor(pos - Vector3.UnitY * 0.5f, color),
                    new VertexPositionColor(pos + Vector3.UnitZ * 0.5f, color), new VertexPositionColor(pos - Vector3.UnitZ * 0.5f, color),
                };

                 _effect.World = Matrix.Identity; 
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
                }
            }
        }

        private void DrawSelectionGizmo(GraphicsDevice graphicsDevice, GameObject go)
        {
            var pos = go.Transform.Position;
            var size = 1f;
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
            _dirLightGizmo?.Dispose();
            _colliderGizmoRenderer?.Dispose();
        }
    }
}
