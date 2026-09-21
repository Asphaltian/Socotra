#include "UI.Common.slang"
#include "UI.Quad.slang"
#include "UI.Blur.slang"

struct DropShadowConstants
{
    QuadDraw Quad;
    int TextureIndex;
    float2 FilterDropShadowOffset;
    float FilterDropShadowBlur;
    float4 FilterDropShadowColor;
    float2 FilterDropShadowScale;
};

[[vk::binding(0, 2)]] ConstantBuffer<DropShadowConstants> Constants;

[shader("vertex")]
PS_INPUT VertexMain(uint nVertexID : SV_VulkanVertexID)
{
    return QuadVertex(Constants.Quad, nVertexID);
}

[shader("fragment")]
float4 FragmentMain(PS_INPUT i) : SV_Target0
{
    UI_CommonProcessing_Pre(Constants.Quad.SoftwareScissorIndex, i);

    Texture2D colorTexture = Textures[Constants.TextureIndex];
    float2 invTextureDim = 1.0 / TextureDimensions2D(colorTexture, 0);

    float2 texCoord = i.vTexCoord.xy;

    float2 scale = Constants.FilterDropShadowScale;

    texCoord = texCoord - (1.0f - scale) * 0.5f;
    texCoord = texCoord / scale;

    float2 vShadowUv = texCoord - Constants.FilterDropShadowOffset * invTextureDim;
    float4 vShadowColor = Constants.FilterDropShadowColor;
    vShadowColor.a *= GaussianBlurTexture(colorTexture, Samplers[SamplerTrilinearBorder], vShadowUv, Constants.FilterDropShadowBlur, invTextureDim).a;

    return UI_ApplyClip(vShadowColor);
}
