// Physically Based Rendering
// Copyright (c) 2017-2018 Michał Siejak

// Environment skybox.

#include "Alimer.hlsli"

cbuffer TransformConstants : register(b0)
{
	float4x4 skyProjectionMatrix;
	float4x4 sceneRotationMatrix;
};

struct VertexOutput {
	float4 position : SV_POSITION;
	float3 worldPosition : POSITION;
};

TextureCube envTexture : register(t0);
SamplerState defaultSampler : register(s0);

// Vertex shader
VertexOutput vertexMain(float3 position : ATTRIBUTE0)
{
    VertexOutput output;
    output.position = mul(skyProjectionMatrix, float4(position, 1.0f));
    output.worldPosition = position;
	return output;
}

// Fragment shader
float4 fragmentMain(in VertexOutput input) : SV_Target
{
	float3 envVector = normalize(input.worldPosition);
	return envTexture.SampleLevel(defaultSampler, envVector, 0);
}
