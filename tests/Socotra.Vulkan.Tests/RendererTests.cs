using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class RendererTests(GpuHost gpu)
{
    [GpuFact]
    public void ClearFillsTheTarget()
    {
        var image = gpu.Render(8, 8, painter => painter.Clear(new Color(0.2f, 0.4f, 0.6f)));

        image.Expect(0, 0, Rgba.Of(0.2f, 0.4f, 0.6f), 1);
        image.Expect(7, 7, Rgba.Of(0.2f, 0.4f, 0.6f), 1);
    }

    [GpuFact]
    public void ClearPremultipliesItsColor()
    {
        var image = gpu.Render(4, 4, painter => painter.Clear(new Color(1, 0, 0, 0.5f)));

        image.Expect(2, 2, new Rgba(128, 0, 0, 128), 1);
    }

    [GpuFact]
    public void RectangleCoversItsPixels()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = new Color(1, 0, 0);
            painter.Rect(new Rect(10, 10, 20, 20));
        });

        image.Expect(10, 10, Rgba.Red);
        image.Expect(29, 29, Rgba.Red);
        image.Expect(9, 15, Rgba.Black);
        image.Expect(30, 15, Rgba.Black);
        image.Expect(15, 30, Rgba.Black);
    }

    [GpuFact]
    public void DrawsIntoBgraTargets()
    {
        var image = gpu.Render(16, 16, painter =>
        {
            painter.Clear(new Color(0, 0, 1));
            painter.Fill = new Color(1, 0.5f, 0);
            painter.Rect(new Rect(0, 0, 8, 16));
        }, Format.B8G8R8A8Unorm);

        image.Expect(2, 8, Rgba.Of(1, 0.5f, 0));
        image.Expect(12, 8, Rgba.Blue);
    }

    [GpuFact]
    public void BackdropsAndLayersWorkOnBgraTargets()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(new Color(0, 0, 1));
            using (painter.BeginLayer(new Rect(0, 0, 20, 40), 0.5f))
            {
                painter.Fill = new Color(1, 0, 0);
                painter.Rect(new Rect(0, 0, 40, 40));
            }

            painter.FilterBackdrop(new Rect(20, 0, 20, 40), new Painter.Filter { Invert = 1 });
        }, Format.B8G8R8A8Unorm);

        image.Expect(10, 20, Rgba.Of(0.5f, 0, 0.5f), 1);
        image.Expect(30, 20, new Rgba(255, 255, 0));
    }

    [GpuFact]
    public void FramesInFlightTakeTurns()
    {
        for (int frame = 0; frame < GpuHost.FramesInFlight * 3; frame++)
        {
            var size = 24 + (frame * 4);
            var shade = (frame + 1) / 8f;
            var image = gpu.Render(size, size, painter =>
            {
                painter.Clear(Color.Black);
                using (painter.BeginLayer(new Rect(0, 0, size, size), filter: new Painter.Filter { Blur = frame % 2 }))
                {
                    painter.Fill = new Color(shade, shade, shade);
                    painter.Rect(new Rect(0, 0, size, size));
                }

                painter.FilterBackdrop(new Rect(0, 0, size / 2, size), new Painter.Filter { Invert = 1 });
            });

            image.Expect(size - 4, size / 2, Rgba.Of(shade, shade, shade), 1);
            image.Expect(4, size / 2, Rgba.Of(1 - shade, 1 - shade, 1 - shade), 1);
        }
    }

    [GpuFact]
    public void OneDrawListCanBeRecordedAgain()
    {
        var list = new DrawList();
        using (var painter = Painter.Begin(list, new Rect(0, 0, 16, 16)))
        {
            painter.Clear(Color.Black);
            painter.Fill = new Color(0, 1, 0);
            painter.Rect(new Rect(4, 4, 8, 8));
        }

        for (int i = 0; i < 3; i++)
        {
            var image = gpu.Render(list, 16, 16);
            image.Expect(8, 8, Rgba.Green);
            image.Expect(1, 1, Rgba.Black);
        }
    }

    [Fact]
    public void InstanceLayoutsMatchTheShaders()
    {
        Assert.Equal(272, Unsafe.SizeOf<BoxInstance>());
        Assert.Equal(292, Unsafe.SizeOf<TextInstance>());
        Assert.Equal(464, Unsafe.SizeOf<ScissorInstance>());
        Assert.Equal(64, Unsafe.SizeOf<Matrix4x4>());
        Assert.Equal(200, Unsafe.SizeOf<GradientInstance>());
        Assert.Equal(104, Unsafe.SizeOf<ShapeInstance>());
        Assert.Equal(56, Unsafe.SizeOf<PathPrimitive>());
        Assert.Equal(24, Unsafe.SizeOf<PathNode>());
        Assert.Equal(36, Unsafe.SizeOf<GpuFontGlyphCache.Glyph>());
        Assert.Equal(144, (int)Marshal.OffsetOf<BoxInstance>(nameof(BoxInstance.TextureIndex)));
        Assert.Equal(228, (int)Marshal.OffsetOf<BoxInstance>(nameof(BoxInstance.Flags)));
        Assert.Equal(252, (int)Marshal.OffsetOf<BoxInstance>(nameof(BoxInstance.BackgroundClipRect)));
        Assert.Equal(268, (int)Marshal.OffsetOf<BoxInstance>(nameof(BoxInstance.ShapeIndex)));
        Assert.Equal(288, (int)Marshal.OffsetOf<TextInstance>(nameof(TextInstance.ShapeIndex)));
        Assert.Equal(160, (int)Marshal.OffsetOf<GradientInstance>(nameof(GradientInstance.Count)));
        Assert.Equal(196, (int)Marshal.OffsetOf<GradientInstance>(nameof(GradientInstance.Corner)));
        Assert.Equal(16, (int)Marshal.OffsetOf<ScissorInstance>(nameof(ScissorInstance.Clips)));
    }

    [Fact]
    public void ConstantLayoutsMatchTheShaders()
    {
        Assert.Equal(84, Unsafe.SizeOf<BoxConstants>());
        Assert.Equal(4, Unsafe.SizeOf<DownsampleConstants>());
        Assert.Equal(188, Unsafe.SizeOf<QuadDraw>());
        Assert.Equal(280, Unsafe.SizeOf<FilterConstants>());
        Assert.Equal(256, Unsafe.SizeOf<BackdropConstants>());
        Assert.Equal(228, Unsafe.SizeOf<DropShadowConstants>());
        Assert.Equal(220, Unsafe.SizeOf<BorderWrapConstants>());
        Assert.Equal(264, (int)Marshal.OffsetOf<FilterConstants>(nameof(FilterConstants.MaskPos)));
        Assert.Equal(252, (int)Marshal.OffsetOf<BackdropConstants>(nameof(BackdropConstants.FrameBufferCopyTextureIndex)));
    }

    [GpuFact]
    public void GlyphBuffersGrowOnlyWhenFull()
    {
        var device = new VulkanDevice(gpu.Api, gpu.PhysicalDevice, gpu.Device);
        using var buffer = new AppendBuffer<uint>(device);
        var items = Enumerable.Range(0, 200).Select(i => (uint)i).ToArray();

        Assert.Null(buffer.Append(items.AsSpan(0, 10)));
        Assert.Null(buffer.Append(items.AsSpan(0, 64)));
        var replaced = buffer.Append(items);

        Assert.NotNull(replaced);
        Assert.True(buffer.Buffer.Size >= 200 * sizeof(uint));
        replaced.Dispose();
    }
}
