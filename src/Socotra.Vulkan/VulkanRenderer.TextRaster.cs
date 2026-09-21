using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

public sealed unsafe partial class VulkanRenderer
{
    private const ImageUsageFlags RasterUsage = ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit;

    private readonly List<(Texture Texture, GpuImage Image, int InstanceOffset, int TileOffset)> _rasters = [];
    private readonly List<TextInstance> _rasterInstances = [];
    private readonly List<uint> _rasterTiles = [];
    private DescriptorSetLayout _rasterOutputLayout;
    private PipelineLayout _rasterPipelineLayout;
    private void CreateTextRaster()
    {
        _rasterOutputLayout = CreateLayout([new DescriptorSetLayoutBinding(0, DescriptorType.StorageImage, 1, ShaderStageFlags.ComputeBit)], []);
        var layouts = stackalloc DescriptorSetLayout[] { _tableLayout, _textureLayout, _rasterOutputLayout };
        var pushConstants = new PushConstantRange(ShaderStageFlags.ComputeBit, 0, (uint)sizeof(TextRasterConstants));
        var info = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 3,
            PSetLayouts = layouts,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstants,
        };
        VulkanDevice.Check(_vulkan.Api.CreatePipelineLayout(_vulkan.Device, in info, null, out _rasterPipelineLayout), "vkCreatePipelineLayout");
    }

    private void DestroyTextRaster()
    {
        _vulkan.Api.DestroyPipelineLayout(_vulkan.Device, _rasterPipelineLayout, null);
        _vulkan.Api.DestroyDescriptorSetLayout(_vulkan.Device, _rasterOutputLayout, null);
    }

    private void AddRaster(Texture texture, GpuImage image)
    {
        var raster = texture.Raster!;
        var tileOffset = _rasterTiles.Count;
        var header = (raster.TilesX * ((texture.Height + GpuFontText.TileSize - 1) / GpuFontText.TileSize)) + 1;
        for (int i = 0; i < raster.Tiles.Length; i++)
        {
            _rasterTiles.Add(i < header ? raster.Tiles[i] + (uint)tileOffset : raster.Tiles[i]);
        }

        _rasters.Add((texture, image, _rasterInstances.Count, tileOffset));
        _rasterInstances.AddRange(raster.Instances);
    }

    private void RasterTextures(CommandBuffer commandBuffer, FramePass pass, int textCount)
    {
        if (_rasters.Count == 0)
        {
            return;
        }

        var api = _vulkan.Api;
        pass.ResetRasterSets((uint)_rasters.Sum(raster => (int)raster.Image.MipLevels));
        api.CmdBindPipeline(commandBuffer, PipelineBindPoint.Compute, _pipelines.TextRaster);
        var tableSet = pass.TableSet;
        api.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _rasterPipelineLayout, 0, 1, &tableSet, 0, null);

        foreach (var (texture, image, instanceOffset, tileOffset) in _rasters)
        {
            _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.Undefined, ImageLayout.General,
                PipelineStageFlags.FragmentShaderBit, 0, PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit, 0, image.MipLevels);

            var raster = texture.Raster!;
            for (int mip = 0; mip < image.MipLevels; mip++)
            {
                var outputSet = pass.AllocateRasterSet(_rasterOutputLayout, image.MipView(mip));
                api.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Compute, _rasterPipelineLayout, 2, 1, &outputSet, 0, null);
                var constants = new TextRasterConstants
                {
                    InstanceOffset = textCount + instanceOffset,
                    TileOffset = tileOffset,
                    TilesX = raster.TilesX,
                    Width = texture.Width,
                    Height = texture.Height,
                    MipLevel = mip,
                    BaseColor = raster.BaseColor,
                };
                api.CmdPushConstants(commandBuffer, _rasterPipelineLayout, ShaderStageFlags.ComputeBit, 0, (uint)sizeof(TextRasterConstants), &constants);
                var size = image.MipSize(mip);
                api.CmdDispatch(commandBuffer, (size.Width + 7) / 8, (size.Height + 7) / 8, 1);
            }

            _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.General, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.ComputeShaderBit, AccessFlags.ShaderWriteBit, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit, 0, image.MipLevels);
            _textures[texture] = (image, texture.DirtyVersion);
        }
    }
}
