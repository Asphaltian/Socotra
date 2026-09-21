using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

internal sealed unsafe class AppendBuffer<T>(VulkanDevice vulkan) : IDisposable
    where T : unmanaged
{
    private int _count;

    public HostBuffer Buffer { get; private set; } = new(vulkan, HostBuffer.MinimumSize, BufferUsageFlags.StorageBufferBit);

    public HostBuffer? Append(ReadOnlySpan<T> items)
    {
        HostBuffer? replaced = null;
        var size = (ulong)(items.Length * sizeof(T));
        if (size > Buffer.Size)
        {
            replaced = Buffer;
            Buffer = new HostBuffer(vulkan, size, BufferUsageFlags.StorageBufferBit);
            _count = 0;
        }

        Buffer.Write(items[_count..], (ulong)(_count * sizeof(T)));
        _count = items.Length;
        return replaced;
    }

    public void Dispose() => Buffer.Dispose();
}
