#nullable disable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameEditor.Core.Graphics;
using MonoGameEditor.Core.Materials;
using MonoGameEditor.Core;

// using MonoGameEditor.Controls; // Removed to decouple core from UI

namespace MonoGameEditor.Core.Components
{
    /// <summary>
    /// Renders skinned meshes with skeletal animation support
    /// </summary>
    public class SkinnedModelRendererComponent : ModelRendererComponent
    {
        public override string ComponentName => "Skinned Model Renderer";

        // Reference to ModelData for AnimationPoseBuilder
        public Assets.ModelData Model { get; set; }
        
        // Final bone matrices ready for GPU (computed by AnimationPoseBuilder)
        public System.Numerics.Matrix4x4[] FinalBoneMatrices { get; set; }

        // Bone GameObjects (must match mesh bone order)
        public List<GameObject> Bones { get; set; } = new List<GameObject>();
        
        // Persisted bone IDs for reconnection after scene load
        public List<Guid> BoneIds { get; set; } = new List<Guid>();

        // MAX_BONES in shader = 128
        private Matrix[] _boneMatrices = new Matrix[128]; // Cached for GPU (Model → World space matrices)
        private Matrix[] _offsetMatrices;
        private bool _bonesInitialized = false;
        
        // DEBUG: Track offset matrix changes
        private int _lastOffsetHash = 0;

        public SkinnedModelRendererComponent()
        {
            // Logger.Log("[SkinnedRenderer] Component created");

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
                //     Logger.Log(
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
            
            // DEBUG: Track offset matrix hash AND bone object IDs to detect resets
            if (_offsetMatrices != null && _offsetMatrices.Length > 0)
            {
                int hash = 0;
                for (int i = 0; i < Math.Min(3, _offsetMatrices.Length); i++)
                {
                    hash ^= _offsetMatrices[i].GetHashCode();
                }
                
                // Track first 3 bone IDs to detect if bones are replaced
                string bone0Id = Bones.Count > 0 ? Bones[0]?.Id.ToString() : "null";
                string bone1Id = Bones.Count > 1 ? Bones[1]?.Id.ToString() : "null";
                string bone2Id = Bones.Count > 2 ? Bones[2]?.Id.ToString() : "null";
            }
            
            Logger.Log($"[SkinnedRenderer] Set {bones.Count} bones with bind pose captured");
        }

        private void OnBoneTransformChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Recalculated automatically during Draw
        }

        /// <summary>
        /// Calculates bone world matrix. 
        /// The SkinnedPBR shader expects bone matrices to already be in world space.
        /// </summary>
        private Matrix CalculateBoneToWorldSpace(int boneIndex)
        {
            if (Bones == null || boneIndex >= Bones.Count || Bones[boneIndex] == null)
                return Matrix.Identity;

            return Bones[boneIndex].Transform.WorldMatrix;
        }

