using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;
using Assimp.Configs;
using MonoGameEditor.Core.Assets;
using MonoGameEditor.Core;

namespace MonoGameEditor.Core.Assets
{
    public static class ModelImporter
    {
        private static Dictionary<string, ModelData> _cache = new Dictionary<string, ModelData>();

        public static void ClearCache()
        {
            lock (_cache)
            {
                _cache.Clear();
            }
        }

        public static async System.Threading.Tasks.Task<AssetMetadata> LoadMetadataAsync(string path)
        {
            var modelData = await LoadModelDataAsync(path);
            if (modelData == null) return new AssetMetadata();

            var metadata = new AssetMetadata
            {
                Name = modelData.Name,
                Extension = Path.GetExtension(path),
                VertexCount = modelData.TotalVertexCount,
                TriangleCount = modelData.TotalTriangleCount,
                MaterialCount = modelData.Meshes.Select(m => m.MaterialIndex).Distinct().Count(),
                HasNormals = modelData.Meshes.All(m => m.Normals.Count > 0),
                HasUVs = modelData.Meshes.All(m => m.TexCoords.Count > 0)
            };

            // Calculate bounding box for metadata
            System.Numerics.Vector3 min = new System.Numerics.Vector3(float.MaxValue);
            System.Numerics.Vector3 max = new System.Numerics.Vector3(float.MinValue);

            foreach (var mesh in modelData.Meshes)
            {
                foreach (var v in mesh.Vertices)
                {
                    min = System.Numerics.Vector3.Min(min, v);
                    max = System.Numerics.Vector3.Max(max, v);
                }
            }

            metadata.Min = min;
            metadata.Max = max;

            return metadata;
        }

        public static async System.Threading.Tasks.Task<ModelData> LoadModelDataAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return null!;

            lock (_cache)
            {
                if (_cache.TryGetValue(path, out var data)) return data;
            }

            return await System.Threading.Tasks.Task.Run(() =>
            {
                string fullPath = path;
                if (!Path.IsPathRooted(path))
                {
                    string[] possibleRoots = new[] { 
                        AppDomain.CurrentDomain.BaseDirectory,
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content"),
                        "D:\\git\\Prism-Engine\\MonoGameEditor\\Content"
                    };

                    foreach (var root in possibleRoots)
                    {
                        string testPath = Path.Combine(root, path);
                        if (File.Exists(testPath))
                        {
                            fullPath = testPath;
                            break;
                        }
                    }
                }
                
                if (!File.Exists(fullPath)) return null!;

                try
                {
                    using (var context = new AssimpContext())
                    {
                        context.SetConfig(new FBXPreservePivotsConfig(false));
                        
                        var importFlags = PostProcessSteps.Triangulate 
                            | PostProcessSteps.GenerateNormals
                            | PostProcessSteps.LimitBoneWeights
                            | PostProcessSteps.JoinIdenticalVertices;
                        
                        var scene = context.ImportFile(fullPath, importFlags);

                        if (scene == null || !scene.HasMeshes) return null!;

                        var modelData = new ModelData
                        {
                            Name = Path.GetFileNameWithoutExtension(fullPath)
                        };

                        // 1. Extract bone hierarchy
                        ExtractBoneHierarchy(scene, modelData);

                        // 2. Extract meshes
                        foreach (var assimpMesh in scene.Meshes)
                        {
                            var meshData = new MeshData
                            {
                                Name = string.IsNullOrEmpty(assimpMesh.Name) ? "Mesh" : assimpMesh.Name,
                                MaterialIndex = assimpMesh.MaterialIndex
                            };

                            foreach (var v in assimpMesh.Vertices)
                                meshData.Vertices.Add(new System.Numerics.Vector3(v.X, v.Y, v.Z));

                            if (assimpMesh.HasNormals)
                                foreach (var n in assimpMesh.Normals)
                                    meshData.Normals.Add(new System.Numerics.Vector3(n.X, n.Y, n.Z));

                            if (assimpMesh.HasTextureCoords(0))
                                foreach (var uv in assimpMesh.TextureCoordinateChannels[0])
                                    meshData.TexCoords.Add(new System.Numerics.Vector2(uv.X, 1.0f - uv.Y));

                            foreach (var face in assimpMesh.Faces)
                                if (face.IndexCount == 3)
                                {
                                    meshData.Indices.Add(face.Indices[0]);
                                    meshData.Indices.Add(face.Indices[1]);
                                    meshData.Indices.Add(face.Indices[2]);
                                }

                            if (assimpMesh.HasBones)
                                ExtractBoneWeights(assimpMesh, meshData, modelData);

                            modelData.Meshes.Add(meshData);
                        }

                        // 3. Update stats
                        modelData.TotalVertexCount = modelData.Meshes.Sum(m => m.Vertices.Count);
                        modelData.TotalTriangleCount = modelData.Meshes.Sum(m => m.Indices.Count / 3);

                        // 4. Extract animations
                        if (scene.HasAnimations)
                        {
                            modelData.Animations = new AnimationCollection();
                            foreach (var anim in scene.Animations)
                            {
                                modelData.Animations.Animations.Add(ExtractAnimationClip(anim));
                            }
                            
                            // Build lookup
                            for (int i = 0; i < modelData.Animations.Animations.Count; i++)
                                modelData.Animations.AnimationNameToIndex[modelData.Animations.Animations[i].Name] = i;
                        }

                        lock (_cache)
                        {
                            if (!_cache.ContainsKey(path)) _cache.Add(path, modelData);
                        }
                        
                        return modelData;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[ModelImporter] Error: {ex.Message}");
                    return null!;
                }
            });
        }

