#nullable disable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.ViewModels;
using MonoGameEditor.Core.Graphics;
using MonoGameEditor.Core.Materials;
using MonoGameEditor.Controls;

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Renders skinned meshes with skeletal animation support
    /// </summary>
    public class SkinnedModelRendererComponent : ModelRendererComponent
    {
        public override string ComponentName => "Skinned Model Renderer";

        // Bone GameObjects (must match mesh bone order)
        public List<GameObject> Bones { get; set; } = new List<GameObject>();
        
        // Persisted bone IDs for reconnection after scene load
        public List<Guid> BoneIds { get; set; } = new List<Guid>();

        // MAX_BONES in shader = 128
        private readonly Matrix[] _boneMatrices = new Matrix[128];
        private Matrix[] _offsetMatrices;
        private bool _bonesInitialized = false;

        public SkinnedModelRendererComponent()
        {
            // ConsoleViewModel.Log("[SkinnedRenderer] Component created");

            // Identity prevents invisible mesh if bones are not ready
            for (int i = 0; i < _boneMatrices.Length; i++)
                _boneMatrices[i] = Matrix.Identity;
        }

        /// <summary>
        /// Sets bone GameObjects and their offset matrices (Inverse Bind Pose)
        /// </summary>
        public void SetBones(List<GameObject> bones, List<System.Numerics.Matrix4x4> offsetMatrices)
        {
            // Unsubscribe old bones
            if (_bonesInitialized && Bones != null)
            {
                foreach (var bone in Bones)
                {
                    if (bone?.Transform != null)
                        bone.Transform.PropertyChanged -= OnBoneTransformChanged;
                }
            }

            Bones = bones;
            _offsetMatrices = new Matrix[bones.Count];
            
            // Capture bone IDs for persistent reference (scene save/load)
            BoneIds.Clear();
            foreach (var bone in bones)
            {
                BoneIds.Add(bone?.Id ?? Guid.Empty);
            }

            // Convert offset matrices (Numerics → XNA)
            for (int i = 0; i < offsetMatrices.Count; i++)
            {
                var m = offsetMatrices[i];
                _offsetMatrices[i] = new Matrix(
                    m.M11, m.M12, m.M13, m.M14,
                    m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34,
                    m.M41, m.M42, m.M43, m.M44
                );

                // Debug offset matrices (disabled to reduce console spam)
                // if (i < 3)
                // {
                //     ConsoleViewModel.Log(
                //         $"[SkinnedRenderer] Offset[{i}] Translation={_offsetMatrices[i].Translation}");
                // }
            }
            
            // Subscribe to bone transform changes
            foreach (var bone in Bones)
            {
                if (bone?.Transform != null)
                    bone.Transform.PropertyChanged += OnBoneTransformChanged;
            }

            _bonesInitialized = true;
            ConsoleViewModel.LogInfo($"[SkinnedRenderer] Set {bones.Count} bones with bind pose captured");
        }

        private void OnBoneTransformChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Recalculated automatically during Draw
        }


        /// <summary>
        /// Calculates bone world matrix by walking entire hierarchy INCLUDING GameObject
        /// Used for current frame animation
        /// </summary>
        private Matrix CalculateBoneToWorldSpace(int boneIndex)
        {
            Matrix result = Bones[boneIndex].Transform.LocalMatrix;

            // Walk up the hierarchy from bone to root
            var parent = Bones[boneIndex].Parent;
            while (parent != null) 
            {
                result = result * parent.Transform.LocalMatrix;
                parent = parent.Parent;
            }
            
            // CRITICAL: The bones are children of GameObject, but the loop above
            // stops when we reach the top of the bone hierarchy.
            // We need to ALSO include the GameObject's transform to get true world space!
            // Without this, the mesh renders at origin even if GameObject is moved.
            // Note: This is already included if GameObject is in the hierarchy above,
            // but we make it explicit here for clarity and to handle edge cases.

            return result;
        }

        /// <summary>
        /// Calculates bone matrices for skinning.
        /// Uses offset matrices from file (Inverse Bind Pose) * Current Bone World Matrix
        /// This is the standard Matrix Palette Skinning formula.
        /// </summary>
        private void CalculateBoneMatrices()
        {
            if (_offsetMatrices == null)
                return;
            
            // Auto-discover bones from hierarchy if not set (happens after scene load)
            if ((Bones == null || Bones.Count == 0) && GameObject != null && !string.IsNullOrEmpty(_originalModelPath))
            {
                ConsoleViewModel.LogInfo($"[SkinnedRenderer] Auto-discovering bones for {GameObject.Name}");
                TryDiscoverBonesFromHierarchy();
            }
                
            if (Bones == null || Bones.Count == 0)
                return;

            for (int i = 0; i < Bones.Count && i < _offsetMatrices.Length; i++)
            {
                // Get current bone transformation in world space
                Matrix currentBoneWorld = CalculateBoneToWorldSpace(i);
                
                // Debug first bone (disabled - called every frame)
                // if (i == 0)
                // {
                //     ConsoleViewModel.Log($"[SkinnedRenderer] Bone[0] '{Bones[0].Name}' world matrix translation: ({currentBoneWorld.M41}, {currentBoneWorld.M42}, {currentBoneWorld.M43})");
                //     ConsoleViewModel.Log($"[SkinnedRenderer] Bone[0] parent: {Bones[0].Parent?.Name ?? "NULL"}");
                //     if (GameObject != null)
                //     {
                //         ConsoleViewModel.Log($"[SkinnedRenderer] GameObject '{GameObject.Name}' position: {GameObject.Transform.LocalPosition}");
                //     }
                // }
                
                // Standard skinning: InverseBindPose * CurrentBoneWorld
                // offsetMatrices from file ARE the InverseBindPose matrices
                _boneMatrices[i] = _offsetMatrices[i] * currentBoneWorld;
            }
        }
        
        /// <summary>
        /// Try to find bones in the GameObject's hierarchy
        /// Used after scene deserialization when bones were loaded but not connected
        /// </summary>
        private async void TryDiscoverBonesFromHierarchy()
        {
            if (GameObject == null || string.IsNullOrEmpty(_originalModelPath))
                return;
                
            // Load model data to get bone names
            var modelData = await Assets.ModelImporter.LoadModelDataAsync(_originalModelPath);
            if (modelData == null || modelData.Bones.Count == 0)
                return;
            
            // For skinned FBX models, bones are typically SIBLINGS of the mesh GameObject
            // Structure: Root → [Mesh (this), Bone1, Bone2, ...]
            // So we search in Parent.Children, not GameObject.Children
            GameObject searchRoot = GameObject.Parent ?? GameObject;
            
            ConsoleViewModel.LogInfo($"[SkinnedRenderer] Searching for bones in '{searchRoot.Name}' (parent of '{GameObject.Name}')");
            // ConsoleViewModel.Log($"[SkinnedRenderer] Search root has {searchRoot.Children.Count} children");
                
            var bones = new List<GameObject>();
            var offsetMatrices = new List<System.Numerics.Matrix4x4>();
            
            // STRATEGY 1: Try ID-based reconnection first (most reliable)
            if (BoneIds != null && BoneIds.Count == modelData.Bones.Count)
            {
                ConsoleViewModel.LogInfo($"[SkinnedRenderer] Attempting ID-based bone reconnection ({BoneIds.Count} bones)");
                
                bool allBonesFound = true;
                for (int i = 0; i < BoneIds.Count; i++)
                {
                    var boneId = BoneIds[i];
                    var boneObj = FindGameObjectById(searchRoot, boneId);
                    
                    if (boneObj != null)
                    {
                        bones.Add(boneObj);
                        offsetMatrices.Add(modelData.Bones[i].OffsetMatrix);
                        // ConsoleViewModel.Log($"[SkinnedRenderer] Found bone by ID: {boneObj.Name} (ID: {boneId})");
                    }
                    else
                    {
                        ConsoleViewModel.LogWarning($"[SkinnedRenderer] Bone ID {boneId} not found in hierarchy");
                        allBonesFound = false;
                        break;
                    }
                }
                
                if (allBonesFound && bones.Count > 0)
                {
                    ConsoleViewModel.LogInfo($"[SkinnedRenderer] ✓ ID-based reconnection successful! ({bones.Count} bones)");
                    SetBones(bones, offsetMatrices);
                    return;
                }
                
                // Clear failed attempt
                bones.Clear();
                offsetMatrices.Clear();
                ConsoleViewModel.LogInfo($"[SkinnedRenderer] ID-based reconnection failed, trying name-based...");
            }
            
            // STRATEGY 2: Fall back to name-based search
            ConsoleViewModel.LogInfo($"[SkinnedRenderer] Attempting name-based bone discovery");
            
            foreach (var boneData in modelData.Bones)
            {
                var boneObj = FindChildRecursive(searchRoot, boneData.Name);
                if (boneObj != null)
                {
                    bones.Add(boneObj);
                    offsetMatrices.Add(boneData.OffsetMatrix);
                    // ConsoleViewModel.Log($"[SkinnedRenderer] Found bone by name: {boneData.Name}");
                }
                else
                {
                    ConsoleViewModel.LogWarning($"[SkinnedRenderer] Bone '{boneData.Name}' not found in hierarchy");
                }
            }
            
            if (bones.Count > 0)
            {
                ConsoleViewModel.LogInfo($"[SkinnedRenderer] ✓ Name-based discovery found {bones.Count} bones");
                SetBones(bones, offsetMatrices);
            }
            else
            {
                ConsoleViewModel.LogError($"[SkinnedRenderer] No bones found using either method!");
            }
        }
        
        /// <summary>
        /// Find a GameObject by its ID in the hierarchy (recursive search)
        /// </summary>
        private GameObject FindGameObjectById(GameObject root, Guid id)
        {
            if (root == null || id == Guid.Empty)
                return null;
                
            // Check root first
            if (root.Id == id)
                return root;
                
            // Search children recursively
            foreach (var child in root.Children)
            {
                var found = FindGameObjectById(child, id);
                if (found != null)
                    return found;
            }
            
            return null;
        }
        
        private GameObject FindChildRecursive(GameObject parent, string name)
        {
            foreach (var child in parent.Children)
            {
                if (child.Name == name)
                    return child;
                    
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Creates SkinnedVertex buffers
        /// </summary>
        protected override DeviceResources CreateResourcesForDevice(GraphicsDevice device)
        {
            if (_meshData == null)
                return null;

            var vertices = _meshData.Vertices;
            var normals = _meshData.Normals;
            var texCoords = _meshData.TexCoords;
            var indices = _meshData.Indices;
            var boneIndices = _meshData.BoneIndices;
            var boneWeights = _meshData.BoneWeights;

            var skinnedVertices = new SkinnedVertex[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                var pos = vertices[i];
                var norm = i < normals.Count ? normals[i] : System.Numerics.Vector3.Zero;
                var tex = i < texCoords.Count ? texCoords[i] : System.Numerics.Vector2.Zero;
                var idx = i < boneIndices.Count ? boneIndices[i] : System.Numerics.Vector4.Zero;
                var wgt = i < boneWeights.Count ? boneWeights[i] : new System.Numerics.Vector4(1, 0, 0, 0);

                // Normalize weights
                float sum = wgt.X + wgt.Y + wgt.Z + wgt.W;
                if (sum < 0.001f)
                    wgt = new System.Numerics.Vector4(1, 0, 0, 0);
                else
                    wgt /= sum;

                skinnedVertices[i] = new SkinnedVertex(
                    new Vector3(pos.X, pos.Y, pos.Z),
                    new Vector3(norm.X, norm.Y, norm.Z),
                    new Vector2(tex.X, tex.Y),
                    new Vector4(idx.X, idx.Y, idx.Z, idx.W),
                    new Vector4(wgt.X, wgt.Y, wgt.Z, wgt.W)
                );
            }

            var vb = new VertexBuffer(device, SkinnedVertex.VertexDeclaration, skinnedVertices.Length, BufferUsage.WriteOnly);
            vb.SetData(skinnedVertices);

            var ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.WriteOnly);
            ib.SetData(indices.ToArray());

            Effect effect;
            var content = GameControl.SharedContent ?? MonoGameControl.OwnContentManager;

            try
            {
                effect = content.Load<Effect>("Shaders/SkinnedPBR");
                // ConsoleViewModel.Log("[SkinnedRenderer] SkinnedPBR loaded");
            }
            catch
            {
                effect = PBREffectLoader.Load(device, content);
                ConsoleViewModel.LogWarning("[SkinnedRenderer] SkinnedPBR not found");
            }

            return new DeviceResources
            {
                VertexBuffer = vb,
                IndexBuffer = ib,
                Effect = effect
            };
        }

        protected override void ApplyCustomEffectParameters(Effect effect, GraphicsDevice device)
        {
            CalculateBoneMatrices();

            var bonesParam = effect.Parameters["Bones"];
            if (bonesParam != null)
                bonesParam.SetValue(_boneMatrices);

            base.ApplyCustomEffectParameters(effect, device);
        }

        public new void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition,
            Texture2D shadowMap = null, Matrix? lightViewProj = null)
        {
            CalculateBoneMatrices();
            base.Draw(device, view, projection, cameraPosition, shadowMap, lightViewProj);
        }

        /// <summary>
        /// Override to apply bone matrices for shadow rendering
        /// </summary>
        public override void DrawWithCustomEffect(Effect customEffect, Matrix lightViewProj)
        {
            // Update bone matrices before shadow pass
            CalculateBoneMatrices();
            
            // Apply bone matrices to shadow shader if it supports them
            var bonesParam = customEffect.Parameters["Bones"];
            if (bonesParam != null)
            {
                bonesParam.SetValue(_boneMatrices);
                // ConsoleViewModel.Log($"[SkinnedRenderer] ✓ Applied {_boneMatrices.Length} bone matrices to shadow shader");
            }
            else
            {
                ConsoleViewModel.LogWarning($"[SkinnedRenderer] Shadow shader has no 'Bones' parameter!");
            }
            
            // Call base implementation to actually draw
            base.DrawWithCustomEffect(customEffect, lightViewProj);
        }
    }
}