#include "UI.Common.slang"
#include "UI.Text.slang"

struct TextRasterConstants
{
    int InstanceOffset;
    int TileOffset;
    int TilesX;
    int Width;
    int Height;
    int MipLevel;
    float3 BaseColor;
};

[[vk::push_constant]] ConstantBuffer<TextRasterConstants> Raster;

[[vk::binding(0, 2)]] [format("rgba8")] RWTexture2D<float4> TextOutput;

[shader("compute")]
[numthreads(8, 8, 1)]
void MainCs(uint3 id : SV_DispatchThreadID)
{
    int2 size = int2(Raster.Width, Raster.Height);
    if (any(int2(id.xy) >= max(size >> Raster.MipLevel, 1)))
        return;

    float ps = (float)(1 << Raster.MipLevel);
    float2 p = (float2(id.xy) + 0.5) * ps;

    TextOutput[id.xy] = TextComposite(p, ps, Raster.InstanceOffset, Raster.TileOffset, Raster.TilesX, size, Raster.BaseColor);
}
