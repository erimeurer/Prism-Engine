using System.Numerics;
using System.Collections.Generic;

namespace MonoGameEditor.Core.Assets
{
    public class AssetMetadata
    {
        public string Name { get; set; }
        public string Extension { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public int MaterialCount { get; set; }
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }
        public bool HasNormals { get; set; }
        public bool HasUVs { get; set; }
        
        public string Dimensions => $"{Max.X - Min.X:F2} x {Max.Y - Min.Y:F2} x {Max.Z - Min.Z:F2}";

        public List<Vector3> PreviewVertices { get; set; } = new List<Vector3>();
        public List<int> PreviewIndices { get; set; } = new List<int>();

        public AssetMetadata()
        {
            Min = new Vector3(float.MaxValue);
            Max = new Vector3(float.MinValue);
        }
    }
}
