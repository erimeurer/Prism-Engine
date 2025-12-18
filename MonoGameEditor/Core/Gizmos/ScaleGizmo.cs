using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MonoGameEditor.Core.Gizmos
{
    public class ScaleGizmo
    {
        private GraphicsDevice _device;
        private BasicEffect _effect;

        // Axis Colors
        private readonly Color _colorX = Color.Red;
        private readonly Color _colorY = Color.Green;
        private readonly Color _colorZ = Color.Blue;
        private readonly Color _colorCenter = Color.White;
        private readonly Color _colorSelected = Color.Yellow;

        // Selection State
        private enum GizmoAxis { None, X, Y, Z, Uniform }
        private GizmoAxis _hoverAxis = GizmoAxis.None;

        private VertexPositionColor[] _cubeVertices;
        private VertexPositionColor[] _lineVertices;

        public ScaleGizmo(GraphicsDevice device)
        {
            _device = device;
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                LightingEnabled = false
            };
            
            BuildGeometry();
        }

        private void BuildGeometry()
        {
            // Unit Cube (for axis tips)
            var vertices = new List<VertexPositionColor>();
            float size = 0.5f;
            Vector3[] p = new Vector3[] {
                new Vector3(-size, -size,  size), new Vector3( size, -size,  size), new Vector3( size,  size,  size), new Vector3(-size,  size,  size),
                new Vector3(-size, -size, -size), new Vector3( size, -size, -size), new Vector3( size,  size, -size), new Vector3(-size,  size, -size)
            };

            // 6 faces
            AddFace(vertices, p[0], p[1], p[2], p[3]); // Front
            AddFace(vertices, p[1], p[5], p[6], p[2]); // Right
            AddFace(vertices, p[5], p[4], p[7], p[6]); // Back
            AddFace(vertices, p[4], p[0], p[3], p[7]); // Left
            AddFace(vertices, p[3], p[2], p[6], p[7]); // Top
            AddFace(vertices, p[4], p[5], p[1], p[0]); // Bottom
            
            _cubeVertices = vertices.ToArray();

            _lineVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(Vector3.Zero, Color.White),
                new VertexPositionColor(Vector3.Up, Color.White)
            };
        }

        private void AddFace(List<VertexPositionColor> list, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
        {
            list.Add(new VertexPositionColor(v1, Color.White));
            list.Add(new VertexPositionColor(v2, Color.White));
            list.Add(new VertexPositionColor(v3, Color.White));
            list.Add(new VertexPositionColor(v1, Color.White));
            list.Add(new VertexPositionColor(v3, Color.White));
            list.Add(new VertexPositionColor(v4, Color.White));
        }

        public void Draw(MonoGameEditor.Controls.EditorCamera camera, Vector3 position)
        {
            if (camera == null) return;

            float gizmoScale = 2.0f; 
            float cubeSize = 0.2f; 
            float centerSize = 0.3f;

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.VertexColorEnabled = false;
            
            var originalRasterizerState = _device.RasterizerState;
            var originalBlendState = _device.BlendState;

            _device.RasterizerState = RasterizerState.CullNone;
            _device.BlendState = BlendState.AlphaBlend;

            try
            {
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    // --- Axis Lines & Handles ---
                    DrawAxis(pass, position, Vector3.Right, _hoverAxis == GizmoAxis.X ? _colorSelected : _colorX, gizmoScale, cubeSize);
                    DrawAxis(pass, position, Vector3.Up, _hoverAxis == GizmoAxis.Y ? _colorSelected : _colorY, gizmoScale, cubeSize);
                    DrawAxis(pass, position, Vector3.Forward, _hoverAxis == GizmoAxis.Z ? _colorSelected : _colorZ, gizmoScale, cubeSize);

                    // --- Center Uniform Handle ---
                    _effect.DiffuseColor = (_hoverAxis == GizmoAxis.Uniform ? _colorSelected : _colorCenter).ToVector3();
                    _effect.World = Matrix.CreateScale(centerSize) * Matrix.CreateTranslation(position);
                    pass.Apply();
                    _device.DrawUserPrimitives(PrimitiveType.TriangleList, _cubeVertices, 0, _cubeVertices.Length / 3);
                }
            }
            finally
            {
                _device.RasterizerState = originalRasterizerState;
                _device.BlendState = originalBlendState;
            }
        }

        private void DrawAxis(EffectPass pass, Vector3 origin, Vector3 direction, Color color, float totalLength, float cubeSize)
        {
            _effect.DiffuseColor = color.ToVector3();
            
            // Line
            Matrix rotation = RotateToDirection(Vector3.Up, direction);
            _effect.World = Matrix.CreateScale(1, totalLength, 1) * rotation * Matrix.CreateTranslation(origin);
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.LineList, _lineVertices, 0, 1);

            // Tip Cube
            _effect.World = Matrix.CreateScale(cubeSize) * Matrix.CreateTranslation(0, totalLength, 0) * rotation * Matrix.CreateTranslation(origin);
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _cubeVertices, 0, _cubeVertices.Length / 3);
        }

        private Matrix RotateToDirection(Vector3 from, Vector3 to)
        {
            if (from == to) return Matrix.Identity;
            if (from == -to) return Matrix.CreateScale(-1, -1, -1);
            
            Vector3 axis = Vector3.Cross(from, to);
            float dot = Vector3.Dot(Vector3.Normalize(from), Vector3.Normalize(to));
            float angle = (float)Math.Acos(MathHelper.Clamp(dot, -1f, 1f));
            return Matrix.CreateFromAxisAngle(axis, angle);
        }

        private bool _isDragging;
        private GizmoAxis _dragAxis;
        private Vector3 _dragStartPoint;
        private Vector3 _objectStartScale;

        public void Update(Ray ray, bool isMouseDown, MonoGameEditor.Core.Transform targetTransform)
        {
            if (targetTransform == null) return;

            if (isMouseDown && !_isDragging)
            {
                if (_hoverAxis != GizmoAxis.None)
                {
                    _isDragging = true;
                    _dragAxis = _hoverAxis;
                    _objectStartScale = targetTransform.LocalScale;
                    _dragStartPoint = GetDragPoint(ray, targetTransform.Position, _dragAxis);
                    return;
                }
            }
            else if (!isMouseDown && _isDragging)
            {
                _isDragging = false;
                _dragAxis = GizmoAxis.None;
            }

            if (_isDragging)
            {
                Vector3 currentPoint = GetDragPoint(ray, targetTransform.Position, _dragAxis);
                Vector3 delta = (currentPoint - _dragStartPoint);

                Vector3 newScale = _objectStartScale;

                if (_dragAxis == GizmoAxis.X) newScale.X += delta.X;
                else if (_dragAxis == GizmoAxis.Y) newScale.Y += delta.Y;
                else if (_dragAxis == GizmoAxis.Z) newScale.Z -= delta.Z; // Inverted Z in MonoGame
                else if (_dragAxis == GizmoAxis.Uniform)
                {
                    float uniformDelta = (delta.X + delta.Y - delta.Z) / 3.0f;
                    newScale += new Vector3(uniformDelta);
                }

                // Prevent negative or too small scale
                newScale.X = Math.Max(newScale.X, 0.001f);
                newScale.Y = Math.Max(newScale.Y, 0.001f);
                newScale.Z = Math.Max(newScale.Z, 0.001f);

                targetTransform.LocalScale = newScale;
            }
            else
            {
                // Hit Test
                _hoverAxis = GizmoAxis.None;
                
                float scale = 2.0f;
                float thickness = 0.2f; 
                Vector3 pos = targetTransform.Position;

                // Center Box (Uniform)
                BoundingBox centerBox = new BoundingBox(pos - new Vector3(thickness), pos + new Vector3(thickness));
                float? distCenter = ray.Intersects(centerBox);

                // Axes Boxes
                BoundingBox boxX = new BoundingBox(pos + new Vector3(0, -thickness, -thickness), pos + new Vector3(scale, thickness, thickness));
                BoundingBox boxY = new BoundingBox(pos + new Vector3(-thickness, 0, -thickness), pos + new Vector3(thickness, scale, thickness));
                BoundingBox boxZ = new BoundingBox(pos + new Vector3(-thickness, -thickness, -scale), pos + new Vector3(thickness, thickness, 0)); 

                float? distX = ray.Intersects(boxX);
                float? distY = ray.Intersects(boxY);
                float? distZ = ray.Intersects(boxZ);

                // Find closest hit
                float closest = float.MaxValue;
                
                if (distCenter != null && distCenter < closest) { closest = distCenter.Value; _hoverAxis = GizmoAxis.Uniform; }
                if (distX != null && distX < closest) { closest = distX.Value; _hoverAxis = GizmoAxis.X; }
                if (distY != null && distY < closest) { closest = distY.Value; _hoverAxis = GizmoAxis.Y; }
                if (distZ != null && distZ < closest) { closest = distZ.Value; _hoverAxis = GizmoAxis.Z; }
            }
        }

        private Vector3 GetDragPoint(Ray ray, Vector3 center, GizmoAxis axis)
        {
             Vector3 planeNormal = Vector3.Up;
             
             if (axis == GizmoAxis.X) 
             {
                 float dotZ = Math.Abs(ray.Direction.Z);
                 float dotY = Math.Abs(ray.Direction.Y);
                 planeNormal = (dotY > dotZ) ? Vector3.Up : Vector3.Forward;
             }
             else if (axis == GizmoAxis.Y) 
             {
                 float dotZ = Math.Abs(ray.Direction.Z);
                 float dotX = Math.Abs(ray.Direction.X);
                 planeNormal = (dotZ > dotX) ? Vector3.Forward : Vector3.Right;
             }
             else if (axis == GizmoAxis.Z) 
             {
                 float dotY = Math.Abs(ray.Direction.Y);
                 float dotX = Math.Abs(ray.Direction.X);
                 planeNormal = (dotY > dotX) ? Vector3.Up : Vector3.Right;
             }
             else if (axis == GizmoAxis.Uniform)
             {
                 // For uniform scaling, pick a plane that matches camera better
                 float dotX = Math.Abs(ray.Direction.X);
                 float dotY = Math.Abs(ray.Direction.Y);
                 float dotZ = Math.Abs(ray.Direction.Z);
                 if (dotX > dotY && dotX > dotZ) planeNormal = Vector3.Right;
                 else if (dotY > dotX && dotY > dotZ) planeNormal = Vector3.Up;
                 else planeNormal = Vector3.Forward;
             }
             
             Plane plane = new Plane(planeNormal, -Vector3.Dot(planeNormal, center));

             float? dist = ray.Intersects(plane);
             if (dist != null)
             {
                 return ray.Position + ray.Direction * dist.Value;
             }
             return center;
        }
    }
}