        /// <summary>
        /// Calculates bone matrices for skinning.
        /// Uses FinalBoneMatrices from AnimationPoseBuilder if available (animated),
        /// otherwise falls back to Transform-based calculation (bind pose).
        /// </summary>
        private void CalculateBoneMatrices()
        {
            if (_offsetMatrices == null)
                return;
            
            // CRITICAL: If FinalBoneMatrices exists (from AnimationPoseBuilder), convert to world space
            if (FinalBoneMatrices != null && FinalBoneMatrices.Length > 0)
            {
                // AnimationPoseBuilder computes matrices in MODEL SPACE (relative to the mesh origin)
                // We MUST transform them to WORLD SPACE using the MESH's world matrix.
                Matrix modelToWorld = GameObject?.Transform.WorldMatrix ?? Matrix.Identity;
                
                // Convert from System.Numerics to XNA and apply world transform
                for (int i = 0; i < Math.Min(FinalBoneMatrices.Length, _boneMatrices.Length); i++)
                {
                    var m = FinalBoneMatrices[i];
                    Matrix modelSpace = new Matrix(
                        m.M11, m.M12, m.M13, m.M14,
                        m.M21, m.M22, m.M23, m.M24,
                        m.M31, m.M32, m.M33, m.M34,
                        m.M41, m.M42, m.M43, m.M44
                    );
                    
                    // Transform to world space: ModelSpace * meshWorld
                    _boneMatrices[i] = modelSpace * modelToWorld;
                }
                return;
            }
            
            // Fallback: Use Transform-based calculation (for non-animated models or bind pose)
            // DEBUG: Check if offset matrices changed
            int currentHash = 0;
            for (int i = 0; i < Math.Min(3, _offsetMatrices.Length); i++)
            {
                currentHash ^= _offsetMatrices[i].GetHashCode();
            }
            if (currentHash != _lastOffsetHash)
            {
                _lastOffsetHash = currentHash;
            }
            
            // Auto-discover bones from hierarchy if not set (happens after scene load)
            if ((Bones == null || Bones.Count == 0) && GameObject != null && !string.IsNullOrEmpty(_originalModelPath))
            {
                TryDiscoverBonesFromHierarchy();
            }
                
            if (Bones == null || Bones.Count == 0)
                return;

            for (int i = 0; i < Bones.Count && i < _offsetMatrices.Length; i++)
            {
                // Get current bone transformation in world space
                Matrix currentBoneWorld = CalculateBoneToWorldSpace(i);
                
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
            
            Logger.Log($"[SkinnedRenderer] Searching for bones in '{searchRoot.Name}' (parent of '{GameObject.Name}')");
            // Logger.Log($"[SkinnedRenderer] Search root has {searchRoot.Children.Count} children");
                
            var bones = new List<GameObject>();
            var offsetMatrices = new List<System.Numerics.Matrix4x4>();
            
            // STRATEGY 1: Try ID-based reconnection first (most reliable)
            if (BoneIds != null && BoneIds.Count == modelData.Bones.Count)
            {
                Logger.Log($"[SkinnedRenderer] Attempting ID-based bone reconnection ({BoneIds.Count} bones)");
                
                bool allBonesFound = true;
                for (int i = 0; i < BoneIds.Count; i++)
                {
                    var boneId = BoneIds[i];
                    var boneObj = FindGameObjectById(searchRoot, boneId);
                    
                    if (boneObj != null)
                    {
                        bones.Add(boneObj);
                        offsetMatrices.Add(modelData.Bones[i].OffsetMatrix);
                        // Logger.Log($"[SkinnedRenderer] Found bone by ID: {boneObj.Name} (ID: {boneId})");
                    }
                    else
                    {
                        Logger.LogWarning($"[SkinnedRenderer] Bone ID {boneId} not found in hierarchy");
                        allBonesFound = false;
                        break;
                    }
                }
                
                if (allBonesFound && bones.Count > 0)
                {
                    Logger.Log($"[SkinnedRenderer] ✓ ID-based reconnection successful! ({bones.Count} bones)");
                    SetBones(bones, offsetMatrices);
                    return;
                }
                
                // Clear failed attempt
                bones.Clear();
                offsetMatrices.Clear();
                Logger.Log($"[SkinnedRenderer] ID-based reconnection failed, trying name-based...");
            }
            
            // STRATEGY 2: Fall back to name-based search
            Logger.Log($"[SkinnedRenderer] Attempting name-based bone discovery");
            
            foreach (var boneData in modelData.Bones)
            {
                var boneObj = FindChildRecursive(searchRoot, boneData.Name);
                if (boneObj != null)
                {
                    bones.Add(boneObj);
                    offsetMatrices.Add(boneData.OffsetMatrix);
                    // Logger.Log($"[SkinnedRenderer] Found bone by name: {boneData.Name}");
                }
                else
                {
                    Logger.LogWarning($"[SkinnedRenderer] Bone '{boneData.Name}' not found in hierarchy");
                }
            }
            
            if (bones.Count > 0)
            {
                Logger.Log($"[SkinnedRenderer] ✓ Name-based discovery found {bones.Count} bones");
                SetBones(bones, offsetMatrices);
            }
            else
            {
                Logger.LogError($"[SkinnedRenderer] No bones found using either method!");

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
            // CRITICAL FIX: Only create resources for the device we're actually rendering to
            // GameControl (Game tab) and MonoGameControl (Scene tab) have separate devices
            // Creating resources for wrong device causes corruption
            var gameDevice = GraphicsManager.GraphicsDevice;
            var sceneDevice = GraphicsManager.GraphicsDevice;
            
            bool isGameDevice = (device == gameDevice);
            bool isSceneDevice = (device == sceneDevice);
            
            // CRITICAL FIX: Always clear and recreate for new device
            // Device Reset can corrupt shared Effects
            
            if (_meshData == null)
            {
                return null;
            }
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
            var content = GetContentManagerForDevice(device);

            try
            {
                // Load via ContentManager (now safe because each view has its own CM and device)
                effect = content.Load<Effect>("Shaders/SkinnedPBR");
                // Logger.Log($"[SkinnedRenderer] Loaded SkinnedPBR from ContentManager for device {device.GetHashCode()}");
            }
            catch (Exception ex)
            {
                // Try PBR fallback
                effect = PBREffectLoader.Load(device, content);
                Logger.LogError($"[SkinnedRenderer] Failed to load SkinnedPBR via ContentManager: {ex.Message}. Using PBR fallback.");
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
            {
                bonesParam.SetValue(_boneMatrices);
            }
            
            // Sync with base parameters

            base.ApplyCustomEffectParameters(effect, device);
        }

        // DEBUG: Skeleton Visualization
        public bool ShowSkeleton { get; set; } = true;

        public override void Draw(GraphicsDevice device, Matrix view, Matrix projection, Vector3 cameraPosition,
            Texture2D shadowMap = null, Matrix? lightViewProj = null)
        {
            CalculateBoneMatrices();
            base.Draw(device, view, projection, cameraPosition, shadowMap, lightViewProj);

            if (ShowSkeleton)
                DrawSkeleton(device, view, projection);
        }

        private void DrawSkeleton(GraphicsDevice device, Matrix view, Matrix projection)
        {
            if (Bones == null || Bones.Count == 0) return;

            // Simple immediate mode drawing for debug
            var vertices = new List<VertexPositionColor>();
            
            foreach (var bone in Bones)
            {
                if (bone == null) continue;

                var parent = bone.Parent;
                // Only draw if parent is also a bone (in the list) or at least valid
                // We'll draw connection to parent regardless if it's in the list, as long as it's not null
                if (parent != null)
                {
                    var start = parent.Transform.Position;
                    var end = bone.Transform.Position;

                    vertices.Add(new VertexPositionColor(new Vector3(start.X, start.Y, start.Z), Microsoft.Xna.Framework.Color.Lime));
                    vertices.Add(new VertexPositionColor(new Vector3(end.X, end.Y, end.Z), Microsoft.Xna.Framework.Color.Lime));
                }
            }

            if (vertices.Count > 0)
            {
                // CRITICAL: Draw ON TOP of everything (disable depth test)
                var prevDepth = device.DepthStencilState;
                device.DepthStencilState = DepthStencilState.None;
                
                var basicEffect = new BasicEffect(device);
                basicEffect.World = Matrix.Identity;
                basicEffect.View = view;
                basicEffect.Projection = projection;
                basicEffect.VertexColorEnabled = true;

                foreach (var pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.LineList, vertices.ToArray(), 0, vertices.Count / 2);
                }
                
                device.DepthStencilState = prevDepth;
            }
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
                // Logger.Log($"[SkinnedRenderer] ✓ Applied {_boneMatrices.Length} bone matrices to shadow shader");
            }
            else
            {
                Logger.LogWarning($"[SkinnedRenderer] Shadow shader has no 'Bones' parameter!");
            }
            
            // Call base implementation to actually draw
            base.DrawWithCustomEffect(customEffect, lightViewProj);
        }
    }
}