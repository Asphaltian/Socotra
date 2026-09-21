using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

using Image = Silk.NET.Vulkan.Image;

internal sealed unsafe class GpuImage : IDisposable
{
    private readonly VulkanDevice _vulkan;
    private readonly DeviceMemory _memory;
    private readonly ImageView[] _mipViews;

    public GpuImage(VulkanDevice vulkan, uint width, uint height, Format format, uint mipLevels, ImageUsageFlags usage)
    {
        _vulkan = vulkan;
        Width = width;
        Height = height;
        Format = format;
        MipLevels = mipLevels;
        Usage = usage;
        var api = vulkan.Api;
        var info = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels = mipLevels,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };
        VulkanDevice.Check(api.CreateImage(vulkan.Device, in info, null, out var image), "vkCreateImage");
        Image = image;
        api.GetImageMemoryRequirements(vulkan.Device, image, out var requirements);
        _memory = vulkan.Allocate(requirements, MemoryPropertyFlags.DeviceLocalBit);
        VulkanDevice.Check(api.BindImageMemory(vulkan.Device, image, _memory, 0), "vkBindImageMemory");
        View = CreateView(0, mipLevels);
        _mipViews = mipLevels > 1 && (usage.HasFlag(ImageUsageFlags.ColorAttachmentBit) || usage.HasFlag(ImageUsageFlags.StorageBit))
            ? [.. Enumerable.Range(0, (int)mipLevels).Select(level => CreateView((uint)level, 1))]
            : [View];
    }

    public Image Image { get; }

    public ImageView View { get; }

    public uint Width { get; }

    public uint Height { get; }

    public Extent2D Size => new(Width, Height);

    public Format Format { get; }

    public uint MipLevels { get; }

    public ImageUsageFlags Usage { get; }

    public long LastUsed { get; set; }

    public static uint MipCount(uint width, uint height) => (uint)BitOperations.Log2(Math.Max(width, height)) + 1;

    public ImageView MipView(int level) => _mipViews[level];

    public Extent2D MipSize(int level) => new(Math.Max(Width >> level, 1), Math.Max(Height >> level, 1));

    public void Dispose()
    {
        var api = _vulkan.Api;
        foreach (var view in _mipViews)
        {
            if (view.Handle != View.Handle)
            {
                api.DestroyImageView(_vulkan.Device, view, null);
            }
        }

        api.DestroyImageView(_vulkan.Device, View, null);
        api.DestroyImage(_vulkan.Device, Image, null);
        api.FreeMemory(_vulkan.Device, _memory, null);
    }

    private ImageView CreateView(uint firstMip, uint mipCount)
    {
        var info = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Image,
            ViewType = ImageViewType.Type2D,
            Format = Format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, firstMip, mipCount, 0, 1),
        };
        VulkanDevice.Check(_vulkan.Api.CreateImageView(_vulkan.Device, in info, null, out var view), "vkCreateImageView");
        return view;
    }
}
