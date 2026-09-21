#include "UI.Common.slang"
#include "UI.Quad.slang"
#include "UI.Scissor.slang"
#include "UI.Blur.slang"
#include "UI.ColorFilter.slang"

[SpecializationConstant] const int BlendMode = 0;

struct FilterConstants
{
    QuadDraw Quad;
    int PainterScissorIndex;
    int TextureIndex;
    float FilterBrightness;
    float FilterHueRotate;
    float FilterBlur;
    float FilterSaturate;
    float FilterSepia;
    float FilterInvert;
    float FilterContrast;
    float4 FilterTint;
    int MaskTextureIndex;
    int MaskMode;
    int MaskScope;
    float MaskAngle;
    int SamplerIndex;
    int BorderSamplerIndex;
    float4 MaskPos;
};

[[vk::binding(0, 2)]] ConstantBuffer<FilterConstants> Constants;

[shader("vertex")]
PS_INPUT VertexMain(uint nVertexID : SV_VulkanVertexID)
{
    return QuadVertex(Constants.Quad, nVertexID);
}

float4 ApplyColorFilters(float4 vColor)
{
    vColor.rgb = saturate((vColor.rgb - 0.5f) * Constants.FilterContrast + 0.5f);

    vColor = DoColorMatrix(vColor, SepiaMatrix(Constants.FilterSepia));

    vColor.rgb = lerp(vColor.rgb, 1.0f - vColor.rgb, Constants.FilterInvert);

    vColor.rgb = saturate(DoHueRotate(vColor.rgb, Constants.FilterHueRotate));
    vColor.rgb = saturate(DoSaturate(vColor.rgb, Constants.FilterSaturate));

    vColor.rgb = saturate(vColor.rgb * Constants.FilterBrightness);

    vColor.rgb = SrgbGammaToLinear(vColor.rgb);

    return vColor * Constants.FilterTint;
}

float4 SampleLayer(float2 uv, SamplerState layerSampler, float blur, float2 invTextureDim)
{
    float4 color = GaussianBlurTexture(Textures[Constants.TextureIndex], layerSampler, uv, blur, invTextureDim);
    color.rgb = color.a > 0.00001 ? color.rgb / color.a : 0;
    return color;
}

[shader("fragment")]
float4 FragmentMain(PS_INPUT i) : SV_Target0
{
    float2 invTextureDim = 1.0 / TextureDimensions2D(Textures[Constants.TextureIndex], 0);

    float2 uv = i.vTexCoord.xy;
    float2 uvAdjust = Constants.FilterBlur * 3.0 * invTextureDim;
    uv = lerp(-uvAdjust, 1.0 + uvAdjust, uv);

    UI_CommonProcessing_Pre(Constants.Quad.SoftwareScissorIndex, i);
    g_flUIClipCoverage *= ScissorCoverage(Constants.PainterScissorIndex, i.vPositionPanelSpace.xy / i.vPositionPanelSpace.w);

    float4 vColor = SampleLayer(uv, Samplers[SamplerTrilinearBorder], Constants.FilterBlur, invTextureDim);
    vColor = ApplyColorFilters(vColor);

    if (Constants.MaskTextureIndex != 0)
    {
        float2 maskSize = Constants.MaskPos.zw;
        float2 vOffset = Constants.MaskPos.xy / maskSize;

        float2 vUV = -vOffset + (uv * (Constants.Quad.BoxSize / maskSize));
        vUV = RotateTexCoord(vUV, Constants.MaskAngle);

        float4 vMask = Textures[Constants.MaskTextureIndex].Sample(GetSampler(Constants.SamplerIndex), vUV);

        float mask = 1.0f;
        if (Constants.MaskMode == 0 || Constants.MaskMode == 2)
        {
            mask = GetLuminance(SrgbGammaToLinear(vMask.xyz));
        }
        else if (Constants.MaskMode == 1)
        {
            mask = vMask.a;
        }

        if (Constants.MaskScope == 0)
        {
            vColor.a *= mask;
        }
        else if (Constants.MaskScope == 1)
        {
            float4 origColor = SampleLayer(uv, GetSampler(Constants.BorderSamplerIndex), 0, invTextureDim);
            origColor.rgb = SrgbGammaToLinear(origColor.rgb);
            vColor = lerp(origColor, vColor, mask);
        }
    }

    vColor = UI_CommonProcessing_Post(vColor);
    if (BlendMode == 3) vColor.rgb *= vColor.a;
    return vColor;
}
