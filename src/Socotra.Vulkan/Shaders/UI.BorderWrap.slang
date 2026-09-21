#include "UI.Common.slang"
#include "UI.Quad.slang"

static const float M_PI = 3.14159265358979323846;

struct BorderWrapConstants
{
    QuadDraw Quad;
    int TextureIndex;
    float4 FilterBorderWrapColor;
    float2 FilterBorderWrapColorScale;
    float FilterBorderWrapWidth;
};

[[vk::binding(0, 2)]] ConstantBuffer<BorderWrapConstants> Constants;

[shader("vertex")]
PS_INPUT VertexMain(uint nVertexID : SV_VulkanVertexID)
{
    return QuadVertex(Constants.Quad, nVertexID);
}

float SampleAlpha(float2 uv)
{
    return Textures[Constants.TextureIndex].Sample(Samplers[SamplerTrilinearBorder], uv).a;
}

float GetBorder(float2 uv)
{
    float2 pixelSize = 1.0 / Constants.Quad.BoxSize;
    pixelSize *= Constants.FilterBorderWrapWidth;

    float Pi = M_PI * 2;
    float Directions = 32.0;

    float alpha = 0.0f;

    for (float d = 0.0; d < Pi; d += Pi / Directions)
    {
        float2 off = float2(cos(d), sin(d)) * pixelSize;
        alpha += SampleAlpha(uv + off);
    }

    alpha /= M_PI;

    return alpha;
}

[shader("fragment")]
float4 FragmentMain(PS_INPUT i) : SV_Target0
{
    UI_CommonProcessing_Pre(Constants.Quad.SoftwareScissorIndex, i);

    float2 texCoord = i.vTexCoord.xy;

    if (Constants.FilterBorderWrapWidth > 0)
    {
        float2 scale = Constants.FilterBorderWrapColorScale;

        texCoord = texCoord - (1.0f - scale) * 0.5f;
        texCoord = texCoord / scale;
    }

    float edge = GetBorder(texCoord);

    float4 color = float4(SrgbGammaToLinear(Constants.FilterBorderWrapColor.rgb), Constants.FilterBorderWrapColor.a);
    float4 colorTransparent = color;
    colorTransparent.a = 0;
    float4 vColor = lerp(colorTransparent, color, edge);

    vColor.a = saturate(vColor.a * 2.0f - 1.0f);

    return UI_CommonProcessing_Post(vColor);
}
