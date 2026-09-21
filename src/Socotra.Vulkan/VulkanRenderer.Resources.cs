using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkFilter = Silk.NET.Vulkan.Filter;

namespace Socotra.Vulkan;

using Image = Silk.NET.Vulkan.Image;

public sealed unsafe partial class VulkanRenderer
{
    private const ImageUsageFlags LayerUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferSrcBit;
    private const ImageUsageFlags GrabUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit;
    private const ImageUsageFlags TextureUsage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.TransferSrcBit;

    private readonly AppendBuffer<Vector2> _curves;
    private readonly AppendBuffer<uint> _bands;
    private readonly AppendBuffer<GpuFontGlyphCache.Glyph> _glyphs;
    private readonly Dictionary<Texture, (GpuImage Image, int? Version)> _textures = [];
    private readonly List<GpuImage> _targets = [];
    private readonly List<(long Record, HostBuffer Buffer)> _retired = [];
    private readonly List<ImageView> _views = [];
    private readonly Dictionary<Texture, int> _textureSlots = [];
    private readonly List<(Texture Texture, GpuImage Image)> _uploads = [];
    private readonly Dictionary<(uint Width, uint Height, Format Format), Grab> _grabs = [];
    private readonly Stack<Surface> _surfaces = [];
    private Layer[] _layers = [];
    private int _quadCount;
    private long _recorded;

    private void Prepare(DrawList list, Surface target)
    {
        _views.Clear();
        _views.Add(default);
        _textureSlots.Clear();
        _uploads.Clear();
        _rasters.Clear();
        _rasterInstances.Clear();
        _rasterTiles.Clear();
        _grabs.Clear();
        _quadCount = 0;
        if (_layers.Length < list.LayerCount)
        {
            Array.Resize(ref _layers, list.LayerCount);
        }

        CheckViewCount(list.Textures.Count + 1);
        foreach (var texture in list.Textures)
        {
            var slot = AddTexture(texture);
            Debug.Assert(slot == list.TextureIndex(texture));
        }

        _surfaces.Clear();
        _surfaces.Push(target);
        foreach (var command in list.Commands)
        {
            switch (command)
            {
                case BeginLayerCommand begin:
                    var image = AcquireTarget((uint)Math.Max(begin.Width, 1), (uint)Math.Max(begin.Height, 1), Format.R8G8B8A8Unorm, 1, LayerUsage);
                    _layers[begin.Layer] = new Layer(image, AddView(image.View));
                    _surfaces.Push(new Surface(image.Image, image.View, image.Size, image.Format));
                    break;
                case EndLayerCommand:
                    _surfaces.Pop();
                    break;
                case FilterCommand filter:
                    _quadCount++;
                    if (filter.Mask is { } mask)
                    {
                        AddTexture(mask.Texture);
                    }

                    break;
                case DropShadowCommand or BorderWrapCommand:
                    _quadCount++;
                    break;
                case BackdropCommand backdrop:
                    _quadCount++;
                    var (size, format) = (Current.Size, Current.Format);
                    if (!backdrop.ReuseGrab && !_grabs.ContainsKey((size.Width, size.Height, format)))
                    {
                        var grab = AcquireTarget(size.Width, size.Height, format, GpuImage.MipCount(size.Width, size.Height), GrabUsage);
                        var slot = AddView(grab.View);
                        for (int level = 0; level < grab.MipLevels; level++)
                        {
                            AddView(grab.MipView(level));
                        }

                        _grabs.Add((size.Width, size.Height, format), new Grab(grab, slot));
                    }

                    break;
            }
        }

        CheckViewCount(_views.Count);
    }

    private void CheckViewCount(int count)
    {
        if (count > _textureCapacity)
        {
            throw new InvalidOperationException($"The draw list needs {count} textures, more than the {_textureCapacity} the renderer can bind.");
        }
    }

    private int AddTexture(Texture texture)
    {
        if (_textureSlots.TryGetValue(texture, out var existing))
        {
            return existing;
        }

        if (_renderTargets.TryGetValue(texture, out var target))
        {
            target.LastUsed = _recorded;
            var targetSlot = AddView(target.View);
            _textureSlots.Add(texture, targetSlot);
            return targetSlot;
        }

        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_textures, texture, out var exists);
        if (!exists)
        {
            entry.Image = new GpuImage(_vulkan, (uint)texture.Width, (uint)texture.Height, Format.R8G8B8A8Unorm, GpuImage.MipCount((uint)texture.Width, (uint)texture.Height),
                texture.Raster is null ? TextureUsage : RasterUsage);
        }

