// Outline Shader - Renders colored outline around selected objects
// Uses two-pass technique with depth testing

float4x4 World;
float4x4 View;
float4x4 Projection;
float OutlineThickness = 0.03; // 3% scale increase
float4 OutlineColor = float4(1.0, 0.6, 0.0, 1.0); // Orange like Unity

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
};

// Pass 1: Render outline by scaling along normals
VertexShaderOutput OutlineVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    // Expand vertex along normal
    float4 expandedPos = input.Position + float4(input.Normal * OutlineThickness, 0);
    
    float4 worldPosition = mul(expandedPos, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    
    return output;
}

float4 OutlinePS() : COLOR0
{
    return OutlineColor;
}

// Technique
technique OutlineTechnique
{
    pass OutlinePass
    {
        VertexShader = compile vs_4_0 OutlineVS();
        PixelShader = compile ps_4_0 OutlinePS();
        
        // Render back faces for outline
        CullMode = CW; // Cull front faces (show back faces)
        
        // Depth settings - render behind actual object
        ZEnable = true;
        ZWriteEnable = false;
        ZFunc = LessEqual;
    }
}
