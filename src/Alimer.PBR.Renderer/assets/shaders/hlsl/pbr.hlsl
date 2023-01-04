// Physically Based Rendering
// Copyright (c) 2017-2018 Michał Siejak

// Physically Based shading model: Lambetrtian diffuse BRDF + Cook-Torrance microfacet specular BRDF + IBL for ambient.

// This implementation is based on "Real Shading in Unreal Engine 4" SIGGRAPH 2013 course notes by Epic Games.
// See: http://blog.selfshadow.com/publications/s2013-shading-course/karis/s2013_pbs_epic_notes_v2.pdf

#include "Alimer.hlsli"

static const uint NumLights = 3;

// Constant normal incidence Fresnel factor for all dielectrics.
static const float3 Fdielectric = 0.04;

cbuffer TransformConstants : register(b0)
{
	float4x4 skyProjectionMatrix;
	float4x4 sceneRotationMatrix;
};

cbuffer ShadingConstants : register(b1)
{
	struct {
		float3 direction;
		float3 radiance;
	} lights[NumLights];
	float3 eyePosition;
};

struct VertexInput {
	float3 position  : ATTRIBUTE0;
	float3 normal    : ATTRIBUTE1;
	float4 tangent   : ATTRIBUTE2;
	float3 bitangent : ATTRIBUTE3;
	float2 texcoord  : ATTRIBUTE4;
};

struct VertexOutput {
	float3 worldPosition : POSITION;
    float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	float3x3 tangentBasis : TBASIS;
	float4 position : SV_POSITION;
};

Texture2D albedoTexture : register(t0);
Texture2D normalTexture : register(t1);
Texture2D metalnessTexture : register(t2);
Texture2D roughnessTexture : register(t3);
TextureCube specularTexture : register(t4);
TextureCube irradianceTexture : register(t5);
Texture2D specularBRDF_LUT : register(t6);

SamplerState defaultSampler : register(s0);
SamplerState spBRDF_Sampler : register(s1);

// GGX/Towbridge-Reitz normal distribution function.
// Uses Disney's reparametrization of alpha = roughness^2.
float ndfGGX(float cosLh, float roughness)
{
	float alpha   = roughness * roughness;
	float alphaSq = alpha * alpha;

	float denom = (cosLh * cosLh) * (alphaSq - 1.0) + 1.0;
	return alphaSq / (PI * denom * denom);
}

// Single term for separable Schlick-GGX below.
float gaSchlickG1(float cosTheta, float k)
{
	return cosTheta / (cosTheta * (1.0 - k) + k);
}

// Schlick-GGX approximation of geometric attenuation function using Smith's method.
float gaSchlickGGX(float cosLi, float cosLo, float roughness)
{
	float r = roughness + 1.0;
	float k = (r * r) / 8.0; // Epic suggests using this roughness remapping for analytic lights.
	return gaSchlickG1(cosLi, k) * gaSchlickG1(cosLo, k);
}