        if (entry.Version != texture.DirtyVersion)
        {
            if (texture.Raster is null)
            {
                _uploads.Add((texture, entry.Image));
            }
            else
            {
                AddRaster(texture, entry.Image);
            }
        }

        entry.Image.LastUsed = _recorded;
        var slot = AddView(entry.Image.View);
        _textureSlots.Add(texture, slot);
        return slot;
    }

    private int AddView(ImageView view)
    {
        _views.Add(view);
        return _views.Count - 1;
    }

    private GpuImage AcquireTarget(uint width, uint height, Format format, uint mipLevels, ImageUsageFlags usage)
    {
        var image = _targets.Find(image => image.LastUsed != _recorded && image.Width == width && image.Height == height
            && image.Format == format && image.MipLevels == mipLevels && image.Usage == usage);
        if (image is null)
        {
            image = new GpuImage(_vulkan, width, height, format, mipLevels, usage);
            _targets.Add(image);
        }

        image.LastUsed = _recorded;
        return image;
    }

    private HostBuffer UploadGlyphs<T>(AppendBuffer<T> buffer, ReadOnlySpan<T> items)
        where T : unmanaged
    {
        if (buffer.Append(items) is { } replaced)
        {
            _retired.Add((_recorded, replaced));
        }

        return buffer.Buffer;
    }

    private void Sweep()
    {
        var expired = _recorded - _frames.Length;
        foreach (var (texture, entry) in _textures)
        {
            if (entry.Image.LastUsed <= expired)
            {
                entry.Image.Dispose();
                _textures.Remove(texture);
            }
        }

        _targets.RemoveAll(image =>
        {
            if (image.LastUsed > expired)
            {
                return false;
            }

            image.Dispose();
            return true;
        });

        _retired.RemoveAll(retired =>
        {
            if (retired.Record > expired)
            {
                return false;
            }

            retired.Buffer.Dispose();
            return true;
        });

        SweepRenderTargets(expired);
    }

    private void FreeResources()
    {
        foreach (var (image, _) in _textures.Values)
        {
            image.Dispose();
        }

        foreach (var image in _targets)
        {
            image.Dispose();
        }

        foreach (var (_, buffer) in _retired)
        {
            buffer.Dispose();
        }

        foreach (var (_, image) in _renderTargetImages)
        {
            image.Dispose();
        }
    }

    private void UploadTextures(CommandBuffer commandBuffer, FramePass frame)
    {
        if (_uploads.Count == 0)
        {
            return;
        }

        frame.ReserveStaging((ulong)_uploads.Sum(upload => (long)upload.Texture.Pixels.Length));
        ulong offset = 0;
        foreach (var (texture, image) in _uploads)
        {
            frame.Staging.Write<byte>(texture.Pixels, offset);
            _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.Undefined, ImageLayout.TransferDstOptimal,
                PipelineStageFlags.FragmentShaderBit, 0, PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, 0, image.MipLevels);
            var region = new BufferImageCopy
            {
                BufferOffset = offset,
                ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                ImageExtent = new Extent3D(image.Width, image.Height, 1),
            };
            _vulkan.Api.CmdCopyBufferToImage(commandBuffer, frame.Staging.Buffer, image.Image, ImageLayout.TransferDstOptimal, 1, &region);
            GenerateMips(commandBuffer, image);
            offset += (ulong)texture.Pixels.Length;
            _textures[texture] = (image, texture.DirtyVersion);
        }
    }

    private void GenerateMips(CommandBuffer commandBuffer, GpuImage image)
    {
        var last = image.MipLevels - 1;
        for (int level = 1; level <= last; level++)
        {
            _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.TransferDstOptimal, ImageLayout.TransferSrcOptimal,
                PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit, (uint)level - 1);
            var source = image.MipSize(level - 1);
            var destination = image.MipSize(level);
            var blit = new ImageBlit
            {
                SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, (uint)level - 1, 0, 1),
                DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, (uint)level, 0, 1),
            };
            blit.SrcOffsets[1] = new Offset3D((int)source.Width, (int)source.Height, 1);
            blit.DstOffsets[1] = new Offset3D((int)destination.Width, (int)destination.Height, 1);
            _vulkan.Api.CmdBlitImage(commandBuffer, image.Image, ImageLayout.TransferSrcOptimal, image.Image, ImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Linear);
        }

        if (last > 0)
        {
            _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.TransferSrcOptimal, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.TransferBit, 0, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit, 0, last);
        }

        _vulkan.Barrier(commandBuffer, image.Image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit, PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit, last);
    }

    private readonly record struct Surface(Image Image, ImageView View, Extent2D Size, Format Format);

    private readonly record struct Layer(GpuImage Image, int Slot);

    private readonly record struct Grab(GpuImage Image, int Slot);
}
