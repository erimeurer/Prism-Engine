using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assimp;
using Assimp.Configs;

namespace MonoGameEditor.Core.Assets
{
    public static class ModelImporter
    {
        public static Task<AssetMetadata> LoadMetadataAsync(string path)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(path)) return null;

                try
                {
                    using (var context = new AssimpContext())
                    {
                        // Configure for fast loading (we only need geometry for preview)
                        // PostProcessSteps.Triangulate is critical to ensure we have triangles
                        var scene = context.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.OptimizeMeshes);

                        if (scene == null || !scene.HasMeshes) return null;

                        var metadata = new AssetMetadata
                        {
                            Name = Path.GetFileName(path),
                            Extension = Path.GetExtension(path),
                            PreviewVertices = new List<System.Numerics.Vector3>(),
                            PreviewIndices = new List<int>()
                        };

                        int indexOffset = 0;
                        int totalVertices = 0;
                        int totalIndices = 0;

                        // Combine all meshes
                        foreach (var mesh in scene.Meshes)
                        {
                            totalVertices += mesh.VertexCount;
                            totalIndices += mesh.FaceCount * 3;

                            foreach (var v in mesh.Vertices)
                            {
                                // Convert Assimp Vector3D to System.Numerics.Vector3
                                metadata.PreviewVertices.Add(new System.Numerics.Vector3(v.X, v.Y, v.Z));
                            }

                            foreach (var face in mesh.Faces)
                            {
                                if (face.IndexCount == 3)
                                {
                                    metadata.PreviewIndices.Add(face.Indices[0] + indexOffset);
                                    metadata.PreviewIndices.Add(face.Indices[1] + indexOffset);
                                    metadata.PreviewIndices.Add(face.Indices[2] + indexOffset);
                                }
                            }
                            indexOffset += mesh.VertexCount;
                        }

                        // Calculate bounds
                        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

                        foreach (var v in metadata.PreviewVertices)
                        {
                            if (v.X < minX) minX = v.X;
                            if (v.Y < minY) minY = v.Y;
                            if (v.Z < minZ) minZ = v.Z;

                            if (v.X > maxX) maxX = v.X;
                            if (v.Y > maxY) maxY = v.Y;
                            if (v.Z > maxZ) maxZ = v.Z;
                        }

                        metadata.Min = new System.Numerics.Vector3(minX, minY, minZ);
                        metadata.Max = new System.Numerics.Vector3(maxX, maxY, maxZ);
                        metadata.VertexCount = totalVertices;
                        metadata.TriangleCount = totalIndices / 3;

                        return metadata;
                    }
                }
                catch (Exception ex)
                {
                    // Log error?
                    System.Diagnostics.Debug.WriteLine($"Error importing model {path}: {ex.Message}");
                    return null;
                }
            });
        }
    }
}
