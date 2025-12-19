using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEditor.Core.Graphics
{
    /// <summary>
    /// Utility class for drawing wireframe collider gizmos
    /// </summary>
    public class ColliderGizmoRenderer : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Color _gizmoColor = new Color(0, 255, 0, 128); // Semi-transparent green

        // Cached vertices for sphere and capsule
        private VertexPositionColor[] _sphereVertices;
        private VertexPositionColor[] _capsuleVertices;
        private const int Segments = 24;

        public ColliderGizmoRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            BuildSphereGeometry();
        }

        private void BuildSphereGeometry()
        {
            var vertices = new List<VertexPositionColor>();
            
            // 3 rings (XY, XZ, YZ)
            for (int i = 0; i < Segments; i++)
            {
                float a1 = i * MathHelper.TwoPi / Segments;
                float a2 = (i + 1) * MathHelper.TwoPi / Segments;

                // XY Plane
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1), (float)Math.Sin(a1), 0), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2), (float)Math.Sin(a2), 0), _gizmoColor));

                // XZ Plane
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1), 0, (float)Math.Sin(a1)), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2), 0, (float)Math.Sin(a2)), _gizmoColor));

                // YZ Plane
                vertices.Add(new VertexPositionColor(new Vector3(0, (float)Math.Cos(a1), (float)Math.Sin(a1)), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3(0, (float)Math.Cos(a2), (float)Math.Sin(a2)), _gizmoColor));
            }

            _sphereVertices = vertices.ToArray();
        }

        public void DrawBox(EditorCamera camera, Matrix world, Vector3 center, Vector3 size)
        {
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            
            // Adjust world to include local center and size
            Matrix boxMatrix = Matrix.CreateScale(size) * Matrix.CreateTranslation(center) * world;
            _effect.World = boxMatrix;

            var vertices = new VertexPositionColor[]
            {
                // Bottom
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), _gizmoColor),
                
                // Top
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), _gizmoColor),
                
                // Sides
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), _gizmoColor),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), _gizmoColor), new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), _gizmoColor),
            };

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, vertices.Length / 2);
            }
        }

        public void DrawSphere(EditorCamera camera, Matrix world, Vector3 center, float radius)
        {
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.World = Matrix.CreateScale(radius) * Matrix.CreateTranslation(center) * world;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _sphereVertices, 0, _sphereVertices.Length / 2);
            }
        }

        public void DrawCapsule(EditorCamera camera, Matrix world, Vector3 center, float radius, float height, Core.Components.CapsuleDirection direction)
        {
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;

            // Rotation based on direction
            Matrix rotation = Matrix.Identity;
            if (direction == Core.Components.CapsuleDirection.X_Axis) rotation = Matrix.CreateRotationZ(MathHelper.PiOver2);
            else if (direction == Core.Components.CapsuleDirection.Z_Axis) rotation = Matrix.CreateRotationX(MathHelper.PiOver2);

            _effect.World = rotation * Matrix.CreateTranslation(center) * world;

            float halfHeight = height * 0.5f;
            float cylinderHalfHeight = Math.Max(0, halfHeight - radius);

            var vertices = new List<VertexPositionColor>();

            // 2 Rings (top/bottom of cylinder part)
            for (int i = 0; i < Segments; i++)
            {
                float a1 = i * MathHelper.TwoPi / Segments;
                float a2 = (i + 1) * MathHelper.TwoPi / Segments;

                // Bottom Ring
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1) * radius, -cylinderHalfHeight, (float)Math.Sin(a1) * radius), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2) * radius, -cylinderHalfHeight, (float)Math.Sin(a2) * radius), _gizmoColor));

                // Top Ring
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1) * radius, cylinderHalfHeight, (float)Math.Sin(a1) * radius), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2) * radius, cylinderHalfHeight, (float)Math.Sin(a2) * radius), _gizmoColor));
            }

            // Side Lines
            vertices.Add(new VertexPositionColor(new Vector3(radius, -cylinderHalfHeight, 0), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(radius, cylinderHalfHeight, 0), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(-radius, -cylinderHalfHeight, 0), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(-radius, cylinderHalfHeight, 0), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(0, -cylinderHalfHeight, radius), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(0, cylinderHalfHeight, radius), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(0, -cylinderHalfHeight, -radius), _gizmoColor));
            vertices.Add(new VertexPositionColor(new Vector3(0, cylinderHalfHeight, -radius), _gizmoColor));

            // Arcs (Top and Bottom caps)
            for (int i = 0; i < Segments; i++)
            {
                float a1 = i * MathHelper.Pi / Segments;
                float a2 = (i + 1) * MathHelper.Pi / Segments;

                // Top Cap Arcs
                // XY plane arc
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1) * radius, cylinderHalfHeight + (float)Math.Sin(a1) * radius, 0), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2) * radius, cylinderHalfHeight + (float)Math.Sin(a2) * radius, 0), _gizmoColor));
                
                // ZY plane arc
                vertices.Add(new VertexPositionColor(new Vector3(0, cylinderHalfHeight + (float)Math.Sin(a1) * radius, (float)Math.Cos(a1) * radius), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3(0, cylinderHalfHeight + (float)Math.Sin(a2) * radius, (float)Math.Cos(a2) * radius), _gizmoColor));

                // Bottom Cap Arcs
                // XY plane arc
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a1) * radius, -cylinderHalfHeight - (float)Math.Sin(a1) * radius, 0), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3((float)Math.Cos(a2) * radius, -cylinderHalfHeight - (float)Math.Sin(a2) * radius, 0), _gizmoColor));

                // ZY plane arc
                vertices.Add(new VertexPositionColor(new Vector3(0, -cylinderHalfHeight - (float)Math.Sin(a1) * radius, (float)Math.Cos(a1) * radius), _gizmoColor));
                vertices.Add(new VertexPositionColor(new Vector3(0, -cylinderHalfHeight - (float)Math.Sin(a2) * radius, (float)Math.Cos(a2) * radius), _gizmoColor));
            }

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices.ToArray(), 0, vertices.Count / 2);
            }
        }

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }
}
