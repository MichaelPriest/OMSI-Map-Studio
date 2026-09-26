cbuffer ViewportCamera : register(b0)
{
    row_major float4x4 ViewProjection;
    float3 CameraPosition;
    float ViewportPadding;
};

cbuffer MaterialPreview : register(b1)
{
    float BumpStrength;
    float EnvironmentStrength;
    float HasBumpTexture;
    float HasEnvironmentTexture;
};

Texture2D DiffuseTexture : register(t0);
Texture2D MaskTexture : register(t1);
Texture2D SecondaryTexture : register(t2);
Texture2D DetailTexture : register(t3);
Texture2D BumpTexture : register(t4);
Texture2D EnvironmentTexture : register(t5);

SamplerState DiffuseSampler : register(s0);
SamplerState MaskSampler : register(s1);

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
    float2 DetailTexCoord : TEXCOORD2;
    float3 Normal : NORMAL;
    float4 Tangent : TANGENT;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 TexCoord : TEXCOORD0;
    float2 MaskTexCoord : TEXCOORD1;
    float2 DetailTexCoord : TEXCOORD2;
    float3 WorldPosition : TEXCOORD3;
    float3 Normal : TEXCOORD4;
    float4 Tangent : TEXCOORD5;
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

    output.WorldPosition =
        input.Position;

    output.Normal =
        normalize(
            input.Normal);

    output.Tangent =
        input.Tangent;

    return output;
}

float3 BuildFallbackTangent(
    float3 normal)
{
    float3 axis =
        abs(normal.y) < 0.95f
            ? float3(0.0f, 1.0f, 0.0f)
            : float3(1.0f, 0.0f, 0.0f);

    return normalize(
        cross(
            axis,
            normal));
}

float3 ResolveSurfaceNormal(
    PSInput input)
{
    float3 normal =
        normalize(
            input.Normal);

    if (HasBumpTexture < 0.5f)
    {
        return normal;
    }

    float3 tangent =
        input.Tangent.xyz;

    tangent -=
        normal *
        dot(
            normal,
            tangent);

    if (
        dot(
            tangent,
            tangent) <
        0.00001f)
    {
        tangent =
            BuildFallbackTangent(
                normal);
    }
    else
    {
        tangent =
            normalize(
                tangent);
    }

    float tangentSign =
        input.Tangent.w < 0.0f
            ? -1.0f
            : 1.0f;

    float3 bitangent =
        normalize(
            cross(
                normal,
                tangent)) *
        tangentSign;

    float3 mapped =
        BumpTexture.Sample(
            DiffuseSampler,
            input.TexCoord).xyz *
        2.0f -
        1.0f;

    float bumpWeight =
        saturate(
            abs(
                BumpStrength) *
            8.0f);

    mapped.xy *=
        bumpWeight;

    mapped.z =
        max(
            0.15f,
            abs(mapped.z));

    float3 perturbed =
        normalize(
            tangent *
                mapped.x +
            bitangent *
                mapped.y +
            normal *
                mapped.z);

    return normalize(
        lerp(
            normal,
            perturbed,
            bumpWeight));
}

float2 EnvironmentUv(
    float3 direction)
{
    const float Pi =
        3.14159265359f;

    direction =
        normalize(
            direction);

    return float2(
        atan2(
            direction.z,
            direction.x) /
            (2.0f * Pi) +
            0.5f,
        asin(
            clamp(
                direction.y,
                -1.0f,
                1.0f)) /
            Pi +
            0.5f);
}

float3 ApplyAdvancedMaterial(
    PSInput input,
    float3 baseRgb)
{
    if (
        HasBumpTexture < 0.5f &&
        HasEnvironmentTexture < 0.5f)
    {
        return baseRgb;
    }

    float3 normal =
        ResolveSurfaceNormal(
            input);

    float3 result =
        baseRgb;

    if (HasBumpTexture >= 0.5f)
    {
        float3 lightDirection =
            normalize(
                float3(
                    0.35f,
                    0.82f,
                    -0.26f));

        float lighting =
            0.55f +
            0.45f *
            abs(
                dot(
                    normal,
                    lightDirection));

        float bumpWeight =
            saturate(
                abs(
                    BumpStrength) *
                8.0f);

        result *=
            lerp(
                1.0f,
                lighting,
                bumpWeight);
    }

    if (HasEnvironmentTexture >= 0.5f)
    {
        float3 viewDirection =
            normalize(
                input.WorldPosition -
                CameraPosition);

        float3 reflected =
            reflect(
                viewDirection,
                normal);

        float3 environment =
            EnvironmentTexture.Sample(
                DiffuseSampler,
                EnvironmentUv(
                    reflected)).rgb;

        result =
            lerp(
                result,
                environment,
                saturate(
                    EnvironmentStrength));
    }

    return saturate(
        result);
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
        ApplyAdvancedMaterial(
            input,
            sampled.rgb *
                input.Color.rgb),
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
        ApplyAdvancedMaterial(
            input,
            sampled.rgb *
                input.Color.rgb),
        1.0f);
}

float4 PSAlphaBlend(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    return float4(
        ApplyAdvancedMaterial(
            input,
            sampled.rgb *
                input.Color.rgb),
        sampled.a *
            input.Color.a);
}
float ResolveTransMapAlpha(
    PSInput input)
{
    float4 trans =
        MaskTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float luminance =
        dot(
            trans.rgb,
            float3(
                0.333333f,
                0.333333f,
                0.333333f));

    return min(
        trans.a,
        luminance);
}

float4 PSAlphaCutoutTransMap(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    float alpha =
        ResolveTransMapAlpha(
            input);

    clip(
        alpha -
        0.5f);

    return float4(
        ApplyAdvancedMaterial(
            input,
            sampled.rgb *
                input.Color.rgb),
        1.0f);
}

float4 PSAlphaBlendTransMap(
    PSInput input) : SV_TARGET
{
    float4 sampled =
        DiffuseTexture.Sample(
            DiffuseSampler,
            input.TexCoord);

    return float4(
        ApplyAdvancedMaterial(
            input,
            sampled.rgb *
                input.Color.rgb),
        ResolveTransMapAlpha(
            input) *
            input.Color.a);
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
        ApplyAdvancedMaterial(
            input,
            ComposeNightPreview(
                baseColor.rgb,
                secondary.rgb) *
                input.Color.rgb),
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
        ApplyAdvancedMaterial(
            input,
            ComposeNightPreview(
                baseColor.rgb,
                secondary.rgb) *
                input.Color.rgb),
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
        ApplyAdvancedMaterial(
            input,
            ComposeNightPreview(
                baseColor.rgb,
                secondary.rgb) *
                input.Color.rgb),
        baseColor.a *
            input.Color.a);
}
float4 PSNightMaterialCutoutTransMap(
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

    float alpha =
        ResolveTransMapAlpha(
            input);

    clip(
        alpha -
        0.5f);

    return float4(
        ApplyAdvancedMaterial(
            input,
            ComposeNightPreview(
                baseColor.rgb,
                secondary.rgb) *
                input.Color.rgb),
        1.0f);
}

float4 PSNightMaterialBlendTransMap(
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
        ApplyAdvancedMaterial(
            input,
            ComposeNightPreview(
                baseColor.rgb,
                secondary.rgb) *
                input.Color.rgb),
        ResolveTransMapAlpha(
            input) *
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
