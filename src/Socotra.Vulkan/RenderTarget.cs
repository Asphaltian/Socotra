using Silk.NET.Vulkan;

namespace Socotra.Vulkan;

using Image = Silk.NET.Vulkan.Image;

/// <summary>
/// <c>RenderTarget</c> is the image that a <see cref="VulkanRenderer"/> draws into, like a swapchain image. Make sure you
/// use a UNORM format, like <see cref="Format.B8G8R8A8Unorm"/> or <see cref="Format.R8G8B8A8Unorm"/>, and create the
/// image with <see cref="ImageUsageFlags.ColorAttachmentBit"/>. If your UI uses <c>backdrop-filter</c> or
/// <see cref="Painter.FilterBackdrop(Rect, Painter.Filter, Painter.CornerRadii)"/>, don't forget to add
/// <see cref="ImageUsageFlags.TransferSrcBit"/> too.
/// </summary>
/// <param name="Image">The image.</param>
/// <param name="View">A view of the image's first mip level, in <paramref name="Format"/>.</param>
/// <param name="Format">The image's format.</param>
/// <param name="Size">The image's size in pixels. You draw in these pixels.</param>
public readonly record struct RenderTarget(Image Image, ImageView View, Format Format, Extent2D Size);