// Shlick's approximation of the Fresnel factor.
float3 fresnelSchlick(float3 F0, float cosTheta)
{
	return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

// Returns number of mipmap levels for specular IBL environment map.
uint querySpecularTextureLevels()
{
	uint width, height, levels;
	specularTexture.GetDimensions(0, width, height, levels);
	return levels;
}

// Vertex shader
VertexOutput vertexMain(in VertexInput input)
{
    VertexOutput output;

    float4 position = float4(input.position, 1.0f);

    output.worldPosition = mul(sceneRotationMatrix, position).xyz;
    output.texcoord = input.texcoord;

    float3 normalVector = mul((float3x3) sceneRotationMatrix, input.normal);
    normalVector = normalize(normalVector);

	// Pass tangent space basis vectors (for normal mapping).
    float3 tangentVector = mul((float3x3) sceneRotationMatrix, input.tangent.xyz);
    tangentVector = normalize(tangentVector);

    float3 bitangentVector = normalize(cross(normalVector, tangentVector) * input.tangent.w);

    float3x3 tbnMatrix = float3x3(
        tangentVector.x, bitangentVector.x, normalVector.x,
        tangentVector.y, bitangentVector.y, normalVector.y,
        tangentVector.z, bitangentVector.z, normalVector.z);

    output.tangentBasis = tbnMatrix;
    
	float4x4 mvpMatrix = mul(viewProjectionMatrix, sceneRotationMatrix);
    output.position = mul(mvpMatrix, position);
	return output;
}

// Fragment shader
float4 fragmentMain(in VertexOutput input) : SV_Target
{
	// Sample input textures to get shading model params.
	float3 albedo = albedoTexture.Sample(defaultSampler, input.texcoord).rgb;
	float metalness = metalnessTexture.Sample(defaultSampler, input.texcoord).r;
	float roughness = roughnessTexture.Sample(defaultSampler, input.texcoord).r;

	// Outgoing light direction (vector from world-space fragment position to the "eye").
	float3 Lo = normalize(eyePosition - input.worldPosition);

	// Get current fragment's normal and transform to world space.
    float3 normalMap = normalTexture.Sample(defaultSampler, input.texcoord).rgb;
	float3 N = normalize(2.0 * normalTexture.Sample(defaultSampler, input.texcoord).rgb - 1.0);
	N = normalize(mul(input.tangentBasis, N));
	
	// Angle between surface normal and outgoing light direction.
	float cosLo = max(0.0, dot(N, Lo));
		
	// Specular reflection vector.
	float3 Lr = 2.0 * cosLo * N - Lo;

	// Fresnel reflectance at normal incidence (for metals use albedo color).
	float3 F0 = lerp(Fdielectric, albedo, metalness);

	// Direct lighting calculation for analytical lights.
	float3 directLighting = 0.0;
	for(uint i=0; i<NumLights; ++i)
	{
		float3 Li = -lights[i].direction;
		float3 Lradiance = lights[i].radiance;

		// Half-vector between Li and Lo.
		float3 Lh = normalize(Li + Lo);

		// Calculate angles between surface normal and various light vectors.
		float cosLi = max(0.0, dot(N, Li));
		float cosLh = max(0.0, dot(N, Lh));

		// Calculate Fresnel term for direct lighting. 
		float3 F  = fresnelSchlick(F0, max(0.0, dot(Lh, Lo)));
		// Calculate normal distribution for specular BRDF.
		float D = ndfGGX(cosLh, roughness);
		// Calculate geometric attenuation for specular BRDF.
		float G = gaSchlickGGX(cosLi, cosLo, roughness);

		// Diffuse scattering happens due to light being refracted multiple times by a dielectric medium.
		// Metals on the other hand either reflect or absorb energy, so diffuse contribution is always zero.
		// To be energy conserving we must scale diffuse BRDF contribution based on Fresnel factor & metalness.
		float3 kd = lerp(float3(1, 1, 1) - F, float3(0, 0, 0), metalness);

		// Lambert diffuse BRDF.
		// We don't scale by 1/PI for lighting & material units to be more convenient.
		// See: https://seblagarde.wordpress.com/2012/01/08/pi-or-not-to-pi-in-game-lighting-equation/
		float3 diffuseBRDF = kd * albedo;

		// Cook-Torrance specular microfacet BRDF.
		float3 specularBRDF = (F * D * G) / max(Epsilon, 4.0 * cosLi * cosLo);

		// Total contribution for this light.
		directLighting += (diffuseBRDF + specularBRDF) * Lradiance * cosLi;
	}

	// Ambient lighting (IBL).
	float3 ambientLighting;
	{
		// Sample diffuse irradiance at normal direction.
		float3 irradiance = irradianceTexture.Sample(defaultSampler, N).rgb;

		// Calculate Fresnel term for ambient lighting.
		// Since we use pre-filtered cubemap(s) and irradiance is coming from many directions
		// use cosLo instead of angle with light's half-vector (cosLh above).
		// See: https://seblagarde.wordpress.com/2011/08/17/hello-world/
		float3 F = fresnelSchlick(F0, cosLo);

		// Get diffuse contribution factor (as with direct lighting).
		float3 kd = lerp(1.0 - F, 0.0, metalness);

		// Irradiance map contains exitant radiance assuming Lambertian BRDF, no need to scale by 1/PI here either.
		float3 diffuseIBL = kd * albedo * irradiance;

		// Sample pre-filtered specular reflection environment at correct mipmap level.
		uint specularTextureLevels = querySpecularTextureLevels();
		float3 specularIrradiance = specularTexture.SampleLevel(defaultSampler, Lr, roughness * specularTextureLevels).rgb;

		// Split-sum approximation factors for Cook-Torrance specular BRDF.
		float2 specularBRDF = specularBRDF_LUT.Sample(spBRDF_Sampler, float2(cosLo, roughness)).rg;

		// Total specular IBL contribution.
		float3 specularIBL = (F0 * specularBRDF.x + specularBRDF.y) * specularIrradiance;

		// Total ambient lighting contribution.
		ambientLighting = diffuseIBL + specularIBL;
	}

	// Final fragment color.
	return float4(directLighting + ambientLighting, 1.0);
}
