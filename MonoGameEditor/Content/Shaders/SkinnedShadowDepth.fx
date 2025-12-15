// Skinned Shadow Depth Shader
// Renders linear depth to texture with skeletal animation support

float4x4 World;
float4x4 LightViewProjection;

// Skeletal Animation - Bone transforms (max 128 bones)
#define MAX_BONES 128
float4x4 Bones[MAX_BONES];

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 BoneIndices : BLENDINDICES0;
    float4 BoneWeights : BLENDWEIGHT0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 Depth : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    // ===== SKELETAL SKINNING =====
    float4 skinnedPosition = float4(0, 0, 0, 0);
    
    // Process up to 4 bones per vertex
    for (int i = 0; i < 4; i++)
    {
        int boneIndex = (int)input.BoneIndices[i];
        float weight = input.BoneWeights[i];
        
        if (weight > 0.0)
        {
            // XNA/MonoGame convention: mul(Vector, Matrix)
            float4 localPosition = mul(input.Position, Bones[boneIndex]);
            skinnedPosition += localPosition * weight;
        }
    }
    
    // ===== TRANSFORM TO LIGHT SPACE =====
    // skinnedPosition is already in world space from bone matrices
    output.Position = mul(skinnedPosition, LightViewProjection);
    
    // Pass depth (Z, W) for perspective divide
    output.Depth = output.Position.zw;
    
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // Linear Depth = Z / W (0..1 range)
    float depth = input.Depth.x / input.Depth.y;
    
    // Return float depth
    return float4(depth, 0, 0, 1);
}

technique ShadowDepth
{
    pass P0
    {
        VertexShader = compile vs_4_0 MainVS();
        PixelShader = compile ps_4_0 MainPS();
    }
}
