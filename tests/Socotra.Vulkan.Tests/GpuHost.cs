using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Socotra.Vulkan.Tests;

using Image = Silk.NET.Vulkan.Image;

public sealed unsafe class GpuHost : IDisposable
{
    public const int FramesInFlight = 2;

    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";

    private static readonly ConcurrentQueue<string> Messages = new();
    private static readonly Lazy<bool> Supported = new(Probe);

    private readonly Vk _vk = Vk.GetApi();
    private readonly Instance _instance;
    private readonly DebugUtilsMessengerEXT _messenger;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Device _device;
    private readonly Queue _queue;
    private readonly CommandPool _commandPool;
    private readonly CommandBuffer _commandBuffer;
    private readonly Fence _fence;

    public GpuHost()
    {
        Validating = HasValidationLayer(_vk);
        if (!Validating)
        {
            Console.Error.WriteLine($"{ValidationLayer} isn't installed, so the GPU tests run without Vulkan validation.");
        }

        _instance = CreateInstance(_vk, Validating);
        if (Validating)
        {
            _messenger = CreateMessenger();
        }

        _physicalDevice = PickPhysicalDevice(_vk, _instance) ?? throw new InvalidOperationException("No GPU can run the renderer.");
        var family = GraphicsFamily(_vk, _physicalDevice)!.Value;
        _device = CreateDevice(family);
        _vk.GetDeviceQueue(_device, family, 0, out _queue);

        var poolInfo = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, Flags = CommandPoolCreateFlags.ResetCommandBufferBit, QueueFamilyIndex = family };
        Check(_vk.CreateCommandPool(_device, in poolInfo, null, out _commandPool));
        var allocation = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo, CommandPool = _commandPool, Level = CommandBufferLevel.Primary, CommandBufferCount = 1 };
        Check(_vk.AllocateCommandBuffers(_device, in allocation, out _commandBuffer));
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        Check(_vk.CreateFence(_device, in fenceInfo, null, out _fence));

        Renderer = new VulkanRenderer(_vk, _physicalDevice, _device, FramesInFlight);
    }

    public static bool Available => Supported.Value;

    public bool Validating { get; }

    public Vk Api => _vk;

    public PhysicalDevice PhysicalDevice => _physicalDevice;

    public Device Device => _device;

    public VulkanRenderer Renderer { get; }

    public Snapshot Render(int width, int height, Action<Painter> paint, Format format = Format.R8G8B8A8Unorm)
    {
        var list = new DrawList();
        using (var painter = Painter.Begin(list, new Rect(0, 0, width, height)))
        {
            paint(painter);
        }

        return Render(list, width, height, format);
    }

    public Snapshot Render(DrawList list, int width, int height, Format format = Format.R8G8B8A8Unorm)
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };
        Check(_vk.CreateImage(_device, in imageInfo, null, out var image));
        _vk.GetImageMemoryRequirements(_device, image, out var imageRequirements);
        var imageMemory = Allocate(imageRequirements, MemoryPropertyFlags.DeviceLocalBit);
        Check(_vk.BindImageMemory(_device, image, imageMemory, 0));
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };
        Check(_vk.CreateImageView(_device, in viewInfo, null, out var view));

        var size = (ulong)(width * height * 4);
        var bufferInfo = new BufferCreateInfo { SType = StructureType.BufferCreateInfo, Size = size, Usage = BufferUsageFlags.TransferDstBit, SharingMode = SharingMode.Exclusive };
        Check(_vk.CreateBuffer(_device, in bufferInfo, null, out var buffer));
        _vk.GetBufferMemoryRequirements(_device, buffer, out var bufferRequirements);
        var bufferMemory = Allocate(bufferRequirements, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        Check(_vk.BindBufferMemory(_device, buffer, bufferMemory, 0));

        try
        {
            Check(_vk.ResetCommandBuffer(_commandBuffer, 0));
            var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            Check(_vk.BeginCommandBuffer(_commandBuffer, in begin));
            Renderer.Record(_commandBuffer, list, new RenderTarget(image, view, format, new Extent2D((uint)width, (uint)height)), ImageLayout.Undefined, ImageLayout.TransferSrcOptimal);
            var region = new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D((uint)width, (uint)height, 1),
            };
            _vk.CmdCopyImageToBuffer(_commandBuffer, image, ImageLayout.TransferSrcOptimal, buffer, 1, &region);
            var barrier = new BufferMemoryBarrier
            {
                SType = StructureType.BufferMemoryBarrier,
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.HostReadBit,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Buffer = buffer,
                Size = Vk.WholeSize,
            };
            _vk.CmdPipelineBarrier(_commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.HostBit, 0, 0, null, 1, &barrier, 0, null);
            Check(_vk.EndCommandBuffer(_commandBuffer));

            var commandBuffer = _commandBuffer;
            var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &commandBuffer };
            Check(_vk.QueueSubmit(_queue, 1, &submit, _fence));
            var fence = _fence;
            Check(_vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue));
            Check(_vk.ResetFences(_device, 1, &fence));

            void* mapped;
            Check(_vk.MapMemory(_device, bufferMemory, 0, size, 0, &mapped));
            var pixels = new ReadOnlySpan<byte>(mapped, (int)size).ToArray();
            _vk.UnmapMemory(_device, bufferMemory);
            ThrowOnValidationMessages();
            return new Snapshot(width, height, pixels, format == Format.B8G8R8A8Unorm);
        }
        finally
        {
            _vk.DestroyBuffer(_device, buffer, null);
            _vk.FreeMemory(_device, bufferMemory, null);
            _vk.DestroyImageView(_device, view, null);
            _vk.DestroyImage(_device, image, null);
            _vk.FreeMemory(_device, imageMemory, null);
        }
    }

    public void Dispose()
    {
        Renderer.Dispose();
        _vk.DestroyFence(_device, _fence, null);
        _vk.DestroyCommandPool(_device, _commandPool, null);
        _vk.DestroyDevice(_device, null);
        if (Validating)
        {
            var destroy = (delegate* unmanaged<Instance, DebugUtilsMessengerEXT, AllocationCallbacks*, void>)InstanceProcedure(_vk, _instance, "vkDestroyDebugUtilsMessengerEXT");
            destroy(_instance, _messenger, null);
        }

        _vk.DestroyInstance(_instance, null);
        ThrowOnValidationMessages();
    }

    private static void Check(Result result)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"Vulkan call failed with {result}.");
        }
    }

    private static void ThrowOnValidationMessages()
    {
        var messages = new List<string>();
        while (Messages.TryDequeue(out var message))
        {
            messages.Add(message);
        }

        if (messages.Count > 0)
        {
            throw new InvalidOperationException("Validation reported:" + Environment.NewLine + string.Join(Environment.NewLine, messages));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static Bool32 OnMessage(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT types, DebugUtilsMessengerCallbackDataEXT* data, void* user)
    {
        Messages.Enqueue($"{severity} {types}: {Marshal.PtrToStringUTF8((nint)data->PMessage)}");
        return false;
    }

    private static bool Probe()
    {
        try
        {
            var vk = Vk.GetApi();
            var instance = CreateInstance(vk, validate: false);
            try
            {
                return PickPhysicalDevice(vk, instance) is not null;
            }
            finally
            {
                vk.DestroyInstance(instance, null);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static bool HasValidationLayer(Vk vk)
    {
        uint count = 0;
        vk.EnumerateInstanceLayerProperties(&count, null);
        var layers = new LayerProperties[count];
        fixed (LayerProperties* pointer = layers)
        {
            vk.EnumerateInstanceLayerProperties(&count, pointer);
        }

        return layers.Any(layer => Marshal.PtrToStringUTF8((nint)layer.LayerName) == ValidationLayer);
    }

    private static DebugUtilsMessengerCreateInfoEXT MessengerInfo() => new()
    {
        SType = StructureType.DebugUtilsMessengerCreateInfoExt,
        MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
        MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
        PfnUserCallback = (delegate* unmanaged[Cdecl]<DebugUtilsMessageSeverityFlagsEXT, DebugUtilsMessageTypeFlagsEXT, DebugUtilsMessengerCallbackDataEXT*, void*, Bool32>)&OnMessage,
    };

    private static Instance CreateInstance(Vk vk, bool validate)
    {
        var application = new ApplicationInfo { SType = StructureType.ApplicationInfo, ApiVersion = Vk.Version12 };
        string[] layers = validate ? [ValidationLayer] : [];
        string[] extensions = validate ? ["VK_EXT_debug_utils", "VK_EXT_validation_features"] : [];
        var enabled = stackalloc ValidationFeatureEnableEXT[] { ValidationFeatureEnableEXT.SynchronizationValidationExt };
        var messenger = MessengerInfo();
        var features = new ValidationFeaturesEXT
        {
            SType = StructureType.ValidationFeaturesExt,
            PNext = &messenger,
            EnabledValidationFeatureCount = 1,
            PEnabledValidationFeatures = enabled,
        };
        var layerNames = (byte**)SilkMarshal.StringArrayToPtr(layers);
        var extensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions);
        try
        {
            var info = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PNext = validate ? &features : null,
                PApplicationInfo = &application,
                EnabledLayerCount = (uint)layers.Length,
                PpEnabledLayerNames = layerNames,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = extensionNames,
            };
            Check(vk.CreateInstance(in info, null, out var instance));
            return instance;
        }
        finally
        {
            SilkMarshal.Free((nint)layerNames);
            SilkMarshal.Free((nint)extensionNames);
        }
    }

    private static void* InstanceProcedure(Vk vk, Instance instance, string name) => (void*)vk.GetInstanceProcAddr(instance, name);

    private static PhysicalDevice? PickPhysicalDevice(Vk vk, Instance instance)
    {
        uint count = 0;
        vk.EnumeratePhysicalDevices(instance, &count, null);
        var devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* pointer = devices)
        {
            vk.EnumeratePhysicalDevices(instance, &count, pointer);
        }

        var usable = devices.Where(device => VulkanRenderer.IsSupported(vk, device) && GraphicsFamily(vk, device) is not null).ToList();
        return usable.OrderBy(device =>
        {
            vk.GetPhysicalDeviceProperties(device, out var properties);
            return properties.DeviceType == PhysicalDeviceType.DiscreteGpu ? 0 : 1;
        }).Cast<PhysicalDevice?>().FirstOrDefault();
    }

    private static uint? GraphicsFamily(Vk vk, PhysicalDevice device)
    {
        uint count = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = new QueueFamilyProperties[count];
        fixed (QueueFamilyProperties* pointer = families)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, pointer);
        }

        var index = Array.FindIndex(families, family => family.QueueFlags.HasFlag(QueueFlags.GraphicsBit));
        return index < 0 ? null : (uint)index;
    }

    private DebugUtilsMessengerEXT CreateMessenger()
    {
        var create = (delegate* unmanaged<Instance, DebugUtilsMessengerCreateInfoEXT*, AllocationCallbacks*, DebugUtilsMessengerEXT*, Result>)InstanceProcedure(_vk, _instance, "vkCreateDebugUtilsMessengerEXT");
        var info = MessengerInfo();
        DebugUtilsMessengerEXT messenger;
        Check(create(_instance, &info, null, &messenger));
        return messenger;
    }

    private Device CreateDevice(uint family)
    {
        var priority = 1f;
        var queue = new DeviceQueueCreateInfo { SType = StructureType.DeviceQueueCreateInfo, QueueFamilyIndex = family, QueueCount = 1, PQueuePriorities = &priority };
        PhysicalDeviceFeatures features = default;
        PhysicalDeviceVulkan12Features vulkan12 = default;
        PhysicalDeviceDynamicRenderingFeatures dynamicRendering = default;
        VulkanRenderer.EnableFeatures(ref features, ref vulkan12, ref dynamicRendering);
        vulkan12.PNext = &dynamicRendering;
        var extensions = (byte**)SilkMarshal.StringArrayToPtr(VulkanRenderer.DeviceExtensions);
        try
        {
            var info = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &vulkan12,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queue,
                EnabledExtensionCount = (uint)VulkanRenderer.DeviceExtensions.Count,
                PpEnabledExtensionNames = extensions,
                PEnabledFeatures = &features,
            };
            Check(_vk.CreateDevice(_physicalDevice, in info, null, out var device));
            return device;
        }
        finally
        {
            SilkMarshal.Free((nint)extensions);
        }
    }

    private DeviceMemory Allocate(MemoryRequirements requirements, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memory);
        for (int i = 0; i < memory.MemoryTypeCount; i++)
        {
            if ((requirements.MemoryTypeBits & (1u << i)) != 0 && (memory.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                var info = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo, AllocationSize = requirements.Size, MemoryTypeIndex = (uint)i };
                Check(_vk.AllocateMemory(_device, in info, null, out var allocation));
                return allocation;
            }
        }

        throw new InvalidOperationException($"No memory is {properties}.");
    }
}
