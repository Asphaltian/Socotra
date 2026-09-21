using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Socotra.Vulkan;

internal sealed unsafe class HostBuffer : IDisposable
{
    public const ulong MinimumSize = 256;

    private readonly VulkanDevice _vulkan;
    private readonly DeviceMemory _memory;
    private readonly byte* _mapped;

    public HostBuffer(VulkanDevice vulkan, ulong size, BufferUsageFlags usage)
    {
        _vulkan = vulkan;
        Size = Math.Max(BitOperations.RoundUpToPowerOf2(size), MinimumSize);
        var api = vulkan.Api;
        var info = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = Size, Usage = usage, SharingMode = SharingMode.Exclusive };
        VulkanDevice.Check(api.CreateBuffer(vulkan.Device, in info, null, out var buffer), "vkCreateBuffer");
        Buffer = buffer;
        api.GetBufferMemoryRequirements(vulkan.Device, buffer, out var requirements);
        _memory = vulkan.Allocate(requirements, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        VulkanDevice.Check(api.BindBufferMemory(vulkan.Device, buffer, _memory, 0), "vkBindBufferMemory");
        void* mapped;
        VulkanDevice.Check(api.MapMemory(vulkan.Device, _memory, 0, Size, 0, &mapped), "vkMapMemory");
        _mapped = (byte*)mapped;
    }

    public Buffer Buffer { get; }

    public ulong Size { get; }

    public static void Ensure(ref HostBuffer buffer, ulong size, BufferUsageFlags usage)
    {
        if (size > buffer.Size)
        {
            var vulkan = buffer._vulkan;
            buffer.Dispose();
            buffer = new HostBuffer(vulkan, size, usage);
        }
    }

    public void Write<T>(ReadOnlySpan<T> data, ulong offset)
        where T : unmanaged
    {
        MemoryMarshal.AsBytes(data).CopyTo(new Span<byte>(_mapped + offset, data.Length * sizeof(T)));
    }

    public void Write<T>(in T value, ulong offset)
        where T : unmanaged
    {
        *(T*)(_mapped + offset) = value;
    }

    public void Dispose()
    {
        _vulkan.Api.DestroyBuffer(_vulkan.Device, Buffer, null);
        _vulkan.Api.FreeMemory(_vulkan.Device, _memory, null);
    }
}
