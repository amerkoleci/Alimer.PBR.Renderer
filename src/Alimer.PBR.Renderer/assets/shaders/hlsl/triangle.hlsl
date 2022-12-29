struct VertexInput {
    float3 Position : ATTRIBUTE0;
    float4 Color : ATTRIBUTE1;
};

struct VertexOutput {
	float4 Position : SV_POSITION;
	float4 Color : COLOR;
};

// Vertex shader
VertexOutput main_vs(in VertexInput input)
{
    VertexOutput output;
    output.Position = float4(input.Position, 1.0f);
    output.Color = input.Color;
    return output;
}

// Pixel shader
float4 main_ps(in VertexOutput input) : SV_Target
{
    return input.Color;
}
