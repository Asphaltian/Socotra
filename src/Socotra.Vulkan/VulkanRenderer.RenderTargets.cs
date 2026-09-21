using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

public sealed unsafe partial class VulkanRenderer
{
    private const ImageUsageFlags RenderTargetUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit;

    private static readonly DrawList NoPaint = new();

    private readonly ConditionalWeakTable<Texture, GpuImage> _renderTargets = [];
    private readonly List<(WeakReference<Texture> Texture, GpuImage Image)> _renderTargetImages = [];

    private void PaintRenderTargets(CommandBuffer commandBuffer, Frame frame, HostBuffer[] glyphs, DrawList list)
    {
        foreach (var texture in RenderTargetsIn(list))
        {
            var created = !_renderTargets.TryGetValue(texture, out var image);
            if (image is null)
            {
                image = new GpuImage(_vulkan, (uint)texture.Width, (uint)texture.Height, Format.R8G8B8A8Unorm, 1, RenderTargetUsage);
                _renderTargets.Add(texture, image);
                _renderTargetImages.Add((new WeakReference<Texture>(texture), image));
            }

            var paints = texture.TakePaints();
            if (paints.Count == 0)
            {
                if (!created)
                {
                    continue;
                }

                paints = [NoPaint];
            }

            image.LastUsed = _recorded;
            var surface = new Surface(image.Image, image.View, image.Size, image.Format);
            for (int i = 0; i < paints.Count; i++)
            {
                PaintRenderTargets(commandBuffer, frame, glyphs, paints[i]);
                var first = created && i == 0;
                RecordPass(commandBuffer, frame.NextPass(), glyphs, paints[i], surface,
                    first ? ImageLayout.Undefined : ImageLayout.ShaderReadOnlyOptimal, ImageLayout.ShaderReadOnlyOptimal,
                    first ? AttachmentLoadOp.Clear : AttachmentLoadOp.Load);
            }
        }
    }

    private static Texture[] RenderTargetsIn(DrawList list) =>
    [
        .. list.Textures
            .Concat(list.Commands.OfType<FilterCommand>().Select(filter => filter.Mask?.Texture).OfType<Texture>())
            .Where(texture => texture.IsRenderTarget)
            .Distinct(),
    ];

    private void SweepRenderTargets(long expired) => _renderTargetImages.RemoveAll(entry =>
    {
        if (entry.Texture.TryGetTarget(out _) || entry.Image.LastUsed > expired)
        {
            return false;
        }

        entry.Image.Dispose();
        return true;
    });
}
