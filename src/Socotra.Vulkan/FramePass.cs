using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

internal sealed unsafe class FramePass : IDisposable
{
    public const int TableCount = 8;

    public const int BindingCount = TableCount + 4;

    public static readonly uint ConstantsRange = (uint)Math.Max(
        Math.Max(sizeof(FilterConstants), sizeof(BackdropConstants)),
        Math.Max(sizeof(DropShadowConstants), sizeof(BorderWrapConstants)));

    private readonly VulkanDevice _vulkan;
    private readonly DescriptorPool _pool;
    private readonly HostBuffer[] _tables;
    private HostBuffer _constants;
    private HostBuffer _staging;
    private HostBuffer _tiles;
    private DescriptorPool _rasterPool;
    private uint _rasterPoolSize;

    public FramePass(VulkanDevice vulkan, DescriptorSetLayout tableLayout, DescriptorSetLayout textureLayout, DescriptorSetLayout constantLayout,
        uint textureCapacity, uint samplerCount)
    {
        _vulkan = vulkan;
        var sizes = stackalloc DescriptorPoolSize[]
        {
            new(DescriptorType.StorageBuffer, BindingCount),
            new(DescriptorType.SampledImage, textureCapacity),
            new(DescriptorType.Sampler, samplerCount),
            new(DescriptorType.UniformBufferDynamic, 1),
        };
        var poolInfo = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = 3, PoolSizeCount = 4, PPoolSizes = sizes };
        VulkanDevice.Check(vulkan.Api.CreateDescriptorPool(vulkan.Device, in poolInfo, null, out _pool), "vkCreateDescriptorPool");

