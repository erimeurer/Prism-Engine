using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameEditor.Core.Graphics
{
    /// <summary>
    /// Vertex format for skinned meshes (skeletal animation)
    /// Includes bone indices and weights for GPU skinning
    /// </summary>
    public struct SkinnedVertex : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector4 BoneIndices;  // Up to 4 bones per vertex
        public Vector4 BoneWeights;  // Corresponding weights

        public SkinnedVertex(Vector3 position, Vector3 normal, Vector2 texCoord, 
            Vector4 boneIndices, Vector4 boneWeights)
        {
            Position = position;
            Normal = normal;
            TextureCoordinate = texCoord;
            BoneIndices = boneIndices;
            BoneWeights = boneWeights;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendIndices, 0),
            new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0)
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}
