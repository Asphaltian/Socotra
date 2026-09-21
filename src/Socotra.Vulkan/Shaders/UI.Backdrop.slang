#include "UI.Common.slang"
#include "UI.Quad.slang"
#include "UI.Scissor.slang"
#include "UI.RoundedRect.slang"
#include "UI.ColorFilter.slang"

struct BackdropConstants
{
    QuadDraw Quad;
    int PainterScissorIndex;
    float4 CornerRadius;
    float4 CornerRadiusV;
    float Brightness;
    float Contrast;
    float Saturate;
    float Invert;
    float HueRotate;
    float Sepia;
    float BlurScale;
    int FrameBufferCopyTextureIndex;
};

[[vk::binding(0, 2)]] ConstantBuffer<BackdropConstants> Constants;

[shader("vertex")]
PS_INPUT VertexMain(uint nVertexID : SV_VulkanVertexID)
{
    return QuadVertex(Constants.Quad, nVertexID);
}

float4 DoBackdropFilter(float2 uv)
{
    float3 backdrop = Textures[Constants.FrameBufferCopyTextureIndex].SampleLevel(Samplers[SamplerTrilinearClamp], uv, sqrt(Constants.BlurScale / 2)).rgb;

    backdrop = DoColorMatrix(backdrop, SepiaMatrix(Constants.Sepia));

    backdrop = lerp(backdrop, 1 - backdrop, Constants.Invert);

    backdrop = saturate(lerp(float3(0.5, 0.5, 0.5), backdrop, Constants.Contrast));

    backdrop = saturate(DoHueRotate(backdrop, Constants.HueRotate));
    backdrop = saturate(DoSaturate(backdrop, Constants.Saturate));
    backdrop *= Constants.Brightness;
    backdrop = SrgbGammaToLinear(backdrop);

    return float4(backdrop, 1);
}

[shader("fragment")]
float4 FragmentMain(PS_INPUT i) : SV_Target0
{
    UI_CommonProcessing_Pre(Constants.Quad.SoftwareScissorIndex, i);

    float2 vNdc = i.vPositionSs.xy / i.vPositionSs.w;
    float2 vUV = float2(vNdc.x, -vNdc.y) * 0.5 + 0.5;

    float4 vColor = DoBackdropFilter(vUV);

    float2 boxSize = Constants.Quad.BoxSize;
    float2 boxUv = (i.vTexCoord.xy - 0.5) * (boxSize + 2.0) / max(boxSize, 0.0001) + 0.5;
    float edge = SdfCoverage(RoundedRectSdfUv(boxUv, boxSize, Constants.CornerRadius, Constants.CornerRadiusV));
    edge *= ScissorCoverage(Constants.PainterScissorIndex, i.vPositionPanelSpace.xy / i.vPositionPanelSpace.w);
    vColor.rgb *= i.vColor.rgb;
    vColor.a = edge * i.vColor.a;

    return UI_CommonProcessing_Post(vColor);
}
