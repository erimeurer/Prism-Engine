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
        // PERFORMANCE: Cache processed models for instant loading (like Unity!)
        private static readonly Dictionary<string, ModelData> _modelCache = new Dictionary<string, ModelData>();
        
        /// <summary>
        /// Clear model cache (call when assets are modified)
        /// </summary>
        public static void ClearCache() => _modelCache.Clear();
        
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
                
                // CHECK CACHE FIRST - instant return if already processed!
                lock (_modelCache)
                {
                    if (_modelCache.TryGetValue(path, out ModelData cachedModel))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ModelImporter] ✓ Cache HIT: {Path.GetFileName(path)} - instant!");
                        return cachedModel;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[ModelImporter] Cache MISS: {Path.GetFileName(path)} - loading...");

                try
                {
                    using (var context = new AssimpContext())
                    {
                        // CRITICAL for Mixamo FBX: Disable pivot preservation
                        // Mixamo FBX files have pivot transforms that cause issues
                        // This config resolves many Mixamo animation/transformation problems
                        context.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(false));
                        
                        // Post-process flags optimized for skeletal animation (Mixamo/FBX)
                        // - Triangulate: Convert all faces to triangles
                        // - GenerateNormals: Generate smooth normals if missing
                        // - LimitBoneWeights: Limit to max 4 bones per vertex (shader requirement)
                        // - JoinIdenticalVertices: Optimize by joining duplicate vertices
                        var importFlags = PostProcessSteps.Triangulate 
                            | PostProcessSteps.GenerateNormals
                            | PostProcessSteps.LimitBoneWeights
                            | PostProcessSteps.JoinIdenticalVertices;
                        
                        var scene = context.ImportFile(path, importFlags);

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

                            // Extract bone weights for skinning (if mesh has bones)
                            if (assimpMesh.HasBones)
                            {
                                ExtractBoneWeights(assimpMesh, meshData, scene);
                            }
                            else
                            {
                                // No bones - fill with defaults
                                for (int j = 0; j < assimpMesh.VertexCount; j++)
                                {
                                    meshData.BoneIndices.Add(System.Numerics.Vector4.Zero);
                                    meshData.BoneWeights.Add(new System.Numerics.Vector4(1, 0, 0, 0));
                                }
                            }

                            totalVertices += meshData.Vertices.Count;
                            totalTriangles += meshData.Indices.Count / 3;

                            modelData.Meshes.Add(meshData);
                        }

                        modelData.TotalVertexCount = totalVertices;
                        modelData.TotalTriangleCount = totalTriangles;
                        
                        // Extract bone hierarchy if present
                        ExtractBoneHierarchy(scene, modelData);
                        
                        // CACHE IT - next time will be instant!
                        lock (_modelCache)
                        {
                            _modelCache[path] = modelData;
                        }

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
        
        /// <summary>
        /// Extracts bone weights for vertex skinning
        /// </summary>
        private static void ExtractBoneWeights(Mesh assimpMesh, MeshData meshData, Scene scene)
        {
            // First, build a bone name to index mapping
            var boneNameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < assimpMesh.BoneCount; i++)
            {
                boneNameToIndex[assimpMesh.Bones[i].Name] = i;
            }
            
            // Initialize arrays for each vertex (up to 4 bones per vertex)
            var vertexBoneIndices = new List<List<int>>();
            var vertexBoneWeights = new List<List<float>>();
            
            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                vertexBoneIndices.Add(new List<int>());
                vertexBoneWeights.Add(new List<float>());
            }
            
            // Process each bone in the mesh
            foreach (var bone in assimpMesh.Bones)
            {
                // Get bone index from our mapping
                int boneIndex = boneNameToIndex[bone.Name];
                
                // Process each vertex weight for this bone
                foreach (var weight in bone.VertexWeights)
                {
                    int vertexId = weight.VertexID;
                    if (vertexId >= 0 && vertexId < assimpMesh.VertexCount)
                    {
                        vertexBoneIndices[vertexId].Add(boneIndex);
                        vertexBoneWeights[vertexId].Add(weight.Weight);
                    }
                }
            }
            
            // Convert to Vector4 format (max 4 bones per vertex)
            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                var indices = vertexBoneIndices[i];
                var weights = vertexBoneWeights[i];
                
                // Sort by weight (descending) and take top 4
                if (indices.Count > 4)
                {
                    var combined = indices.Zip(weights, (idx, wgt) => new { Index = idx, Weight = wgt })
                                         .OrderByDescending(x => x.Weight)
                                         .Take(4)
                                         .ToList();
                    indices = combined.Select(x => x.Index).ToList();
                    weights = combined.Select(x => x.Weight).ToList();
                }
                
                // Normalize weights to sum to 1.0
                float totalWeight = weights.Sum();
                if (totalWeight > 0.001f)
                {
                    for (int j = 0; j < weights.Count; j++)
                    {
                        weights[j] /= totalWeight;
                    }
                }
                
                // Ensure we have exactly 4 entries (pad with zeros if needed)
                while (indices.Count < 4)
                {
                    indices.Add(0);
                    weights.Add(0f);
                }
                
                // Create Vector4s
                var boneIdx = new System.Numerics.Vector4(
                    indices.Count > 0 ? indices[0] : 0,
                    indices.Count > 1 ? indices[1] : 0,
                    indices.Count > 2 ? indices[2] : 0,
                    indices.Count > 3 ? indices[3] : 0
                );
                
                var boneWgt = new System.Numerics.Vector4(
                    weights.Count > 0 ? weights[0] : 0,
                    weights.Count > 1 ? weights[1] : 0,
                    weights.Count > 2 ? weights[2] : 0,
                    weights.Count > 3 ? weights[3] : 0
                );
                
                meshData.BoneIndices.Add(boneIdx);
                meshData.BoneWeights.Add(boneWgt);
            }
            
            System.Diagnostics.Debug.WriteLine($"[ModelImporter] Extracted bone weights for {assimpMesh.VertexCount} vertices ({assimpMesh.BoneCount} bones)");
        }
        
        /// <summary>
        /// Extracts bone hierarchy from Assimp scene
        /// </summary>
        private static void ExtractBoneHierarchy(Scene scene, ModelData modelData)
        {
            if (scene.RootNode == null) return;
            
            // First, collect all bones mentioned in meshes
            var boneNames = new HashSet<string>();
            foreach (var mesh in scene.Meshes)
            {
                if (mesh.HasBones)
                {
                    foreach (var bone in mesh.Bones)
                    {
                        boneNames.Add(bone.Name);
                    }
                }
            }
            
            // If no bones found, return
            if (boneNames.Count == 0) return;
            
            // CRITICAL FIX: Build bones in mesh.Bones[] order, NOT hierarchy order!
            // Vertex bone indices reference mesh.Bones[], so we MUST match that order.
            
            // Step 1: Build ordered list from mesh.Bones[] (this is what vertex indices reference)
            var boneOrderedList = new List<string>();
            var boneOffsets = new Dictionary<string, System.Numerics.Matrix4x4>();
            
            foreach (var mesh in scene.Meshes)
            {
                if (mesh.HasBones)
                {
                    foreach (var bone in mesh.Bones)
                    {
                        if (!boneOrderedList.Contains(bone.Name))
                        {
                            boneOrderedList.Add(bone.Name);  // Preserve mesh.Bones[] order!
                            boneOffsets[bone.Name] = ConvertMatrix(bone.OffsetMatrix);
                        }
                    }
                }
            }
            
            // Step 2: Build node map for fast lookup
            var nodeMap = new Dictionary<string, Node>();
            BuildNodeMap(scene.RootNode, nodeMap);
            
            // Step 3: Create bones in mesh.Bones[] order
            for (int i = 0; i < boneOrderedList.Count; i++)
            {
                string boneName = boneOrderedList[i];
                
                if (nodeMap.TryGetValue(boneName, out Node node))
                {
                    // Find parent index (must search in boneOrderedList, not hierarchy)
                    int parentIndex = -1;
                    if (node.Parent != null)
                    {
                        parentIndex = boneOrderedList.IndexOf(node.Parent.Name);
                        // If parent not in list, keep looking up the hierarchy
                        Node ancestor = node.Parent;
                        while (parentIndex < 0 && ancestor.Parent != null)
                        {
                            ancestor = ancestor.Parent;
                            parentIndex = boneOrderedList.IndexOf(ancestor.Name);
                        }
                    }
                    
                    var boneData = new BoneData
                    {
                        Name = boneName,
                        OffsetMatrix = boneOffsets[boneName],
                        LocalTransform = ConvertMatrix(node.Transform),
                        ParentIndex = parentIndex
                    };
                    
                    modelData.Bones.Add(boneData);
                    modelData.BoneNameToIndex[boneName] = i;
                    
                    System.Diagnostics.Debug.WriteLine($"[ModelImporter] Bone[{i}] '{boneName}' | ParentIdx: {parentIndex}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ModelImporter] Created {modelData.Bones.Count} bones in mesh.Bones[] order");
        }
        
        /// <summary>
        /// Recursively build map of node names to nodes
        /// </summary>
        private static void BuildNodeMap(Node node, Dictionary<string, Node> map)
        {
            map[node.Name] = node;
            foreach (var child in node.Children)
            {
                BuildNodeMap(child, map);
            }
        }
        
        /// <summary>
        /// Recursively traverses node hierarchy to build bone list
        /// </summary>
        private static void TraverseNodeHierarchy(Node node, Node parent, int parentIndex, 
            HashSet<string> boneNames, ModelData modelData, Scene scene)
        {
            int currentIndex = -1;
            
            // Check if this node is a bone
            if (boneNames.Contains(node.Name))
            {
                currentIndex = modelData.Bones.Count;
                
                // Get offset matrix from mesh bones
                var offsetMatrix = System.Numerics.Matrix4x4.Identity;
                foreach (var mesh in scene.Meshes)
                {
                    if (mesh.HasBones)
                    {
                        foreach (var bone in mesh.Bones)
                        {
                            if (bone.Name == node.Name)
                            {
                                // Convert Assimp Matrix4x4 to System.Numerics.Matrix4x4
                                offsetMatrix = ConvertMatrix(bone.OffsetMatrix);
                                break;
                            }
                        }
                    }
                }
                
                var boneData = new BoneData
                {
                    Name = node.Name,
                    OffsetMatrix = offsetMatrix,
                    LocalTransform = ConvertMatrix(node.Transform),
                    ParentIndex = parentIndex
                };
                
                modelData.Bones.Add(boneData);
                modelData.BoneNameToIndex[node.Name] = currentIndex;
                
                var t = node.Transform;
                System.Diagnostics.Debug.WriteLine($"[ModelImporter] Found bone: {node.Name} | ParentIdx: {parentIndex} | LocalPos: {t.A4},{t.B4},{t.C4}");
            }
            
            // Recursively process children
            // CRITICAL FIX: If current node is NOT a bone (currentIndex == -1), 
            // pass through the parentIndex we received instead of -1.
            // This ensures bone hierarchy is preserved even when there are non-bone
            // intermediate nodes (common in Mixamo FBX files).
            int parentForChildren = (currentIndex >= 0) ? currentIndex : parentIndex;
            
            foreach (var child in node.Children)
            {
                TraverseNodeHierarchy(child, node, parentForChildren, boneNames, modelData, scene);
            }
        }
        
        /// <summary>
        /// Converts Assimp Matrix4x4 to System.Numerics.Matrix4x4
        /// IMPORTANT: Assimp uses ROW-major matrices, System.Numerics uses COLUMN-major
        /// We need to TRANSPOSE the matrix (swap rows and columns)
        /// </summary>
        private static System.Numerics.Matrix4x4 ConvertMatrix(Assimp.Matrix4x4 m)
        {
            // Transpose: Assimp rows become System.Numerics columns
            // This ensures translation components (A4, B4, C4) end up in M41, M42, M43
            return new System.Numerics.Matrix4x4(
                m.A1, m.B1, m.C1, m.D1,  // Column 1 (was Row 1)
                m.A2, m.B2, m.C2, m.D2,  // Column 2 (was Row 2)
                m.A3, m.B3, m.C3, m.D3,  // Column 3 (was Row 3)
                m.A4, m.B4, m.C4, m.D4   // Column 4 (was Row 4) - TRANSLATION
            );
        }
    }
}
