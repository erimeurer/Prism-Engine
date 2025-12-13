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

        /// <summary>
        /// Loads a 3D model with hierarchical mesh structure (separate meshes)
        /// </summary>
        public static Task<ModelData> LoadModelDataAsync(string path)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(path)) return null;

                try
                {
                    using (var context = new AssimpContext())
                    {
                        var scene = context.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals);

                        if (scene == null || !scene.HasMeshes) return null;

                        var modelData = new ModelData
                        {
                            Name = Path.GetFileNameWithoutExtension(path)
                        };

                        int totalVertices = 0;
                        int totalTriangles = 0;

                        // Extract each mesh separately
                        for (int i = 0; i < scene.MeshCount; i++)
                        {
                            var assimpMesh = scene.Meshes[i];
                            
                            var meshData = new MeshData
                            {
                                Name = string.IsNullOrEmpty(assimpMesh.Name) ? $"Mesh_{i}" : assimpMesh.Name,
                                MaterialIndex = assimpMesh.MaterialIndex
                            };

                            // Copy vertices
                            foreach (var v in assimpMesh.Vertices)
                            {
                                meshData.Vertices.Add(new System.Numerics.Vector3(v.X, v.Y, v.Z));
                            }

                            // Copy normals
                            if (assimpMesh.HasNormals)
                            {
                                foreach (var n in assimpMesh.Normals)
                                {
                                    meshData.Normals.Add(new System.Numerics.Vector3(n.X, n.Y, n.Z));
                                }
                            }
                            else
                            {
                                // If no normals, fill with default (up)
                                for (int j = 0; j < assimpMesh.VertexCount; j++)
                                {
                                    meshData.Normals.Add(new System.Numerics.Vector3(0, 1, 0));
                                }
                            } // Close else block

                            // Copy Texture Coordinates
                            if (assimpMesh.HasTextureCoords(0))
                            {
                                foreach (var uv in assimpMesh.TextureCoordinateChannels[0])
                                {
                                    // Convert Assimp Vector3D to System.Numerics.Vector2
                                    // Note: We might need to flip Y (1 - uv.Y) depending on model source, keeping raw for now
                                    meshData.TexCoords.Add(new System.Numerics.Vector2(uv.X, 1.0f - uv.Y)); // Often required for OpenGL/MonoGame
                                }
                            }
                            else
                            {
                                // If no UVs, fill with Zero
                                for (int j = 0; j < assimpMesh.VertexCount; j++)
                                {
                                    meshData.TexCoords.Add(System.Numerics.Vector2.Zero);
                                }
                            }

                            // Copy indices
                            foreach (var face in assimpMesh.Faces)
                            {
                                if (face.IndexCount == 3)
                                {
                                    meshData.Indices.Add(face.Indices[0]);
                                    meshData.Indices.Add(face.Indices[1]);
                                    meshData.Indices.Add(face.Indices[2]);
                                }
                            }

                            totalVertices += meshData.Vertices.Count;
                            totalTriangles += meshData.Indices.Count / 3;

                            modelData.Meshes.Add(meshData);
                        }

                        modelData.TotalVertexCount = totalVertices;
                        modelData.TotalTriangleCount = totalTriangles;

                        return modelData;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error importing model {path}: {ex.Message}");
                    return null;
                }
            });
        }
    }
}
