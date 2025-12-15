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

        // MAX_BONES in shader = 128
        private readonly Matrix[] _boneMatrices = new Matrix[128];
        private Matrix[] _offsetMatrices;
        private bool _bonesInitialized = false;

        public SkinnedModelRendererComponent()
        {
            ConsoleViewModel.Log("[SkinnedRenderer] Component created");

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

                if (i < 3)
                {
                    ConsoleViewModel.Log(
                        $"[SkinnedRenderer] Offset[{i}] Translation={_offsetMatrices[i].Translation}");
                }
            }
            
            // Subscribe to bone transform changes
            foreach (var bone in Bones)
            {
                if (bone?.Transform != null)
                    bone.Transform.PropertyChanged += OnBoneTransformChanged;
            }

            _bonesInitialized = true;
            ConsoleViewModel.Log($"[SkinnedRenderer] Set {bones.Count} bones with bind pose captured");
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

            var parent = Bones[boneIndex].Parent;
            while (parent != null) 
            {
                result = result * parent.Transform.LocalMatrix;
                parent = parent.Parent;
            }

            return result;
        }

        /// <summary>
        /// Calculates bone matrices for skinning.
        /// Uses offset matrices from file (Inverse Bind Pose) * Current Bone World Matrix
        /// This is the standard Matrix Palette Skinning formula.
        /// </summary>
        private void CalculateBoneMatrices()
        {
            if (Bones == null || Bones.Count == 0 || _offsetMatrices == null)
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
                ConsoleViewModel.Log("[SkinnedRenderer] SkinnedPBR loaded");
            }
            catch
            {
                effect = PBREffectLoader.Load(device, content);
                ConsoleViewModel.Log("[SkinnedRenderer] WARNING: SkinnedPBR not found");
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
    }
}