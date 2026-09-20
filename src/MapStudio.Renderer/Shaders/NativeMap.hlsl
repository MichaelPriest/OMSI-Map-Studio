cbuffer ViewportCamera : register(b0)
{
    row_major float4x4 ViewProjection;
};

Texture2D DiffuseTexture : register(t0);
Texture2D MaskTexture : register(t1);

SamplerState DiffuseSampler : register(s0);
SamplerState MaskSampler : register(s1);

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
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

    output.TexCoord =
        input.TexCoord;

    output.MaskTexCoord =
        input.MaskTexCoord;

    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}

float4 PSTextured(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    clip(
        sampled.a -
        0.05f);

    return sampled *
        input.Color;
}

float4 PSTerrainLayer(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float mask =
        MaskTexture.Sample(
            MaskSampler,
            input.MaskTexCoord).a;

    return float4(
        sampled.rgb *
            input.Color.rgb,
        sampled.a *
            input.Color.a *
            mask);
}
