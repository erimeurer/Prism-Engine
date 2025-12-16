using System.Collections.Generic;

namespace MonoGameEditor.Core.Assets
{
    /// <summary>
    /// Represents a single mesh within a 3D model
    /// </summary>
    public class MeshData
    {
        public string Name { get; set; }
        public List<System.Numerics.Vector3> Vertices { get; set; } = new List<System.Numerics.Vector3>();
        public List<System.Numerics.Vector3> Normals { get; set; } = new List<System.Numerics.Vector3>();
        public List<int> Indices { get; set; } = new List<int>();
        public List<System.Numerics.Vector2> TexCoords { get; set; } = new List<System.Numerics.Vector2>();
        public int MaterialIndex { get; set; } = -1;
        
        // Skeletal animation skinning data (up to 4 bones per vertex)
        public List<System.Numerics.Vector4> BoneIndices { get; set; } = new List<System.Numerics.Vector4>();
        public List<System.Numerics.Vector4> BoneWeights { get; set; } = new List<System.Numerics.Vector4>();
        
        // Cached bounding box in local space (calculated once during import)
        public Microsoft.Xna.Framework.BoundingBox LocalBounds { get; set; }
    }
    
    /// <summary>
    /// Represents a bone in a skeletal hierarchy
    /// </summary>
    public class BoneData
    {
        public string Name { get; set; }
        public System.Numerics.Matrix4x4 OffsetMatrix { get; set; }
        public System.Numerics.Matrix4x4 LocalTransform { get; set; }
        public int ParentIndex { get; set; } = -1;
    }

    /// <summary>
    /// Represents a complete 3D model with hierarchical mesh structure and optional skeleton
    /// </summary>
    public class ModelData
    {
        public string Name { get; set; }
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public int TotalVertexCount { get; set; }
        public int TotalTriangleCount { get; set; }
        
        // Skeletal animation support
        public List<BoneData> Bones { get; set; } = new List<BoneData>();
        public Dictionary<string, int> BoneNameToIndex { get; set; } = new Dictionary<string, int>();
    }
}
