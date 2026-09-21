using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

using Image = Silk.NET.Vulkan.Image;

/// <summary>
/// The image a <see cref="VulkanRenderer"/> draws into, like a swapchain image. Use a UNORM format, like
/// <see cref="Format.B8G8R8A8Unorm"/> or <see cref="Format.R8G8B8A8Unorm"/>, and create the image with
/// <see cref="ImageUsageFlags.ColorAttachmentBit"/>. Add <see cref="ImageUsageFlags.TransferSrcBit"/> too if your UI
/// uses <c>backdrop-filter</c> or <see cref="Painter.FilterBackdrop(Rect, Painter.Filter, Painter.CornerRadii)"/>.
/// </summary>
/// <param name="Image">The image.</param>
/// <param name="View">A view of the image's first mip level, in <paramref name="Format"/>.</param>
/// <param name="Format">The image's format.</param>
/// <param name="Size">The image's size in pixels. You draw in these pixels.</param>
public readonly record struct RenderTarget(Image Image, ImageView View, Format Format, Extent2D Size);
