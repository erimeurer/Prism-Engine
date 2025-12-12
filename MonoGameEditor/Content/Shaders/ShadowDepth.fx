// Shadow Depth Shader
// Renders linear depth to texture (R32F or similar)

float4x4 World;
float4x4 LightViewProjection;

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 Depth : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    // Transform to light space
    float4 worldPos = mul(input.Position, World);
    output.Position = mul(worldPos, LightViewProjection);
    
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
