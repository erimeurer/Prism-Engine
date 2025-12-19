using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using MonoGameEditor.Core.Graphics;

namespace MonoGameEditor.Core.Gizmos
{
    public class RotationGizmo
    {
        private GraphicsDevice _device;
        private BasicEffect _effect;

        private readonly Color _colorX = Color.Red;
        private readonly Color _colorY = Color.Green;
        private readonly Color _colorZ = Color.Blue;
        private readonly Color _colorFree = Color.Gray;
        private readonly Color _colorSelected = Color.Yellow;

        private enum GizmoAxis { None, X, Y, Z, Screen }
        private GizmoAxis _hoverAxis = GizmoAxis.None;

        private VertexPositionColor[] _circleVertices;
        private const int CircleSegments = 64;

        public RotationGizmo(GraphicsDevice device)
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
            var vertices = new List<VertexPositionColor>();
            float radius = 1.0f;

            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = (float)i / CircleSegments * MathHelper.TwoPi;
                Vector3 pos = new Vector3((float)Math.Cos(angle) * radius, 0, (float)Math.Sin(angle) * radius);
                vertices.Add(new VertexPositionColor(pos, Color.White));
            }
            _circleVertices = vertices.ToArray();
        }

        public void Draw(EditorCamera camera, Vector3 position)
        {
            if (camera == null) return;

            float scale = 2.0f; 

            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            _effect.VertexColorEnabled = false;

            // X Axis (Red) - Ring in YZ plane (Normal X)
            DrawCircle(position, Vector3.Right, _hoverAxis == GizmoAxis.X ? _colorSelected : _colorX, scale);

            // Y Axis (Green) - Ring in XZ plane (Normal Y)
            DrawCircle(position, Vector3.Up, _hoverAxis == GizmoAxis.Y ? _colorSelected : _colorY, scale);

            // Z Axis (Blue) - Ring in XY plane (Normal Z)
            DrawCircle(position, Vector3.Forward, _hoverAxis == GizmoAxis.Z ? _colorSelected : _colorZ, scale);
            
            // Screen Space (Grey) - Ring facing camera
            // DrawCircle(position, camera.Transform.Forward, _hoverAxis == GizmoAxis.Screen ? _colorSelected : _colorFree, scale * 1.2f);
        }

        private void DrawCircle(Vector3 center, Vector3 normal, Color color, float radius)
        {
            _effect.DiffuseColor = color.ToVector3();
            
            // Default circle is in XZ plane (Normal Y)
            Matrix rotation = RotateToDirection(Vector3.Up, normal);
            Matrix world = Matrix.CreateScale(radius) * rotation * Matrix.CreateTranslation(center);
            
            _effect.World = world;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineStrip, _circleVertices, 0, _circleVertices.Length - 1);
            }
        }

        private Matrix RotateToDirection(Vector3 from, Vector3 to)
        {
            if (from == to) return Matrix.Identity;
            if (from == -to) return Matrix.CreateScale(1, -1, 1); // Flip Y
            
            Vector3 axis = Vector3.Cross(from, to);
             if (axis.LengthSquared() < 0.001f) // Parallel but maybe opposite? Handled above.
                return Matrix.Identity;
            
            float angle = (float)Math.Acos(MathHelper.Clamp(Vector3.Dot(Vector3.Normalize(from), Vector3.Normalize(to)), -1, 1));
            return Matrix.CreateFromAxisAngle(axis, angle);
        }

        // Interaction Logic
        private bool _isDragging;
        private GizmoAxis _dragAxis;
        private Vector3 _lastDragVector;
        private Plane _dragPlane;

        public void Update(Ray ray, bool isMouseDown, MonoGameEditor.Core.Transform targetTransform)
        {
            if (targetTransform == null) return;

            if (isMouseDown && !_isDragging)
            {
                if (_hoverAxis != GizmoAxis.None)
                {
                    _isDragging = true;
                    _dragAxis = _hoverAxis;
                    
                    Vector3 normal = GetAxisNormal(_dragAxis);
                    _dragPlane = new Plane(normal, -Vector3.Dot(normal, targetTransform.Position));
                    
                    float? hit = ray.Intersects(_dragPlane);
                    if (hit != null)
                    {
                        Vector3 hitPoint = ray.Position + ray.Direction * hit.Value;
                        _lastDragVector = Vector3.Normalize(hitPoint - targetTransform.Position);
                    }
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
                float? hit = ray.Intersects(_dragPlane);
                if (hit != null)
                {
                    Vector3 hitPoint = ray.Position + ray.Direction * hit.Value;
                    Vector3 currentVector = Vector3.Normalize(hitPoint - targetTransform.Position);

                    // Angle between last and current
                    Vector3 cross = Vector3.Cross(_lastDragVector, currentVector);
                    float dot = Vector3.Dot(_lastDragVector, currentVector);
                    float angle = (float)Math.Atan2(cross.Length(), dot);
                    
                    // Determine sign based on plane normal
                    if (Vector3.Dot(cross, _dragPlane.Normal) < 0) angle = -angle;

                    if (Math.Abs(angle) > 0.0001f)
                    {
                        // Convert angle to degrees
                        float angleDegrees = MathHelper.ToDegrees(angle);
                        
                        // Apply rotation directly to the corresponding Euler axis
                        Vector3 currentRotation = targetTransform.Rotation;
                        
                        switch (_dragAxis)
                        {
                            case GizmoAxis.X:
                                currentRotation.X += angleDegrees;
                                break;
                            case GizmoAxis.Y:
                                currentRotation.Y += angleDegrees;
                                break;
                            case GizmoAxis.Z:
                                currentRotation.Z += angleDegrees;
                                break;
                        }
                        
                        targetTransform.Rotation = currentRotation;
                        _lastDragVector = currentVector;
                    }
                }
            }
            else
            {
                // Hover Detection (Ray vs Ring)
                _hoverAxis = GizmoAxis.None;
                float closestDist = float.MaxValue;
                float radius = 2.0f;
                float threshold = 0.2f; // Distance from ring line

                CheckRingHover(ray, targetTransform.Position, Vector3.Right, radius, threshold, GizmoAxis.X, ref closestDist);
                CheckRingHover(ray, targetTransform.Position, Vector3.Up, radius, threshold, GizmoAxis.Y, ref closestDist);
                CheckRingHover(ray, targetTransform.Position, Vector3.Forward, radius, threshold, GizmoAxis.Z, ref closestDist);
            }
        }

        private Vector3 GetAxisNormal(GizmoAxis axis)
        {
            switch(axis)
            {
                case GizmoAxis.X: return Vector3.Right;
                case GizmoAxis.Y: return Vector3.Up;
                case GizmoAxis.Z: return Vector3.Forward;
                default: return Vector3.Up;
            }
        }

        private void CheckRingHover(Ray ray, Vector3 center, Vector3 normal, float radius, float threshold, GizmoAxis axis, ref float closestDist)
        {
            // Ray Plane intersection
            Plane plane = new Plane(normal, -Vector3.Dot(normal, center));
            float? hit = ray.Intersects(plane);
            if (hit != null)
            {
                Vector3 hitPoint = ray.Position + ray.Direction * hit.Value;
                float distFromCenter = Vector3.Distance(hitPoint, center);
                float distFromRing = Math.Abs(distFromCenter - radius);

                if (distFromRing < threshold)
                {
                    if (hit.Value < closestDist)
                    {
                        closestDist = hit.Value;
                        _hoverAxis = axis;
                    }
                }
            }
        }
    }
}
