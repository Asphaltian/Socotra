using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

using Image = Silk.NET.Vulkan.Image;

internal sealed unsafe class VulkanDevice
{
    private readonly PhysicalDeviceMemoryProperties _memory;
    private readonly delegate* unmanaged<CommandBuffer, RenderingInfo*, void> _beginRendering;
    private readonly delegate* unmanaged<CommandBuffer, void> _endRendering;

    public VulkanDevice(Vk api, PhysicalDevice physicalDevice, Device device)
    {
        Api = api;
        Device = device;
        api.GetPhysicalDeviceMemoryProperties(physicalDevice, out _memory);
        api.GetPhysicalDeviceProperties(physicalDevice, out var properties);
        Limits = properties.Limits;
        _beginRendering = (delegate* unmanaged<CommandBuffer, RenderingInfo*, void>)Procedure("vkCmdBeginRenderingKHR");
        _endRendering = (delegate* unmanaged<CommandBuffer, void>)Procedure("vkCmdEndRenderingKHR");
    }

    public Vk Api { get; }

    public Device Device { get; }

    public PhysicalDeviceLimits Limits { get; }

    public static void Check(Result result, string call)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"{call} failed with {result}.");
        }
    }

    public DeviceMemory Allocate(MemoryRequirements requirements, MemoryPropertyFlags properties)
    {
        for (int i = 0; i < _memory.MemoryTypeCount; i++)
        {
            if ((requirements.MemoryTypeBits & (1u << i)) != 0 && (_memory.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                var info = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = (uint)i };
                Check(Api.AllocateMemory(Device, in info, null, out var memory), "vkAllocateMemory");
                return memory;
            }
        }

        throw new InvalidOperationException($"The GPU has no memory that is {properties}.");
    }

    public void BeginRendering(CommandBuffer commandBuffer, ImageView view, Extent2D size, AttachmentLoadOp load)
    {
        var attachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = view,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = load,
            StoreOp = AttachmentStoreOp.Store,
        };
        var info = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D(default, size),
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &attachment,
        };
        _beginRendering(commandBuffer, &info);
    }

    public void EndRendering(CommandBuffer commandBuffer) => _endRendering(commandBuffer);

    public void Barrier(CommandBuffer commandBuffer, Image image, ImageLayout from, ImageLayout to, PipelineStageFlags sourceStage, AccessFlags sourceAccess,
        PipelineStageFlags destinationStage, AccessFlags destinationAccess, uint firstMip = 0, uint mipCount = 1)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess,
            OldLayout = from,
            NewLayout = to,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, firstMip, mipCount, 0, 1),
        };
        Api.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
    }

    private void* Procedure(string name)
    {
        var procedure = (void*)Api.GetDeviceProcAddr(Device, name);
        return procedure != null ? procedure : throw new InvalidOperationException($"The device has no {name}. Enable the extensions in VulkanRenderer.DeviceExtensions.");
    }
}
