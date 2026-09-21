#pragma once

#include "UI.Common.slang"
#include "UI.Scissor.slang"

struct QuadDraw
{
    float4x4 TransformMat;
    float4x4 LayerMat;
    float4 Viewport;
    float4 QuadRect;
    float4 QuadColor;
    float2 BoxSize;
    int SoftwareScissorIndex;
};

struct PS_INPUT
{
    float4 vColor : COLOR0;
    float4 vTexCoord : TEXCOORD0;
    float4 vPositionSs : TEXCOORD1;
    float4 vPositionPanelSpace : TEXCOORD2;
    float4 vPositionPs : SV_Position;
};

PS_INPUT QuadVertex(QuadDraw quad, uint nVertexID)
{
    PS_INPUT o;

    float2 corner = QuadPositions[nVertexID];
    float2 vPositionSs = quad.QuadRect.xy + corner * quad.QuadRect.zw;
    float4 vViewport = quad.Viewport;

    float4 vMatrix = mul(quad.LayerMat, mul(quad.TransformMat, float4(vPositionSs, 0, 1)));
    o.vPositionPs.xy = 2.0 * (vMatrix.xy - vViewport.xy * vMatrix.w) / vViewport.zw - vMatrix.w;
    o.vPositionPs.y *= -1.0;
    o.vPositionPs.z = vMatrix.w;
    o.vPositionPs.w = vMatrix.w * (1.0 + EPSILON);

    o.vPositionSs = o.vPositionPs;
    o.vPositionPanelSpace = mul(quad.TransformMat, float4(vPositionSs, 0, 1));

    o.vColor.rgb = SrgbGammaToLinear(quad.QuadColor.rgb);
    o.vColor.a = quad.QuadColor.a;
    o.vTexCoord = float4(corner, 0, 0);

    return o;
}

static float g_flUIClipCoverage = 1.0;

void UI_CommonProcessing_Pre(int softwareScissorIndex, PS_INPUT i)
{
    if (softwareScissorIndex >= 0)
    {
        g_flUIClipCoverage = SoftwareScissorCoverage(softwareScissorIndex, i.vPositionPanelSpace.xy / i.vPositionPanelSpace.w);
    }
}

float4 UI_ApplyClip(float4 vColor, bool bPremultiplied = false)
{
    if (g_flUIClipCoverage <= 0.0)
        clip(-1);

    if (bPremultiplied)
        vColor *= g_flUIClipCoverage;
    else
        vColor.a *= g_flUIClipCoverage;

    return vColor;
}

float4 UIEncodeOutput(float4 vColor)
{
    vColor.rgb = SrgbLinearToGamma(vColor.rgb);
    return vColor;
}

float4 UI_CommonProcessing_Post(float4 vColor)
{
    vColor = UI_ApplyClip(vColor);
    vColor = UIEncodeOutput(vColor);
    return vColor;
}
