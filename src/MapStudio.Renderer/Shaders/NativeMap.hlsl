cbuffer ViewportCamera : register(b0)
{
    row_major float4x4 ViewProjection;
};

Texture2D DiffuseTexture : register(t0);
Texture2D MaskTexture : register(t1);
Texture2D SecondaryTexture : register(t2);
Texture2D DetailTexture : register(t3);

SamplerState DiffuseSampler : register(s0);
SamplerState MaskSampler : register(s1);

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
    float2 DetailTexCoord : TEXCOORD2;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
    float2 DetailTexCoord : TEXCOORD2;
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

    output.DetailTexCoord =
        input.DetailTexCoord;

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

    return float4(
        sampled.rgb *
            input.Color.rgb,
        1.0f);
}

float4 PSAlphaCutout(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    clip(
        sampled.a -
        0.5f);

    return float4(
        sampled.rgb *
            input.Color.rgb,
        1.0f);
}

float4 PSAlphaBlend(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    return sampled *
        input.Color;
}

float3 ComposeNightPreview(
    float3 baseRgb,
    float3 secondaryRgb)
{
    return saturate(
        baseRgb *
            0.30f +
        secondaryRgb);
}

float4 PSNightMaterial(
    PSInput input) : SV_TARGET
{
    float4 baseColor =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float4 secondary =
        SecondaryTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    return float4(
        ComposeNightPreview(
            baseColor.rgb,
            secondary.rgb) *
            input.Color.rgb,
        1.0f);
}

float4 PSNightMaterialCutout(
    PSInput input) : SV_TARGET
{
    float4 baseColor =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float4 secondary =
        SecondaryTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    clip(
        baseColor.a -
        0.5f);

    return float4(
        ComposeNightPreview(
            baseColor.rgb,
            secondary.rgb) *
            input.Color.rgb,
        1.0f);
}

float4 PSNightMaterialBlend(
    PSInput input) : SV_TARGET
{
    float4 baseColor =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float4 secondary =
        SecondaryTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    return float4(
        ComposeNightPreview(
            baseColor.rgb,
            secondary.rgb) *
            input.Color.rgb,
        baseColor.a *
            input.Color.a);
}

float3 ComposeTerrainDetail(
    float3 baseRgb,
    float3 detailRgb)
{
    float3 detailModulation =
        lerp(
            float3(
                1.0f,
                1.0f,
                1.0f),
            saturate(
                detailRgb *
                2.0f),
            0.35f);

    return saturate(
        baseRgb *
        detailModulation);
}

float4 PSTerrainBaseDetail(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float4 detail =
        DetailTexture.Sample(
            DiffuseSampler,
            input.DetailTexCoord);

    return float4(
        ComposeTerrainDetail(
            sampled.rgb,
            detail.rgb) *
            input.Color.rgb,
        1.0f);
}

float4 PSTerrainLayerDetail(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float4 detail =
        DetailTexture.Sample(
            DiffuseSampler,
            input.DetailTexCoord);

    float mask =
        MaskTexture.Sample(
            MaskSampler,
            input.MaskTexCoord).a;

    return float4(
        ComposeTerrainDetail(
            sampled.rgb,
            detail.rgb) *
            input.Color.rgb,
        sampled.a *
            input.Color.a *
            mask);
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
