// PBR Effect - Physically Based Rendering using Cook-Torrance BRDF
// Implements Unity Standard shader (Metallic workflow)

// Matrices
float4x4 World;
float4x4 View;
float4x4 Projection;

// Camera
float3 CameraPosition;

// Light (single directional for now)
float3 LightDirection = float3(-0.5773, -0.5773, -0.5773); // normalized (-1,-1,-1)
float3 LightColor = float3(1, 1, 1);
float LightIntensity = 1.0;

// Material Properties
float4 AlbedoColor = float4(1, 1, 1, 1);
float Metallic = 0.0;
float Roughness = 0.5;
float AO = 1.0;

// Shadow Properties - REMOVED for stability
// float4x4 LightViewProjection;
// bool UseShadows;
// texture ShadowMap;

// Textures (optional)
texture AlbedoTexture;
sampler2D AlbedoSampler = sampler_state
{
    Texture = <AlbedoTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

texture NormalTexture;
sampler2D NormalSampler = sampler_state
{
    Texture = <NormalTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

// Vertex Shader Input
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

// Vertex Shader Output / Pixel Shader Input
struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float3 WorldPos : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
};

// Vertex Shader
VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    float4 worldPos = mul(input.Position, World);
    float4 viewPos = mul(worldPos, View);
    output.Position = mul(viewPos, Projection);
    
    output.WorldPos = worldPos.xyz;
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    
    return output;
}

// ===== PBR Functions =====

// Constants
static const float PI = 3.14159265359;

// Fresnel-Schlick approximation
float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

// GGX/Trowbridge-Reitz Normal Distribution Function
float DistributionGGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    
    float num = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    
    return num / max(denom, 0.0001);
}

// Schlick-GGX Geometry Function
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    
    float num = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    
    return num / max(denom, 0.0001);
}

// Smith's Geometry Function
float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    
    return ggx1 * ggx2;
}

// Pixel Shader
float4 MainPS(VertexShaderOutput input) : COLOR0
{
    // Sample textures
    float3 albedo = AlbedoColor.rgb;
    
    // Normal (from mesh, could be from normal map)
    float3 N = normalize(input.Normal);
    float3 V = normalize(CameraPosition - input.WorldPos);
    
    // Calculate reflectance at normal incidence
    // For dielectrics (non-metals) F0 is 0.04, for metals it's the albedo color
    float3 F0 = float3(0.04, 0.04, 0.04);
    F0 = lerp(F0, albedo, Metallic);
    
    // Reflectance equation
    float3 Lo = float3(0.0, 0.0, 0.0);
    
    // Directional light calculation
    float3 L = normalize(-LightDirection);
    float3 H = normalize(V + L);
    
    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, Roughness);
    float G = GeometrySmith(N, V, L, Roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
    
    float3 kS = F; // Specular reflection coefficient
    float3 kD = float3(1.0, 1.0, 1.0) - kS; // Diffuse reflection coefficient
    kD *= 1.0 - Metallic; // Metals don't have diffuse reflection
    
    float NdotL = max(dot(N, L), 0.0);
    
    // Specular BRDF
    float3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * NdotL;
    float3 specular = numerator / max(denominator, 0.001);
    
    // Shadow Calculation (Disabled)
    float shadow = 1.0;
    
    /*
    if (UseShadows)
    {
        // ... Shadow Logic Removed ...
    }
    */

    // Add to outgoing radiance Lo
    float3 radiance = LightColor * LightIntensity * shadow;
    Lo += (kD * albedo / PI + specular) * radiance * NdotL;
    
    // Ambient lighting (simple approximation)
    // Ambient is NOT shadowed (usually)
    float3 ambient = float3(0.03, 0.03, 0.03) * albedo * AO;
    float3 color = ambient + Lo;
    
    // Tone mapping (simple Reinhard)
    color = color / (color + float3(1.0, 1.0, 1.0));
    
    // Gamma correction
    color = pow(color, float3(1.0/2.2, 1.0/2.2, 1.0/2.2));
    
    return float4(color, 1.0);
}

// Technique
technique PBRTechnique
{
    pass P0
    {
        VertexShader = compile vs_3_0 MainVS();
        PixelShader = compile ps_3_0 MainPS();
    }
}
