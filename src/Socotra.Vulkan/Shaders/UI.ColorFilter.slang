#pragma once

float4 DoColorMatrix(float4 color, float4x4 mColorMatrix)
{
    return saturate(mul(mColorMatrix, color));
}

float3 DoColorMatrix(float3 color, float4x4 mColorMatrix)
{
    return mul(mColorMatrix, float4(color, 1.0f)).rgb;
}

float GetLuminance(float3 vColor)
{
    return dot(vColor, float3(0.2126729f, 0.7151522f, 0.0721750f));
}

float3 DoSaturate(float3 vColor, float s)
{
    float3x3 m = float3x3(
        0.213f + 0.787f * s, 0.715f - 0.715f * s, 0.072f - 0.072f * s,
        0.213f - 0.213f * s, 0.715f + 0.285f * s, 0.072f - 0.072f * s,
        0.213f - 0.213f * s, 0.715f - 0.715f * s, 0.072f + 0.928f * s);

    return mul(m, vColor);
}

float3 DoHueRotate(float3 vColor, float flDegrees)
{
    float c = cos(radians(flDegrees));
    float s = sin(radians(flDegrees));

    float3x3 m = float3x3(
        0.213f + c * 0.787f - s * 0.213f, 0.715f - c * 0.715f - s * 0.715f, 0.072f - c * 0.072f + s * 0.928f,
        0.213f - c * 0.213f + s * 0.143f, 0.715f + c * 0.285f + s * 0.140f, 0.072f - c * 0.072f - s * 0.283f,
        0.213f - c * 0.213f - s * 0.787f, 0.715f - c * 0.715f + s * 0.715f, 0.072f + c * 0.928f + s * 0.072f);

    return mul(m, vColor);
}

float4x4 SepiaMatrix(float sepia)
{
    return float4x4(
        0.393f + 0.607f * (1.0f - sepia), 0.769f - 0.769f * (1.0f - sepia), 0.189f - 0.189f * (1.0f - sepia), 0.0f,
        0.349f - 0.349f * (1.0f - sepia), 0.686f + 0.314f * (1.0f - sepia), 0.168f - 0.168f * (1.0f - sepia), 0.0f,
        0.272f - 0.272f * (1.0f - sepia), 0.534f - 0.534f * (1.0f - sepia), 0.131f + 0.869f * (1.0f - sepia), 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);
}
