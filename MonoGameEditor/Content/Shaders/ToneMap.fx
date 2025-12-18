#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0
	#define PS_SHADERMODEL ps_4_0
#endif

Texture2D ScreenTexture;
float Exposure;

// PP Settings
bool UseAA;
bool UsePRAA;
bool UseMotionBlur;
float MotionBlurIntensity;
float2 ScreenSize;
float2 BlurDirection; // Based on camera velocity

sampler2D TextureSampler = sampler_state
{
	Texture = <ScreenTexture>;
	MinFilter = Linear;
	MagFilter = Linear;
	MipFilter = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	output.Position = input.Position;
	output.TexCoord = input.TexCoord;

	return output;
}

// Narkowicz ACES Tone Mapping approximation
float3 ACESFilm(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x*(a*x+b))/(x*(c*x+d)+e));
}

// Simple FXAA Implementation
float4 ApplyFXAA(float2 texCoord)
{
    float2 inverseScreenSize = 1.0f / ScreenSize;
    
    float lumaCenter = dot(tex2D(TextureSampler, texCoord).rgb, float3(0.299, 0.587, 0.114));
    float lumaL = dot(tex2D(TextureSampler, texCoord + float2(-1, 0) * inverseScreenSize).rgb, float3(0.299, 0.587, 0.114));
    float lumaR = dot(tex2D(TextureSampler, texCoord + float2(1, 0) * inverseScreenSize).rgb, float3(0.299, 0.587, 0.114));
    float lumaU = dot(tex2D(TextureSampler, texCoord + float2(0, -1) * inverseScreenSize).rgb, float3(0.299, 0.587, 0.114));
    float lumaD = dot(tex2D(TextureSampler, texCoord + float2(0, 1) * inverseScreenSize).rgb, float3(0.299, 0.587, 0.114));
    
    float lumaMin = min(lumaCenter, min(min(lumaL, lumaR), min(lumaU, lumaD)));
    float lumaMax = max(lumaCenter, max(max(lumaL, lumaR), max(lumaU, lumaD)));
    
    float2 dir;
    dir.x = -((lumaU + lumaD) - (lumaL + lumaR));
    dir.y = ((lumaL + lumaR) - (lumaU + lumaD));
    
    float dirReduce = max((lumaL + lumaR + lumaU + lumaD) * (0.25 * 0.125), 0.00001);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    
    dir = min(float2(8.0, 8.0), max(float2(-8.0, -8.0), dir * rcpDirMin)) * inverseScreenSize;
    
    float3 rgbA = 0.5 * (
        tex2D(TextureSampler, texCoord + dir * (1.0 / 3.0 - 0.5)).rgb +
        tex2D(TextureSampler, texCoord + dir * (2.0 / 3.0 - 0.5)).rgb);
    float3 rgbB = rgbA * 0.5 + 0.25 * (
        tex2D(TextureSampler, texCoord + dir * (0.0 / 3.0 - 0.5)).rgb +
        tex2D(TextureSampler, texCoord + dir * (3.0 / 3.0 - 0.5)).rgb);
    
    float lumaB = dot(rgbB, float3(0.299, 0.587, 0.114));
    if ((lumaB < lumaMin) || (lumaB > lumaMax)) return float4(rgbA, 1.0);
    return float4(rgbB, 1.0);
}

// Enhanced PRAA Antialiasing (Spatial)
float4 ApplyPRAA(float2 texCoord)
{
    float2 invSize = 1.0f / ScreenSize;
    float4 center = tex2D(TextureSampler, texCoord);
    
    // 5-tap cross sample for edge detection and smoothing
    // Using 1.5 offset for a soft, cinematic look
    float4 n = tex2D(TextureSampler, texCoord + float2(0, 1.5) * invSize);
    float4 s = tex2D(TextureSampler, texCoord + float2(0, -1.5) * invSize);
    float4 w = tex2D(TextureSampler, texCoord + float2(-1.5, 0) * invSize);
    float4 e = tex2D(TextureSampler, texCoord + float2(1.5, 0) * invSize);
    
    float4 average = (center + n + s + w + e) * 0.2;
    
    // Use FXAA for edge weighting
    float4 fxaa = ApplyFXAA(texCoord);
    
    return (average + fxaa) * 0.5;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 color;
    
    if (UsePRAA)
    {
        color = ApplyPRAA(input.TexCoord);
    }
    else if (UseAA)
    {
        color = ApplyFXAA(input.TexCoord);
    }
    else
    {
        color = tex2D(TextureSampler, input.TexCoord);
    }
    
    // Simple Motion Blur
    if (UseMotionBlur)
    {
        float2 blurVec = BlurDirection * MotionBlurIntensity;
        float4 blurColor = color;
        const int samples = 8;
        for (int i = 1; i < samples; ++i)
        {
            float2 offset = blurVec * (float(i) / float(samples - 1) - 0.5);
            blurColor += tex2D(TextureSampler, input.TexCoord + offset);
        }
        color = blurColor / float(samples);
    }
    
    // 1. Exposure
    color.rgb *= Exposure;
    
    // 2. ACES Tone Mapping
    color.rgb = ACESFilm(color.rgb);
    
    // 3. Gamma Correction (Linear -> sRGB)
    color.rgb = pow(color.rgb, 1.0 / 2.2);

	return float4(color.rgb, 1.0);
}

technique BasicColorDrawing
{
	pass P0
	{
		VertexShader = compile VS_SHADERMODEL MainVS();
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