        var layouts = stackalloc DescriptorSetLayout[] { tableLayout, textureLayout, constantLayout };
        var sets = stackalloc DescriptorSet[3];
        var info = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _pool, DescriptorSetCount = 3, PSetLayouts = layouts };
        VulkanDevice.Check(vulkan.Api.AllocateDescriptorSets(vulkan.Device, in info, sets), "vkAllocateDescriptorSets");
        TableSet = sets[0];
        TextureSet = sets[1];
        ConstantSet = sets[2];

        _tables = [.. Enumerable.Range(0, TableCount).Select(_ => new HostBuffer(vulkan, HostBuffer.MinimumSize, BufferUsageFlags.StorageBufferBit))];
        _constants = new HostBuffer(vulkan, ConstantsRange, BufferUsageFlags.UniformBufferBit);
        _staging = new HostBuffer(vulkan, HostBuffer.MinimumSize, BufferUsageFlags.TransferSrcBit);
        _tiles = new HostBuffer(vulkan, HostBuffer.MinimumSize, BufferUsageFlags.StorageBufferBit);
        BindConstants();
    }

    public DescriptorSet TableSet { get; }

    public DescriptorSet TextureSet { get; }

    public DescriptorSet ConstantSet { get; }

    public HostBuffer Constants => _constants;

    public HostBuffer Staging => _staging;

    public void Upload(PainterBatcher batcher, ReadOnlySpan<HostBuffer> glyphs, ReadOnlySpan<TextInstance> rasterInstances, ReadOnlySpan<uint> rasterTiles)
    {
        Write(0, batcher.Boxes.Span);
        Write(1, batcher.Texts.Span, rasterInstances);
        Write(2, batcher.Scissors.Span);
        Write(3, batcher.Transforms.Span);
        Write(4, batcher.Gradients.Span);
        Write(5, batcher.Shapes.Span);
        Write(6, batcher.Paths.Span);
        Write(7, batcher.PathNodes.Span);
        HostBuffer.Ensure(ref _tiles, (ulong)(rasterTiles.Length * sizeof(uint)), BufferUsageFlags.StorageBufferBit);
        _tiles.Write(rasterTiles, 0);

        var infos = stackalloc DescriptorBufferInfo[BindingCount];
        var writes = stackalloc WriteDescriptorSet[BindingCount];
        for (int i = 0; i < BindingCount; i++)
        {
            var buffer = i < TableCount ? _tables[i] : i < BindingCount - 1 ? glyphs[i - TableCount] : _tiles;
            infos[i] = new DescriptorBufferInfo(buffer.Buffer, 0, Vk.WholeSize);
            writes[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = TableSet,
                DstBinding = (uint)i,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infos[i],
            };
        }

        _vulkan.Api.UpdateDescriptorSets(_vulkan.Device, BindingCount, writes, 0, null);
    }

    public void BindTextures(ReadOnlySpan<ImageView> views)
    {
        if (views.Length <= 1)
        {
            return;
        }

        var infos = new DescriptorImageInfo[views.Length - 1];
        for (int i = 1; i < views.Length; i++)
        {
            infos[i - 1] = new DescriptorImageInfo(default, views[i], ImageLayout.ShaderReadOnlyOptimal);
        }

        fixed (DescriptorImageInfo* pointer = infos)
        {
            var write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = TextureSet,
                DstBinding = 0,
                DstArrayElement = 1,
                DescriptorCount = (uint)infos.Length,
                DescriptorType = DescriptorType.SampledImage,
                PImageInfo = pointer,
            };
            _vulkan.Api.UpdateDescriptorSets(_vulkan.Device, 1, &write, 0, null);
        }
    }

    public void ReserveConstants(ulong size)
    {
        if (size > _constants.Size)
        {
            HostBuffer.Ensure(ref _constants, size, BufferUsageFlags.UniformBufferBit);
            BindConstants();
        }
    }

    public void ReserveStaging(ulong size) => HostBuffer.Ensure(ref _staging, size, BufferUsageFlags.TransferSrcBit);

    public void ResetRasterSets(uint sets)
    {
        var api = _vulkan.Api;
        if (_rasterPool.Handle != 0 && _rasterPoolSize >= sets)
        {
            VulkanDevice.Check(api.ResetDescriptorPool(_vulkan.Device, _rasterPool, 0), "vkResetDescriptorPool");
            return;
        }

        if (_rasterPool.Handle != 0)
        {
            api.DestroyDescriptorPool(_vulkan.Device, _rasterPool, null);
        }

        _rasterPoolSize = Math.Max(sets, _rasterPoolSize * 2);
        var size = new DescriptorPoolSize(DescriptorType.StorageImage, _rasterPoolSize);
        var info = new DescriptorPoolCreateInfo { SType = StructureType.DescriptorPoolCreateInfo, MaxSets = _rasterPoolSize, PoolSizeCount = 1, PPoolSizes = &size };
        VulkanDevice.Check(api.CreateDescriptorPool(_vulkan.Device, in info, null, out _rasterPool), "vkCreateDescriptorPool");
    }

    public DescriptorSet AllocateRasterSet(DescriptorSetLayout layout, ImageView view)
    {
        var allocate = new DescriptorSetAllocateInfo { SType = StructureType.DescriptorSetAllocateInfo, DescriptorPool = _rasterPool, DescriptorSetCount = 1, PSetLayouts = &layout };
        VulkanDevice.Check(_vulkan.Api.AllocateDescriptorSets(_vulkan.Device, in allocate, out var set), "vkAllocateDescriptorSets");
        var image = new DescriptorImageInfo(default, view, ImageLayout.General);
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.StorageImage,
            PImageInfo = &image,
        };
        _vulkan.Api.UpdateDescriptorSets(_vulkan.Device, 1, &write, 0, null);
        return set;
    }

    public void Dispose()
    {
        foreach (var table in _tables)
        {
            table.Dispose();
        }

        _constants.Dispose();
        _staging.Dispose();
        _tiles.Dispose();
        if (_rasterPool.Handle != 0)
        {
            _vulkan.Api.DestroyDescriptorPool(_vulkan.Device, _rasterPool, null);
        }

        _vulkan.Api.DestroyDescriptorPool(_vulkan.Device, _pool, null);
    }

    private void Write<T>(int table, ReadOnlySpan<T> data, ReadOnlySpan<T> more = default)
        where T : unmanaged
    {
        HostBuffer.Ensure(ref _tables[table], (ulong)((data.Length + more.Length) * sizeof(T)), BufferUsageFlags.StorageBufferBit);
        _tables[table].Write(data, 0);
        _tables[table].Write(more, (ulong)(data.Length * sizeof(T)));
    }

    private void BindConstants()
    {
        var info = new DescriptorBufferInfo(_constants.Buffer, 0, ConstantsRange);
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = ConstantSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBufferDynamic,
            PBufferInfo = &info,
        };
        _vulkan.Api.UpdateDescriptorSets(_vulkan.Device, 1, &write, 0, null);
    }
}