        private static void ExtractBoneHierarchy(Scene scene, ModelData modelData)
        {
            var boneNames = new HashSet<string>();
            foreach (var mesh in scene.Meshes)
                foreach (var bone in mesh.Bones)
                    boneNames.Add(bone.Name);

            if (boneNames.Count == 0) return;

            TraverseNodeHierarchy(scene.RootNode, -1, modelData, boneNames, System.Numerics.Matrix4x4.Identity);
        }

        private static void TraverseNodeHierarchy(Node node, int parentIndex, ModelData modelData, HashSet<string> boneNames, System.Numerics.Matrix4x4 accumulated)
        {
            var local = ConvertMatrix(node.Transform);
            var world = local * accumulated;
            
            int currentIndex = parentIndex;
            System.Numerics.Matrix4x4 nextAccumulated = world;

            if (boneNames.Contains(node.Name))
            {
                currentIndex = modelData.Bones.Count;
                modelData.BoneNameToIndex[node.Name] = currentIndex;
                
                modelData.Bones.Add(new BoneData
                {
                    Name = node.Name,
                    ParentIndex = parentIndex,
                    LocalTransform = local,
                    OffsetMatrix = System.Numerics.Matrix4x4.Identity
                });

                nextAccumulated = System.Numerics.Matrix4x4.Identity;
            }

            foreach (var child in node.Children)
                TraverseNodeHierarchy(child, currentIndex, modelData, boneNames, nextAccumulated);
        }

        private static void ExtractBoneWeights(Mesh mesh, MeshData meshData, ModelData modelData)
        {
            var indices = new System.Numerics.Vector4[mesh.VertexCount];
            var weights = new System.Numerics.Vector4[mesh.VertexCount];
            var filled = new int[mesh.VertexCount];

            foreach (var bone in mesh.Bones)
            {
                if (modelData.BoneNameToIndex.TryGetValue(bone.Name, out int index))
                {
                    // Update Offset Matrix from Assimp
                    modelData.Bones[index].OffsetMatrix = ConvertMatrix(bone.OffsetMatrix);

                    foreach (var weight in bone.VertexWeights)
                    {
                        int vId = weight.VertexID;
                        if (vId < mesh.VertexCount && filled[vId] < 4)
                        {
                            int slot = filled[vId];
                            if (slot == 0) { indices[vId].X = index; weights[vId].X = weight.Weight; }
                            else if (slot == 1) { indices[vId].Y = index; weights[vId].Y = weight.Weight; }
                            else if (slot == 2) { indices[vId].Z = index; weights[vId].Z = weight.Weight; }
                            else if (slot == 3) { indices[vId].W = index; weights[vId].W = weight.Weight; }
                            filled[vId]++;
                        }
                    }
                }
            }

            meshData.BoneIndices.AddRange(indices);
            meshData.BoneWeights.AddRange(weights);
        }

        private static AnimationClip ExtractAnimationClip(Assimp.Animation anim)
        {
            var clip = new AnimationClip 
            { 
                Name = anim.Name, 
                Duration = (float)(anim.DurationInTicks / anim.TicksPerSecond),
                TicksPerSecond = (float)anim.TicksPerSecond
            };

            foreach (var channel in anim.NodeAnimationChannels)
            {
                var chan = new AnimationChannel { BoneName = channel.NodeName };
                var timeMap = new SortedDictionary<double, AnimationKeyframe>();

                foreach (var k in channel.PositionKeys) GetOrCreateKey(timeMap, k.Time).Position = new System.Numerics.Vector3(k.Value.X, k.Value.Y, k.Value.Z);
                foreach (var k in channel.RotationKeys) GetOrCreateKey(timeMap, k.Time).Rotation = new System.Numerics.Quaternion(k.Value.X, k.Value.Y, k.Value.Z, k.Value.W);
                foreach (var k in channel.ScalingKeys) GetOrCreateKey(timeMap, k.Time).Scale = new System.Numerics.Vector3(k.Value.X, k.Value.Y, k.Value.Z);

                foreach (var pair in timeMap)
                {
                    pair.Value.Time = (float)(pair.Key / anim.TicksPerSecond);
                    chan.Keyframes.Add(pair.Value);
                }
                
                clip.Channels.Add(chan);
            }

            return clip;
        }

        private static AnimationKeyframe GetOrCreateKey(SortedDictionary<double, AnimationKeyframe> map, double time)
        {
            if (!map.TryGetValue(time, out var key))
            {
                key = new AnimationKeyframe { Position = System.Numerics.Vector3.Zero, Rotation = System.Numerics.Quaternion.Identity, Scale = System.Numerics.Vector3.One };
                map[time] = key;
            }
            return key;
        }

        private static System.Numerics.Matrix4x4 ConvertMatrix(Assimp.Matrix4x4 m)
        {
            return new System.Numerics.Matrix4x4(
                m.A1, m.B1, m.C1, m.D1,
                m.A2, m.B2, m.C2, m.D2,
                m.A3, m.B3, m.C3, m.D3,
                m.A4, m.B4, m.C4, m.D4);
        }
    }
}
