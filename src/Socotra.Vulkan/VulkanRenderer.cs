using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkFilter = Silk.NET.Vulkan.Filter;

namespace Socotra.Vulkan;

/// <summary>
/// Draws your UI with Vulkan, on a device you made. Create one per device, then call <see cref="Record"/> each
/// frame. Before each call, wait until the GPU has finished the commands from <c>framesInFlight</c> calls ago,
/// and submit them all to one queue, in order. If you draw several lists a frame, count each one as a frame in flight.
/// </summary>
/// <example>
/// <code>
/// // When you create the device, turn on what the renderer needs
/// if (!VulkanRenderer.IsSupported(vk, physicalDevice)) { /* pick another GPU */ }
/// extensions.AddRange(VulkanRenderer.DeviceExtensions);
/// VulkanRenderer.EnableFeatures(ref features, ref vulkan12Features, ref dynamicRenderingFeatures);
///
/// // Then make the renderer and a draw list to reuse every frame
/// var renderer = new VulkanRenderer(vk, physicalDevice, device, framesInFlight: 2);
/// var list = new DrawList();
///
/// // Every frame: draw the UI and add it to your command buffer, outside a render pass
/// root.Update(new Rect(0, 0, width, height), deltaTime);
/// root.Paint(list);
/// renderer.Record(commandBuffer, list, new RenderTarget(image, view, Format.B8G8R8A8Unorm, extent),
///     currentLayout, ImageLayout.PresentSrcKhr);
/// </code>
/// </example>
public sealed unsafe partial class VulkanRenderer : IDisposable
{
    private const int MaxTextures = 4096;
    private const float MaxAnisotropy = 8;

    private readonly VulkanDevice _vulkan;
    private readonly Sampler[] _samplers;
    private readonly DescriptorSetLayout _tableLayout;
    private readonly DescriptorSetLayout _textureLayout;
    private readonly DescriptorSetLayout _constantLayout;
    private readonly PipelineLayout _pipelineLayout;
    private readonly Pipelines _pipelines;
    private readonly Frame[] _frames;
    private readonly uint _textureCapacity;
    private readonly ulong _constantStride;
    private bool _disposed;

    /// <summary>
    /// A renderer for a device you made. Check the GPU with <see cref="IsSupported"/>, and enable
    /// <see cref="DeviceExtensions"/> and <see cref="EnableFeatures"/> when you create the device.
    /// </summary>
    /// <param name="api">The Vulkan API the device was made with.</param>
    /// <param name="physicalDevice">The GPU the device runs on.</param>
    /// <param name="device">The device.</param>
    /// <param name="framesInFlight">How many <see cref="Record"/> calls can have their commands on the GPU at once.</param>
    public VulkanRenderer(Vk api, PhysicalDevice physicalDevice, Device device, int framesInFlight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(framesInFlight, 1);
        _vulkan = new VulkanDevice(api, physicalDevice, device);
        var limits = _vulkan.Limits;
        _textureCapacity = Math.Min(MaxTextures, Math.Min(limits.MaxPerStageDescriptorSampledImages, limits.MaxDescriptorSetSampledImages));
        _constantStride = (FramePass.ConstantsRange + limits.MinUniformBufferOffsetAlignment - 1) / limits.MinUniformBufferOffsetAlignment * limits.MinUniformBufferOffsetAlignment;
        _samplers = [.. Enumerable.Range(0, Samplers.Count).Select(CreateSampler)];
        _tableLayout = CreateTableLayout();
        _textureLayout = CreateTextureLayout();
        _constantLayout = CreateConstantLayout();
        _pipelineLayout = CreatePipelineLayout();
        CreateTextRaster();
        _pipelines = new Pipelines(_vulkan, _pipelineLayout, _rasterPipelineLayout);
        _frames = [.. Enumerable.Range(0, framesInFlight).Select(_ => new Frame(CreatePass))];
        _curves = new AppendBuffer<Vector2>(_vulkan);
        _bands = new AppendBuffer<uint>(_vulkan);
        _glyphs = new AppendBuffer<GpuFontGlyphCache.Glyph>(_vulkan);
    }

    /// <summary>The device extensions to enable when you create the device.</summary>
    public static IReadOnlyList<string> DeviceExtensions { get; } = ["VK_KHR_dynamic_rendering"];

