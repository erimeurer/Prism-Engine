using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace MonoGameEditor.Core.Gizmos
{
    public class TranslationGizmo
    {
        private GraphicsDevice _device;
        private BasicEffect _effect;

        // Axis Colors
        private readonly Microsoft.Xna.Framework.Color _colorX = Microsoft.Xna.Framework.Color.Red;
        private readonly Microsoft.Xna.Framework.Color _colorY = Microsoft.Xna.Framework.Color.Green;
        private readonly Microsoft.Xna.Framework.Color _colorZ = Microsoft.Xna.Framework.Color.Blue;
        private readonly Microsoft.Xna.Framework.Color _colorSelected = Microsoft.Xna.Framework.Color.Yellow;

        // Selection State
        private enum GizmoAxis { None, X, Y, Z, XY, XZ, YZ }
        private GizmoAxis _hoverAxis = GizmoAxis.None;

        private VertexPositionColor[] _coneVertices;
        private VertexPositionColor[] _lineVertices;
        private VertexPositionColor[] _quadVertices;

        public TranslationGizmo(GraphicsDevice device)
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
            float height = 1.0f;
            float radius = 0.25f;
            int tessellation = 16;
            
            var vertices = new List<VertexPositionColor>();
            Vector3 tip = Vector3.Up * height;

            for (int i = 0; i < tessellation; i++)
            {
                float angle = (float)i / tessellation * MathHelper.TwoPi;
                float nextAngle = (float)(i + 1) / tessellation * MathHelper.TwoPi;
                
                Vector3 p1 = new Vector3((float)Math.Cos(angle) * radius, 0, (float)Math.Sin(angle) * radius);
                Vector3 p2 = new Vector3((float)Math.Cos(nextAngle) * radius, 0, (float)Math.Sin(nextAngle) * radius);
                
                vertices.Add(new VertexPositionColor(p1, Color.White));
                vertices.Add(new VertexPositionColor(tip, Color.White));
                vertices.Add(new VertexPositionColor(p2, Color.White));
                
                vertices.Add(new VertexPositionColor(Vector3.Zero, Color.White));
                vertices.Add(new VertexPositionColor(p2, Color.White));
                vertices.Add(new VertexPositionColor(p1, Color.White));
            }
            _coneVertices = vertices.ToArray();

             _lineVertices = new VertexPositionColor[]
             {
                 new VertexPositionColor(Vector3.Zero, Color.White),
                 new VertexPositionColor(Vector3.Up, Color.White)
             };

             // Plane Quad (Unit Square on XY plane)
             _quadVertices = new VertexPositionColor[]
             {
                 new VertexPositionColor(new Vector3(0,0,0), Color.White),
                 new VertexPositionColor(new Vector3(1,0,0), Color.White),
                 new VertexPositionColor(new Vector3(0,1,0), Color.White),
                 
                 new VertexPositionColor(new Vector3(1,0,0), Color.White),
                 new VertexPositionColor(new Vector3(1,1,0), Color.White),
                 new VertexPositionColor(new Vector3(0,1,0), Color.White)
             };
        }

        public void Draw(MonoGameEditor.Controls.EditorCamera camera, Microsoft.Xna.Framework.Vector3 position, Quaternion objectRotation = default, bool useLocalOrientation = false)
        {
            if (camera == null) return;

            float gizmoScale = 2.0f; 
            float coneHeight = 0.5f; 
            float coneRadius = 0.15f;
            float planeSize = 0.5f; 
            float alpha = 0.6f;

            // Create rotation matrix for local mode
            Matrix orientationMatrix = useLocalOrientation && objectRotation != default 
                ? Matrix.CreateFromQuaternion(objectRotation)
                : Matrix.Identity;

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

                    // Transform axes by orientation
                    Vector3 rightAxis = Vector3.Transform(Vector3.Right, orientationMatrix);
                    Vector3 upAxis = Vector3.Transform(Vector3.Up, orientationMatrix);
                    Vector3 forwardAxis = Vector3.Transform(Vector3.Forward, orientationMatrix);

                    // --- Arrows ---
                    _effect.Alpha = 1.0f;
                    DrawArrow(pass, position, rightAxis, _hoverAxis == GizmoAxis.X ? _colorSelected : _colorX, gizmoScale, coneHeight, coneRadius);
                    DrawArrow(pass, position, upAxis, _hoverAxis == GizmoAxis.Y ? _colorSelected : _colorY, gizmoScale, coneHeight, coneRadius);
                    DrawArrow(pass, position, forwardAxis, _hoverAxis == GizmoAxis.Z ? _colorSelected : _colorZ, gizmoScale, coneHeight, coneRadius);

                    // --- Planes ---
                    _effect.Alpha = alpha;
                    // XY Plane (Blue) - Up and Right
                    DrawPlane(pass, position, forwardAxis, rightAxis, upAxis, _hoverAxis == GizmoAxis.XY ? _colorSelected : _colorZ, planeSize);
                    
                    // XZ Plane (Green) - Right and Forward
                    DrawPlane(pass, position, upAxis, rightAxis, forwardAxis, _hoverAxis == GizmoAxis.XZ ? _colorSelected : _colorY, planeSize);
                    
                    // YZ Plane (Red) - Up and Forward
                    DrawPlane(pass, position, rightAxis, forwardAxis, upAxis, _hoverAxis == GizmoAxis.YZ ? _colorSelected : _colorX, planeSize);
                }
            }
            finally
            {
                _device.RasterizerState = originalRasterizerState;
                _device.BlendState = originalBlendState;
                _effect.Alpha = 1.0f;
            }
        }

        private void DrawArrow(EffectPass pass, Vector3 origin, Vector3 direction, Color color, float totalLength, float headLength, float headRadius)
        {
            _effect.DiffuseColor = color.ToVector3();
            
            Matrix rotation = RotateToDirection(Vector3.Up, direction);
            Matrix scale = Matrix.CreateScale(1, totalLength - headLength, 1);
            Matrix world = scale * rotation * Matrix.CreateTranslation(origin);
            
            _effect.World = world;
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.LineList, _lineVertices, 0, 1);

            Matrix coneScale = Matrix.CreateScale(headRadius / 0.25f, headLength, headRadius / 0.25f);
            Matrix coneOffset = Matrix.CreateTranslation(0, totalLength - headLength, 0);
            
            _effect.World = coneScale * coneOffset * rotation * Matrix.CreateTranslation(origin);
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _coneVertices, 0, _coneVertices.Length / 3);
        }

        private void DrawPlane(EffectPass pass, Vector3 origin, Vector3 normal, Vector3 axisU, Vector3 axisV, Color color, float size)
        {
            _effect.DiffuseColor = color.ToVector3();
            
            Matrix world = Matrix.Identity;
            world.Right = axisU * size;
            world.Up = axisV * size;
            world.Forward = normal;
            world.Translation = origin;
            
            _effect.World = world;
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _quadVertices, 0, 2);
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
        private Vector3 _objectStartPos;

        public void Update(Ray ray, bool isMouseDown, MonoGameEditor.Core.Transform targetTransform)
        {
            if (targetTransform == null) return;

            if (isMouseDown && !_isDragging)
            {
                if (_hoverAxis != GizmoAxis.None)
                {
                    _isDragging = true;
                    _dragAxis = _hoverAxis;
                    _objectStartPos = targetTransform.Position;
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
                Vector3 delta = currentPoint - _dragStartPoint;

                // Apply constraints
                if (_dragAxis == GizmoAxis.X) delta = new Vector3(delta.X, 0, 0);
                if (_dragAxis == GizmoAxis.Y) delta = new Vector3(0, delta.Y, 0);
                if (_dragAxis == GizmoAxis.Z) delta = new Vector3(0, 0, delta.Z);
                
                if (_dragAxis == GizmoAxis.XY) delta = new Vector3(delta.X, delta.Y, 0);
                if (_dragAxis == GizmoAxis.XZ) delta = new Vector3(delta.X, 0, delta.Z);
                if (_dragAxis == GizmoAxis.YZ) delta = new Vector3(0, delta.Y, delta.Z);

                targetTransform.Position = _objectStartPos + delta;
            }
            else
            {
                // Hit Test
                _hoverAxis = GizmoAxis.None;
                
                float scale = 2.0f;
                float thickness = 0.2f; 
                Vector3 pos = targetTransform.Position;

                // Axes Boxes
                BoundingBox boxX = new BoundingBox(pos + new Vector3(0, -thickness, -thickness), pos + new Vector3(scale, thickness, thickness));
                BoundingBox boxY = new BoundingBox(pos + new Vector3(-thickness, 0, -thickness), pos + new Vector3(thickness, scale, thickness));
                BoundingBox boxZ = new BoundingBox(pos + new Vector3(-thickness, -thickness, -scale), pos + new Vector3(thickness, thickness, 0)); 

                float? distX = ray.Intersects(boxX);
                float? distY = ray.Intersects(boxY);
                float? distZ = ray.Intersects(boxZ);
                
                // Planes (Small squares near origin)
                float pSize = 0.5f; 
                float pThick = 0.05f; 
                
                BoundingBox boxXY = new BoundingBox(pos + new Vector3(0, 0, -pThick), pos + new Vector3(pSize, pSize, pThick));
                BoundingBox boxXZ = new BoundingBox(pos + new Vector3(0, -pThick, -pSize), pos + new Vector3(pSize, pThick, 0));
                BoundingBox boxYZ = new BoundingBox(pos + new Vector3(-pThick, 0, -pSize), pos + new Vector3(pThick, pSize, 0));

                float? distXY = ray.Intersects(boxXY);
                float? distXZ = ray.Intersects(boxXZ);
                float? distYZ = ray.Intersects(boxYZ);

                // Find closest hit
                float closest = float.MaxValue;
                
                if (distX != null && distX < closest) { closest = distX.Value; _hoverAxis = GizmoAxis.X; }
                if (distY != null && distY < closest) { closest = distY.Value; _hoverAxis = GizmoAxis.Y; }
                if (distZ != null && distZ < closest) { closest = distZ.Value; _hoverAxis = GizmoAxis.Z; }
                
                if (distXY != null && distXY < closest) { closest = distXY.Value; _hoverAxis = GizmoAxis.XY; }
                if (distXZ != null && distXZ < closest) { closest = distXZ.Value; _hoverAxis = GizmoAxis.XZ; }
                if (distYZ != null && distYZ < closest) { closest = distYZ.Value; _hoverAxis = GizmoAxis.YZ; }
            }
        }

        private Vector3 GetDragPoint(Ray ray, Vector3 center, GizmoAxis axis)
        {
             Vector3 planeNormal = Vector3.Up;
             
             // Dynamic plane selection based on camera view (Ray direction)
             // We want the plane that is most perpendicular to the ray direction (largest dot product)
             
             if (axis == GizmoAxis.X) 
             {
                 // Axis is Right (1,0,0). Planes containing X are XY (Normal Z) and XZ (Normal Y).
                 float dotZ = Math.Abs(ray.Direction.Z);
                 float dotY = Math.Abs(ray.Direction.Y);
                 planeNormal = (dotY > dotZ) ? Vector3.Up : Vector3.Forward;
             }
             else if (axis == GizmoAxis.Y) 
             {
                 // Axis is Up (0,1,0). Planes containing Y are XY (Normal Z) and YZ (Normal X).
                 float dotZ = Math.Abs(ray.Direction.Z);
                 float dotX = Math.Abs(ray.Direction.X);
                 planeNormal = (dotZ > dotX) ? Vector3.Forward : Vector3.Right;
             }
             else if (axis == GizmoAxis.Z) 
             {
                 // Axis is Forward (0,0,1). Planes containing Z are XZ (Normal Y) and YZ (Normal X).
                 float dotY = Math.Abs(ray.Direction.Y);
                 float dotX = Math.Abs(ray.Direction.X);
                 planeNormal = (dotY > dotX) ? Vector3.Up : Vector3.Right;
             }
             
             // For planar handles, normals are fixed
             if (axis == GizmoAxis.XY) planeNormal = Vector3.Forward; // Normal Z
             if (axis == GizmoAxis.XZ) planeNormal = Vector3.Up;      // Normal Y
             if (axis == GizmoAxis.YZ) planeNormal = Vector3.Right;   // Normal X
             
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
