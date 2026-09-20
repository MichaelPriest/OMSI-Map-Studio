cbuffer ViewportCamera : register(b0)
{
    row_major float4x4 ViewProjection;
};

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(VSInput input)
{
    PSInput output;

    output.Position =
        mul(
            float4(
                input.Position,
                1.0f),
            ViewProjection);

    output.Color =
        input.Color;

    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