    /// <summary>
    /// Whether the renderer works on <paramref name="physicalDevice"/>. It needs Vulkan 1.2, so create the
    /// instance with an API version of 1.2 or newer.
    /// </summary>
    public static bool IsSupported(Vk api, PhysicalDevice physicalDevice)
    {
        api.GetPhysicalDeviceProperties(physicalDevice, out var properties);
        if (properties.ApiVersion < Vk.Version12)
        {
            return false;
        }

        uint count = 0;
        api.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, null);
        var extensions = new ExtensionProperties[count];
        fixed (ExtensionProperties* pointer = extensions)
        {
            api.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, pointer);
        }

        var names = extensions.Select(extension => Marshal.PtrToStringUTF8((nint)extension.ExtensionName)).ToHashSet();
        if (!DeviceExtensions.All(names.Contains))
        {
            return false;
        }

        var dynamicRendering = new PhysicalDeviceDynamicRenderingFeatures { SType = StructureType.PhysicalDeviceDynamicRenderingFeatures };
        var vulkan12 = new PhysicalDeviceVulkan12Features { SType = StructureType.PhysicalDeviceVulkan12Features, PNext = &dynamicRendering };
        var features = new PhysicalDeviceFeatures2 { SType = StructureType.PhysicalDeviceFeatures2, PNext = &vulkan12 };
        api.GetPhysicalDeviceFeatures2(physicalDevice, &features);
        return features.Features.SamplerAnisotropy && vulkan12.ScalarBlockLayout && vulkan12.DescriptorIndexing && vulkan12.RuntimeDescriptorArray
            && vulkan12.ShaderSampledImageArrayNonUniformIndexing && vulkan12.DescriptorBindingPartiallyBound && dynamicRendering.DynamicRendering;
    }

    /// <summary>
    /// Turns on the device features the renderer needs, on top of the ones you already set. Chain
    /// <paramref name="vulkan12"/> and <paramref name="dynamicRendering"/> into the device's create info and pass
    /// <paramref name="features"/> as its enabled features.
    /// </summary>
    public static void EnableFeatures(ref PhysicalDeviceFeatures features, ref PhysicalDeviceVulkan12Features vulkan12, ref PhysicalDeviceDynamicRenderingFeatures dynamicRendering)
    {
        features.SamplerAnisotropy = true;
        vulkan12.SType = StructureType.PhysicalDeviceVulkan12Features;
        vulkan12.ScalarBlockLayout = true;
        vulkan12.DescriptorIndexing = true;
        vulkan12.RuntimeDescriptorArray = true;
        vulkan12.ShaderSampledImageArrayNonUniformIndexing = true;
        vulkan12.DescriptorBindingPartiallyBound = true;
        dynamicRendering.SType = StructureType.PhysicalDeviceDynamicRenderingFeatures;
        dynamicRendering.DynamicRendering = true;
    }

    /// <summary>
    /// Adds commands to <paramref name="commandBuffer"/> that draw <paramref name="list"/> into <paramref name="target"/>.
    /// Call it outside a render pass. After a resize, just pass the new image.
    /// </summary>
    /// <param name="commandBuffer">The command buffer you're recording.</param>
    /// <param name="list">What to draw, from <see cref="RootPanel.Paint"/> or <see cref="Painter.Begin(DrawList, Rect)"/>.</param>
    /// <param name="target">The image to draw into.</param>
    /// <param name="oldLayout">The image's layout now. Pass <see cref="ImageLayout.Undefined"/> if the list starts with <see cref="Painter.Clear"/> and you don't need what's in the image.</param>
    /// <param name="newLayout">The layout to leave the image in, like <see cref="ImageLayout.PresentSrcKhr"/>.</param>
    /// <exception cref="InvalidOperationException">The list uses more textures and layers than the device can bind at once.</exception>
    public void Record(CommandBuffer commandBuffer, DrawList list, RenderTarget target, ImageLayout oldLayout, ImageLayout newLayout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfZero(target.Size.Width);
        ArgumentOutOfRangeException.ThrowIfZero(target.Size.Height);

        var frame = _frames[_recorded % _frames.Length];
        _recorded++;
        frame.Begin();
        HostBuffer[] glyphs = [];
        GpuFontGlyphCache.Read((curves, bands, table) => glyphs = [UploadGlyphs(_curves, curves), UploadGlyphs(_bands, bands), UploadGlyphs(_glyphs, table)]);
        PaintRenderTargets(commandBuffer, frame, glyphs, list);
        RecordPass(commandBuffer, frame.NextPass(), glyphs, list, new Surface(target.Image, target.View, target.Size, target.Format), oldLayout, newLayout, AttachmentLoadOp.Load);
        Sweep();
    }

    /// <summary>Frees the renderer. It waits for the device to go idle first.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var api = _vulkan.Api;
        var device = _vulkan.Device;
        api.DeviceWaitIdle(device);
        FreeResources();
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _curves.Dispose();
        _bands.Dispose();
        _glyphs.Dispose();
        _pipelines.Dispose();
        DestroyTextRaster();
        api.DestroyPipelineLayout(device, _pipelineLayout, null);
        api.DestroyDescriptorSetLayout(device, _constantLayout, null);
        api.DestroyDescriptorSetLayout(device, _textureLayout, null);
        api.DestroyDescriptorSetLayout(device, _tableLayout, null);
        foreach (var sampler in _samplers)
        {
            api.DestroySampler(device, sampler, null);
        }
    }

    private static SamplerAddressMode AddressMode(TextureAddress address) => address switch
    {
        TextureAddress.Clamp => SamplerAddressMode.ClampToEdge,
        TextureAddress.Border => SamplerAddressMode.ClampToBorder,
        _ => SamplerAddressMode.Repeat,
    };

    private Sampler CreateSampler(int index)
    {
        var (filter, u, v) = index switch
        {
            Samplers.TrilinearBorder => (FilterMode.Trilinear, TextureAddress.Border, TextureAddress.Border),
            Samplers.TrilinearClamp => (FilterMode.Trilinear, TextureAddress.Clamp, TextureAddress.Clamp),
            _ => Samplers.At(index),
        };
        var linear = filter != FilterMode.Point;
        var info = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = linear ? VkFilter.Linear : VkFilter.Nearest,
            MinFilter = linear ? VkFilter.Linear : VkFilter.Nearest,
            MipmapMode = filter >= FilterMode.Trilinear ? SamplerMipmapMode.Linear : SamplerMipmapMode.Nearest,
            AddressModeU = AddressMode(u),
            AddressModeV = AddressMode(v),
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = filter == FilterMode.Anisotropic,
            MaxAnisotropy = Math.Min(MaxAnisotropy, _vulkan.Limits.MaxSamplerAnisotropy),
            MaxLod = Vk.LodClampNone,
            BorderColor = BorderColor.FloatTransparentBlack,
        };
        VulkanDevice.Check(_vulkan.Api.CreateSampler(_vulkan.Device, in info, null, out var sampler), "vkCreateSampler");
        return sampler;
    }

    private DescriptorSetLayout CreateLayout(ReadOnlySpan<DescriptorSetLayoutBinding> bindings, ReadOnlySpan<DescriptorBindingFlags> flags)
    {
        fixed (DescriptorSetLayoutBinding* bindingPointer = bindings)
        fixed (DescriptorBindingFlags* flagPointer = flags)
        {
            var flagInfo = new DescriptorSetLayoutBindingFlagsCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfo,
                BindingCount = (uint)flags.Length,
                PBindingFlags = flagPointer,
            };
            var info = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                PNext = flags.IsEmpty ? null : &flagInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bindingPointer,
            };
            VulkanDevice.Check(_vulkan.Api.CreateDescriptorSetLayout(_vulkan.Device, in info, null, out var layout), "vkCreateDescriptorSetLayout");
            return layout;
        }
    }

    private DescriptorSetLayout CreateTableLayout()
    {
        var bindings = new DescriptorSetLayoutBinding[FramePass.BindingCount];
        for (int i = 0; i < bindings.Length; i++)
        {
            bindings[i] = new DescriptorSetLayoutBinding((uint)i, DescriptorType.StorageBuffer, 1, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit | ShaderStageFlags.ComputeBit);
        }

        return CreateLayout(bindings, []);
    }

    private DescriptorSetLayout CreateTextureLayout()
    {
        fixed (Sampler* samplers = _samplers)
        {
            return CreateLayout(
                [
                    new DescriptorSetLayoutBinding(0, DescriptorType.SampledImage, _textureCapacity, ShaderStageFlags.FragmentBit),
                    new DescriptorSetLayoutBinding(1, DescriptorType.Sampler, (uint)_samplers.Length, ShaderStageFlags.FragmentBit, samplers),
                ],
                [DescriptorBindingFlags.PartiallyBoundBit, 0]);
        }
    }

    private DescriptorSetLayout CreateConstantLayout() =>
        CreateLayout([new DescriptorSetLayoutBinding(0, DescriptorType.UniformBufferDynamic, 1, ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit)], []);

    private PipelineLayout CreatePipelineLayout()
    {
        var layouts = stackalloc DescriptorSetLayout[] { _tableLayout, _textureLayout, _constantLayout };
        var pushConstants = new PushConstantRange(ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, 0, (uint)sizeof(BoxConstants));
        var info = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 3,
            PSetLayouts = layouts,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstants,
        };
        VulkanDevice.Check(_vulkan.Api.CreatePipelineLayout(_vulkan.Device, in info, null, out var layout), "vkCreatePipelineLayout");
        return layout;
    }

    private FramePass CreatePass() => new(_vulkan, _tableLayout, _textureLayout, _constantLayout, _textureCapacity, (uint)_samplers.Length);

    private void RecordPass(CommandBuffer commandBuffer, FramePass pass, HostBuffer[] glyphs, DrawList list, Surface target,
        ImageLayout oldLayout, ImageLayout newLayout, AttachmentLoadOp load)
    {
        Prepare(list, target);
        pass.Upload(list.Batcher, glyphs, CollectionsMarshal.AsSpan(_rasterInstances), CollectionsMarshal.AsSpan(_rasterTiles));
        pass.ReserveConstants((ulong)Math.Max(_quadCount, 1) * _constantStride);
        UploadTextures(commandBuffer, pass);
        RasterTextures(commandBuffer, pass, list.Batcher.Texts.Count);
        pass.BindTextures(CollectionsMarshal.AsSpan(_views));

        _vulkan.Barrier(commandBuffer, target.Image, oldLayout, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags.AllCommandsBit, AccessFlags.MemoryWriteBit,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit);
        Execute(commandBuffer, pass, list, target, load);
        _vulkan.Barrier(commandBuffer, target.Image, ImageLayout.ColorAttachmentOptimal, newLayout,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.AllCommandsBit, AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit);
    }
}
