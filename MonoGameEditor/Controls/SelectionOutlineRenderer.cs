using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Simple selection indicator - wireframe bounding box
    /// </summary>
    public class SelectionOutlineRenderer
    {
        private GraphicsDevice? _graphicsDevice;
        private BasicEffect? _lineEffect;
        
        public Color BoxColor { get; set; } = new Color(255, 153, 0); // Orange

        private VertexPositionColor[] _boxVertices = new VertexPositionColor[8];
        private short[] _boxIndices = new short[]
        {
            0, 1,  1, 2,  2, 3,  3, 0,  // Bottom
            4, 5,  5, 6,  6, 7,  7, 4,  // Top
            0, 4,  1, 5,  2, 6,  3, 7   // Sides
        };

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            _lineEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void RenderOutline(GameObject selectedObject, Matrix view, Matrix projection)
        {
            if (_lineEffect == null || _graphicsDevice == null || selectedObject == null)
                return;

            // Get object bounds
            var bounds = CalculateBounds(selectedObject);
            
            // Add small padding
            float padding = 0.05f;
            var min = bounds.Min - new Vector3(padding);
            var max = bounds.Max + new Vector3(padding);
            
            // Create box vertices
            _boxVertices[0] = new VertexPositionColor(new Vector3(min.X, min.Y, min.Z), BoxColor);
            _boxVertices[1] = new VertexPositionColor(new Vector3(max.X, min.Y, min.Z), BoxColor);
            _boxVertices[2] = new VertexPositionColor(new Vector3(max.X, min.Y, max.Z), BoxColor);
            _boxVertices[3] = new VertexPositionColor(new Vector3(min.X, min.Y, max.Z), BoxColor);
            _boxVertices[4] = new VertexPositionColor(new Vector3(min.X, max.Y, min.Z), BoxColor);
            _boxVertices[5] = new VertexPositionColor(new Vector3(max.X, max.Y, min.Z), BoxColor);
            _boxVertices[6] = new VertexPositionColor(new Vector3(max.X, max.Y, max.Z), BoxColor);
            _boxVertices[7] = new VertexPositionColor(new Vector3(min.X, max.Y, max.Z), BoxColor);

            // Render
            _lineEffect.World = Matrix.Identity;
            _lineEffect.View = view;
            _lineEffect.Projection = projection;

            var oldDepth = _graphicsDevice.DepthStencilState;
            _graphicsDevice.DepthStencilState = DepthStencilState.None; // Always on top

            foreach (var pass in _lineEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _boxVertices, 0, 8,
                    _boxIndices, 0, 12);
            }

            _graphicsDevice.DepthStencilState = oldDepth;
        }

        private BoundingBox CalculateBounds(GameObject obj)
        {
            BoundingBox? result = null;
            CollectBounds(obj, ref result);
            
            // If no bounds found, use transform as fallback
            if (!result.HasValue)
            {
                var pos = obj.Transform.Position;
                result = new BoundingBox(pos - Vector3.One * 0.5f, pos + Vector3.One * 0.5f);
            }
            
            return result.Value;
        }

        private void CollectBounds(GameObject obj, ref BoundingBox? totalBounds)
        {
            // Get renderer and use cached mesh bounds
            var renderer = obj.GetComponent<Core.Components.ModelRendererComponent>();
            if (renderer != null)
            {
                var meshData = renderer.GetMeshData();
                if (meshData != null && meshData.LocalBounds != default(BoundingBox))
                {
                    // Transform the local bounds to world space
                    var world = obj.Transform.WorldMatrix;
                    var localBounds = meshData.LocalBounds;
                    var corners = localBounds.GetCorners();
                    
                    // Transform all corners to world space
                    for (int i = 0; i < corners.Length; i++)
                    {
                        corners[i] = Vector3.Transform(corners[i], world);
                    }
                    
                    var worldBounds = BoundingBox.CreateFromPoints(corners);
                    
                    if (totalBounds.HasValue)
                        totalBounds = BoundingBox.CreateMerged(totalBounds.Value, worldBounds);
                    else
                        totalBounds = worldBounds;
                }
                else
                {
                    // Fallback: use transform-based approximation if no cached bounds
                    var world = obj.Transform.WorldMatrix;
                    var scale = obj.Transform.Scale;
                    
                    float size = Math.Max(Math.Max(scale.X, scale.Y), scale.Z);
                    var localBounds = new BoundingBox(-Vector3.One * size, Vector3.One * size);
                    var corners = localBounds.GetCorners();
                    
                    for (int i = 0; i < corners.Length; i++)
                    {
                        corners[i] = Vector3.Transform(corners[i], world);
                    }
                    
                    var worldBounds = BoundingBox.CreateFromPoints(corners);
                    
                    if (totalBounds.HasValue)
                        totalBounds = BoundingBox.CreateMerged(totalBounds.Value, worldBounds);
                    else
                        totalBounds = worldBounds;
                }
            }
            
            // Recursively check children
            foreach (var child in obj.Children)
            {
                CollectBounds(child, ref totalBounds);
            }
        }
    }
}
