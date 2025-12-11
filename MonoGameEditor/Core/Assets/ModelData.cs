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
        public int MaterialIndex { get; set; } = -1;
    }

    /// <summary>
    /// Represents a complete 3D model with hierarchical mesh structure
    /// </summary>
    public class ModelData
    {
        public string Name { get; set; }
        public List<MeshData> Meshes { get; set; } = new List<MeshData>();
        public int TotalVertexCount { get; set; }
        public int TotalTriangleCount { get; set; }
    }
}
