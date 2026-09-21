#include "UI.Common.slang"

struct DownsampleConstants
{
    int SourceTextureIndex;
};

[[vk::push_constant]] ConstantBuffer<DownsampleConstants> Downsample;

[shader("vertex")]
float4 VertexMain(uint vertexId : SV_VulkanVertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
    return float4(corner * 2.0 - 1.0, 0.0, 1.0);
}

[shader("fragment")]
float4 FragmentMain(float4 position : SV_Position) : SV_Target0
{
    Texture2D source = Textures[Downsample.SourceTextureIndex];
    SamplerState clampSampler = Samplers[SamplerTrilinearClamp];

    uint width, height, levels;
    source.GetDimensions(0, width, height, levels);
    float2 sourceSize = float2(width, height);
    float2 targetSize = max(floor(sourceSize * 0.5), 1.0);

    float2 uv = position.xy / targetSize;
    float2 tap = 0.75 / sourceSize;

    float4 sum = source.SampleLevel(clampSampler, uv + float2(-tap.x, -tap.y), 0);
    sum += source.SampleLevel(clampSampler, uv + float2(tap.x, -tap.y), 0);
    sum += source.SampleLevel(clampSampler, uv + float2(-tap.x, tap.y), 0);
    sum += source.SampleLevel(clampSampler, uv + float2(tap.x, tap.y), 0);
    return sum * 0.25;
}
