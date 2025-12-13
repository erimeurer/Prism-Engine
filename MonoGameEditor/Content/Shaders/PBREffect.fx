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

// Shadow Properties
float4x4 LightViewProjection;
bool UseShadows;
int ShadowQuality; // 0=None, 1=Hard, 2=Soft
float ShadowStrength;
float ShadowBias;

texture ShadowMap;
sampler2D ShadowMapSampler = sampler_state
{
    Texture = <ShadowMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

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

texture MetallicTexture;
sampler2D MetallicSampler = sampler_state
{
    Texture = <MetallicTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

texture RoughnessTexture;
sampler2D RoughnessSampler = sampler_state
{
    Texture = <RoughnessTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

texture AOTexture;
sampler2D AOSampler = sampler_state
{
    Texture = <AOTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

// Texture usage flags
bool UseAlbedoMap = false;
bool UseNormalMap = false;
bool UseMetallicMap = false;
bool UseRoughnessMap = false;
bool UseAOMap = false;

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
    // Sample Albedo texture if enabled, otherwise use color
    float3 albedo = AlbedoColor.rgb;
    if (UseAlbedoMap)
    {
        albedo = tex2D(AlbedoSampler, input.TexCoord).rgb;
    }
    
    // Sample Normal map if enabled
    float3 N = normalize(input.Normal);
    if (UseNormalMap)
    {
        // Sample normal map (assume it's in tangent space, stored as RGB [0,1])
        float3 normalMap = tex2D(NormalSampler, input.TexCoord).rgb;
        // Convert from [0,1] to [-1,1]
        normalMap = normalMap * 2.0 - 1.0;
        
        // For now, just use the normal map directly (proper tangent-space transformation would require tangent/bitangent)
        // This is a simplified version - for full PBR you'd want proper tangent space
        N = normalize(normalMap);
    }
    
    float3 V = normalize(CameraPosition - input.WorldPos);
    
    // Sample Metallic texture if enabled, otherwise use parameter
    float metallic = Metallic;
    if (UseMetallicMap)
    {
        metallic = tex2D(MetallicSampler, input.TexCoord).r; // Metallic stored in R channel
    }
    
    // Sample Roughness texture if enabled, otherwise use parameter
    float roughness = Roughness;
    if (UseRoughnessMap)
    {
        roughness = tex2D(RoughnessSampler, input.TexCoord).r; // Roughness stored in R channel
    }
    
    // Sample AO texture if enabled, otherwise use parameter
    float ao = AO;
    if (UseAOMap)
    {
        ao = tex2D(AOSampler, input.TexCoord).r; // AO stored in R channel
    }
    
    // Calculate reflectance at normal incidence
    // For dielectrics (non-metals) F0 is 0.04, for metals it's the albedo color
    float3 F0 = float3(0.04, 0.04, 0.04);
    F0 = lerp(F0, albedo, metallic);
    
    // Reflectance equation
    float3 Lo = float3(0.0, 0.0, 0.0);
    
    // Directional light calculation
    float3 L = normalize(-LightDirection);
    float3 H = normalize(V + L);
    
    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);
    
    float3 kS = F; // Specular reflection coefficient
    float3 kD = float3(1.0, 1.0, 1.0) - kS; // Diffuse reflection coefficient
    kD *= 1.0 - metallic; // Metals don't have diffuse reflection
    
    float NdotL = max(dot(N, L), 0.0);
    
    // Specular BRDF
    float3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * NdotL;
    float3 specular = numerator / max(denominator, 0.001);
    
    // Shadow Calculation
    float shadow = 1.0;
    
    // Only calculate if shadows are enabled AND the map is bound
    if (UseShadows)
    {
        // Transform WorldPos to Light Projection Space
        float4 lightScreenPos = mul(float4(input.WorldPos, 1.0), LightViewProjection);
        lightScreenPos /= lightScreenPos.w; // Perspective divide
        
        // Map from (-1, 1) to (0, 1) space
        float2 shadowTexCoord = 0.5 * lightScreenPos.xy + float2(0.5, 0.5);
        shadowTexCoord.y = 1.0 - shadowTexCoord.y; // Flip Y for Texture coords
        
        // Check bounds (0 to 1) - if outside, no shadow (clamped to 1.0)
        if (shadowTexCoord.x >= 0.0 && shadowTexCoord.x <= 1.0 &&
            shadowTexCoord.y >= 0.0 && shadowTexCoord.y <= 1.0)
        {
            float currentDepth = lightScreenPos.z;
            float bias = 0.0002; // Ultra-low bias thanks to backface culling
            float texelSize = 1.0 / 4096.0;
            
            // RANDOM ROTATION per pixel to eliminate banding (Unity technique)
            // Creates smooth gradient by rotating sample pattern
            float2 seed = shadowTexCoord * 100.0; // Use shadow texture coords as seed
            float randomAngle = frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453) * 6.28318; // 0 to 2*PI
            float cosAngle = cos(randomAngle);
            float sinAngle = sin(randomAngle);
            float2x2 rotationMatrix = float2x2(cosAngle, -sinAngle, sinAngle, cosAngle);
            
            float shadowFactor = 0.0;
            
            // PROFESSIONAL MULTI-QUALITY SHADOW SYSTEM (Unity-style)
            // Quality 1=Low (4), 2=Medium (8), 3=High (16), 4=VeryHigh (32)
            
            if (ShadowQuality == 1) // Low Quality - 4 samples, fast
            {
                // 4-tap bilinear PCF with rotation
                float shadowSum = 0.0;
                float filterRadius = 1.5;
                
                float2 offsets[4] = {
                    float2(-0.5, -0.5), float2(0.5, -0.5),
                    float2(-0.5, 0.5), float2(0.5, 0.5)
                };
                
                for(int i = 0; i < 4; i++)
                {
                    float2 rotatedOffset = mul(offsets[i], rotationMatrix) * filterRadius * texelSize;
                    float pcfDepth = tex2D(ShadowMapSampler, shadowTexCoord + rotatedOffset).r;
                    if (currentDepth - bias > pcfDepth) shadowSum += 1.0;
                }
                
                shadowFactor = shadowSum / 4.0;
            }
            else if (ShadowQuality == 2) // Medium Quality - 8 samples, balanced
            {
                // 8-sample Poisson disk
                static const float2 poisson8[8] = {
                    float2(-0.7071, 0.7071), float2(-0.0000, -0.8750),
                    float2(0.5303, 0.5303), float2(-0.6250, -0.0000),
                    float2(0.3536, -0.3536), float2(0.0000, 0.6250),
                    float2(-0.3536, -0.3536), float2(0.7071, 0.0000)
                };
                
                float shadowSum = 0.0;
                float filterRadius = 2.5;
                
                for(int i = 0; i < 8; i++)
                {
                    float2 rotatedOffset = mul(poisson8[i], rotationMatrix) * filterRadius * texelSize;
                    float pcfDepth = tex2D(ShadowMapSampler, shadowTexCoord + rotatedOffset).r;
                    if (currentDepth - bias > pcfDepth) shadowSum += 1.0;
                }
                
                shadowFactor = shadowSum / 8.0;
            }
            else if (ShadowQuality == 3) // High Quality - 16 samples, smooth
            {
                // 16-sample Poisson disk
                static const float2 poisson16[16] = {
                    float2(-0.94201624, -0.39906216), float2(0.94558609, -0.76890725),
                    float2(-0.094184101, -0.92938870), float2(0.34495938, 0.29387760),
                    float2(-0.91588581, 0.45771432), float2(-0.81544232, -0.87912464),
                    float2(-0.38277543, 0.27676845), float2(0.97484398, 0.75648379),
                    float2(0.44323325, -0.97511554), float2(0.53742981, -0.47373420),
                    float2(-0.26496911, -0.41893023), float2(0.79197514, 0.19090188),
                    float2(-0.24188840, 0.99706507), float2(-0.81409955, 0.91437590),
                    float2(0.19984126, 0.78641367), float2(0.14383161, -0.14100790)
                };
                
                float shadowSum = 0.0;
                float filterRadius = 3.5;
                
                for(int i = 0; i < 16; i++)
                {
                    float2 rotatedOffset = mul(poisson16[i], rotationMatrix) * filterRadius * texelSize;
                    float pcfDepth = tex2D(ShadowMapSampler, shadowTexCoord + rotatedOffset).r;
                    if (currentDepth - bias > pcfDepth) shadowSum += 1.0;
                }
                
                shadowFactor = shadowSum / 16.0;
            }
            else if (ShadowQuality == 4) // Very High Quality - 32 samples, ultra smooth (UNITY QUALITY!)
            {
                // 32-sample Poisson disk - MAXIMUM QUALITY
                static const float2 poisson32[32] = {
                    float2(-0.975402, -0.0711386), float2(-0.920505, -0.41125),
                    float2(-0.883908, -0.699843), float2(-0.771388, -0.890433),
                    float2(-0.571019, 0.744969), float2(-0.555064, 0.559374),
                    float2(-0.541534, 0.201836), float2(-0.515174, -0.186982),
                    float2(-0.506475, -0.554764), float2(-0.397458, -0.894557),
                    float2(-0.320911, 0.917206), float2(-0.279415, 0.411033),
                    float2(-0.234813, -0.395923), float2(-0.187043, 0.651528),
                    float2(-0.113087, -0.684156), float2(0.0116578, -0.934071),
                    float2(0.126116, 0.455607), float2(0.182669, -0.239951),
                    float2(0.339112, 0.914424), float2(0.342948, -0.533424),
                    float2(0.423332, 0.317099), float2(0.487635, -0.868818),
                    float2(0.505348, 0.655838), float2(0.540464, -0.126364),
                    float2(0.580653, -0.700114), float2(0.685879, 0.661535),
                    float2(0.695726, 0.327731), float2(0.736117, -0.464652),
                    float2(0.858321, 0.543018), float2(0.863971, -0.164021),
                    float2(0.952487, -0.569365), float2(0.988071, 0.224193)
                };
                
                float shadowSum = 0.0;
                float filterRadius = 4.5; // Maximum softness
                
                for(int i = 0; i < 32; i++)
                {
                    float2 rotatedOffset = mul(poisson32[i], rotationMatrix) * filterRadius * texelSize;
                    float pcfDepth = tex2D(ShadowMapSampler, shadowTexCoord + rotatedOffset).r;
                    if (currentDepth - bias > pcfDepth) shadowSum += 1.0;
                }
                
                shadowFactor = shadowSum / 32.0;
            }
            
            // Apply strength and invert
            shadow = lerp(1.0, 1.0 - shadowFactor, ShadowStrength);
        }
    }

    // Add to outgoing radiance Lo
    float3 radiance = LightColor * LightIntensity * shadow;
    Lo += (kD * albedo / PI + specular) * radiance * NdotL;
    
    // Ambient lighting (increased for better visibility)
    float3 ambient = float3(0.15, 0.15, 0.15) * albedo * ao;
    float3 color = ambient + Lo;
    
    // Tone mapping (Reinhard)
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
        VertexShader = compile vs_4_0 MainVS();
        PixelShader = compile ps_4_0 MainPS();
    }
}
