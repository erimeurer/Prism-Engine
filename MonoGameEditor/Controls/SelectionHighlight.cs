using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core;
using MonoGameEditor.Core.Components;

namespace MonoGameEditor.Controls
{
    /// <summary>
    /// Renders selection highlight using wireframe bounding box (no shader required)
    /// </summary>
    public class SelectionHighlight
    {
        private GraphicsDevice? _graphicsDevice;
        private BasicEffect? _effect;
        
        // Highlight settings
        public Color HighlightColor { get; set; } = new Color(255, 153, 0); // Unity orange
        
        // Bounding box vertices  
        private VertexPositionColor[] _boxVertices;
        private short[] _boxIndices;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            // Create BasicEffect for wireframe
            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            // Define box indices for lines (12 edges of a cube)
            _boxIndices = new short[]
            {
                // Bottom face
                0, 1,  1, 2,  2, 3,  3, 0,
                // Top face  
                4, 5,  5, 6,  6, 7,  7, 4,
                // Vertical edges
                0, 4,  1, 5,  2, 6,  3, 7
            };
            
            _boxVertices = new VertexPositionColor[8];
        }

        public void RenderHighlight(GameObject selectedObject, Matrix view, Matrix projection)
        {
            if (_effect == null || _graphicsDevice == null || selectedObject == null)
                return;

            // Get bounding box from all renderers in hierarchy
            BoundingBox? bounds = GetHierarchyBounds(selectedObject);
            if (!bounds.HasValue)
                return;

            // Create box vertices from bounds
            var min = bounds.Value.Min;
            var max = bounds.Value.Max;
            
            _boxVertices[0] = new VertexPositionColor(new Vector3(min.X, min.Y, min.Z), HighlightColor);
            _boxVertices[1] = new VertexPositionColor(new Vector3(max.X, min.Y, min.Z), HighlightColor);
            _boxVertices[2] = new VertexPositionColor(new Vector3(max.X, min.Y, max.Z), HighlightColor);
            _boxVertices[3] = new VertexPositionColor(new Vector3(min.X, min.Y, max.Z), HighlightColor);
            _boxVertices[4] = new VertexPositionColor(new Vector3(min.X, max.Y, min.Z), HighlightColor);
            _boxVertices[5] = new VertexPositionColor(new Vector3(max.X, max.Y, min.Z), HighlightColor);
            _boxVertices[6] = new VertexPositionColor(new Vector3(max.X, max.Y, max.Z), HighlightColor);
            _boxVertices[7] = new VertexPositionColor(new Vector3(min.X, max.Y, max.Z), HighlightColor);

            // Set effect parameters
            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = projection;

            // Draw wireframe box
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            var rasterizerState = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid
            };
            _graphicsDevice.RasterizerState = rasterizerState;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.LineList,
                    _boxVertices,
                    0,
                    8,
                    _boxIndices,
                    0,
                    12 // 12 lines
                );
            }

            _graphicsDevice.RasterizerState = oldRasterizerState;
            rasterizerState.Dispose();
        }

        private BoundingBox? GetHierarchyBounds(GameObject obj)
        {
            BoundingBox? bounds = null;
            CollectBounds(obj, ref bounds);
            return bounds;
        }

        private void CollectBounds(GameObject obj, ref BoundingBox? bounds)
        {
            // Get renderer's mesh bounds if it exists
            var renderer = obj.GetComponent<ModelRendererComponent>();
            if (renderer != null)
            {
                var meshBounds = GetRendererBounds(renderer, obj.Transform.WorldMatrix);
                if (meshBounds.HasValue)
                {
                    if (bounds.HasValue)
                        bounds = BoundingBox.CreateMerged(bounds.Value, meshBounds.Value);
                    else
                        bounds = meshBounds;
                }
            }

            // Recursively check children
            foreach (var child in obj.Children)
            {
                CollectBounds(child, ref bounds);
            }
        }

        private BoundingBox? GetRendererBounds(ModelRendererComponent renderer, Matrix worldMatrix)
        {
            // Simple approximation - use fixed size box around transform position
            // In a real engine, you'd get actual mesh bounds
            var center = worldMatrix.Translation;
            var size = 1f; // Default size
            
            return new BoundingBox(
                center - new Vector3(size),
                center + new Vector3(size)
            );
        }
    }
}
