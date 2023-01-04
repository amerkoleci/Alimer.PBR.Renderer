// Physically Based Rendering
// Copyright (c) 2017-2018 Michał Siejak

// Tone-mapping & gamma correction.

static const float gamma     = 2.2;
static const float exposure  = 1.0;
static const float pureWhite = 1.0;

struct VertexOutput {
	float4 position : SV_POSITION;
	float2 texcoord : TEXCOORD;
};

Texture2D sceneColor: register(t0);
SamplerState defaultSampler : register(s0);

VertexOutput vertexMain(in uint vertexID : SV_VertexID)
{
    VertexOutput output;

	if(vertexID == 0) {
        output.position = float4(-1.0f, 1.0f, 1.0f, 1.0f);
        output.texcoord = float2(0.0f, 0.0f);
	}
	else if(vertexID == 1) {
        output.position = float4(3.0f, 1.0f, 1.0f, 1.0f);
        output.texcoord = float2(2.0f, 0.0f);
	}
	else /* if(vertexID == 2) */ {
        output.position = float4(-1.0f, -3.0f, 1.0f, 1.0f);
        output.texcoord = float2(0.0f, 2.0f);
	}

	return output;
}

float4 fragmentMain(in VertexOutput input) : SV_Target
{
	float3 color = sceneColor.Sample(defaultSampler, input.texcoord).rgb * exposure;
	
	// Reinhard tonemapping operator.
	// see: "Photographic Tone Reproduction for Digital Images", eq. 4
	float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
	float mappedLuminance = (luminance * (1.0 + luminance/(pureWhite*pureWhite))) / (1.0 + luminance);

	// Scale color by ratio of average luminances.
	float3 mappedColor = (mappedLuminance / luminance) * color;

	// Gamma correction.
	return float4(pow(mappedColor, 1.0/gamma), 1.0);
}
