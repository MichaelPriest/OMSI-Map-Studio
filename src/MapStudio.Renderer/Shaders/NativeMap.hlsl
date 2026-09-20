cbuffer ViewportTransform : register(b0)
{
    float4 ViewTransform;
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
    float2 transformed =
        input.Position.xy *
        ViewTransform.z +
        ViewTransform.xy;

    output.Position =
        float4(
            transformed,
            input.Position.z,
            1.0f);
    output.Color = input.Color;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
