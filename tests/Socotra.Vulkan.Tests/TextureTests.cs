namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class TextureTests(GpuHost gpu)
{
    private static Texture Quadrants() => Texture.FromPixels(2, 2, [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);

    [GpuFact]
    public void TextureStretchesOverTheRectangle()
    {
        var texture = Quadrants();
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Texture(texture, new Rect(0, 0, 40, 40));
        });

        image.Expect(3, 3, Rgba.Red);
        image.Expect(36, 3, Rgba.Green);
        image.Expect(3, 36, Rgba.Blue);
        image.Expect(36, 36, Rgba.White);
        image.Expect(19, 3, Rgba.Of(1 - (19.5f / 20 - 0.5f), 19.5f / 20 - 0.5f, 0));
    }

    [GpuFact]
    public void TintMultipliesTheTexture()
    {
        var texture = Texture.FromPixels(1, 1, [255, 255, 255, 255]);
        var image = gpu.Render(8, 8, painter =>
        {
            painter.Clear(Color.Black);
            painter.Texture(texture, new Rect(0, 0, 8, 8), new Color(1, 0.5f, 0));
        });

        image.Expect(4, 4, Rgba.Of(1, 0.5f, 0));
    }

    [GpuFact]
    public void RepeatTilesBothWays()
    {
        var image = RenderTiles(BackgroundRepeat.Repeat);

        image.Expect(2, 2, Rgba.Red);
        image.Expect(7, 2, Rgba.Green);
        image.Expect(2, 7, Rgba.Blue);
        image.Expect(7, 7, Rgba.White);
        image.Expect(12, 2, Rgba.Red);
        image.Expect(2, 12, Rgba.Red);
        image.Expect(37, 37, Rgba.White);
    }

    [GpuFact]
    public void NoRepeatDrawsOneTile()
    {
        var image = RenderTiles(BackgroundRepeat.NoRepeat);

        image.Expect(2, 2, Rgba.Red);
        image.Expect(7, 7, Rgba.White);
        image.Expect(12, 2, Rgba.Black);
        image.Expect(2, 12, Rgba.Black);
        image.Expect(30, 30, Rgba.Black);
    }

    [GpuFact]
    public void RepeatXTilesAcrossOnly()
    {
        var image = RenderTiles(BackgroundRepeat.RepeatX);

        image.Expect(12, 2, Rgba.Red);
        image.Expect(37, 7, Rgba.White);
        image.Expect(2, 12, Rgba.Black);
    }

    [GpuFact]
    public void RepeatYTilesDownOnly()
    {
        var image = RenderTiles(BackgroundRepeat.RepeatY);

        image.Expect(2, 12, Rgba.Red);
        image.Expect(7, 37, Rgba.White);
        image.Expect(12, 2, Rgba.Black);
    }

    [GpuFact]
    public void ClampStretchesTheEdgeTexels()
    {
        var image = RenderTiles(BackgroundRepeat.Clamp);

        image.Expect(2, 2, Rgba.Red);
        image.Expect(30, 2, Rgba.Green);
        image.Expect(2, 30, Rgba.Blue);
        image.Expect(30, 30, Rgba.White);
    }

    [GpuFact]
    public void RotatedImageTurnsAroundItsMiddle()
    {
        var texture = Quadrants();
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.Image(texture, filter: FilterMode.Point).WithRotation(90);
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(5, 5, Rgba.Green);
        image.Expect(34, 5, Rgba.White);
        image.Expect(5, 34, Rgba.Red);
        image.Expect(34, 34, Rgba.Blue);
    }

    [GpuFact]
    public void PremultipliedTexturesBlendAsPremultiplied()
    {
        var texture = Texture.FromPixels(1, 1, [128, 0, 0, 128], premultipliedAlpha: true);
        var image = gpu.Render(8, 8, painter =>
        {
            painter.Clear(Color.White);
            painter.Texture(texture, new Rect(0, 0, 8, 8));
        });

        image.Expect(4, 4, new Rgba(255, 127, 127));
    }

    [GpuFact]
    public void UpdatedTexturesUploadAgain()
    {
        var texture = Texture.FromPixels(2, 1, [255, 0, 0, 255, 255, 0, 0, 255]);
        void Paint(Painter painter)
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.Image(texture, filter: FilterMode.Point);
            painter.Rect(new Rect(0, 0, 8, 8));
        }

        gpu.Render(8, 8, Paint).Expect(6, 4, Rgba.Red);
        texture.Update([0, 0, 255, 255], x: 1, width: 1, height: 1);
        var image = gpu.Render(8, 8, Paint);

        image.Expect(1, 4, Rgba.Red);
        image.Expect(6, 4, Rgba.Blue);
    }

    [GpuFact]
    public void PaintingARenderTargetKeepsWhatWasThereAndShowsWhenDrawn()
    {
        var target = Texture.CreateRenderTarget(8, 8);
        using (var painter = Painter.Begin(target))
        {
            painter.Fill = new Color(1, 0, 0);
            painter.Rect(new Rect(0, 0, 4, 8));
        }

        using (var painter = Painter.Begin(target))
        {
            painter.Fill = new Color(0, 0, 1);
            painter.Rect(new Rect(4, 0, 4, 8));
        }

        var image = gpu.Render(8, 8, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.Image(target, filter: FilterMode.Point);
            painter.Rect(new Rect(0, 0, 8, 8));
        });

        image.Expect(1, 4, Rgba.Red);
        image.Expect(6, 4, Rgba.Blue);
    }

    [GpuFact]
    public void AnUnpaintedRenderTargetIsTransparent()
    {
        var target = Texture.CreateRenderTarget(4, 4);

        var image = gpu.Render(4, 4, painter =>
        {
            painter.Clear(new Color(0, 1, 0));
            painter.Texture(target, new Rect(0, 0, 4, 4));
        });

        image.Expect(2, 2, Rgba.Green);
    }

    [GpuFact]
    public void TranslucentPaintIsPremultiplied()
    {
        var target = Texture.CreateRenderTarget(4, 4);
        using (var painter = Painter.Begin(target))
        {
            painter.Fill = new Color(1, 0, 0, 0.5f);
            painter.Rect(new Rect(0, 0, 4, 4));
        }

        var image = gpu.Render(4, 4, painter =>
        {
            painter.Clear(Color.Black);
            painter.Texture(target, new Rect(0, 0, 4, 4));
        });

        image.Expect(2, 2, Rgba.Of(0.5f, 0, 0), 3);
    }

    [GpuFact]
    public void RenderTargetsCanBePaintedWithOtherRenderTargets()
    {
        var inner = Texture.CreateRenderTarget(4, 4);
        using (var painter = Painter.Begin(inner))
        {
            painter.Clear(new Color(1, 0, 0));
        }

        var outer = Texture.CreateRenderTarget(8, 8);
        using (var painter = Painter.Begin(outer))
        {
            painter.Clear(new Color(0, 0, 1));
            painter.Texture(inner, new Rect(0, 0, 4, 4));
        }

        var image = gpu.Render(8, 8, painter => painter.Texture(outer, new Rect(0, 0, 8, 8)));

        image.Expect(1, 1, Rgba.Red);
        image.Expect(6, 6, Rgba.Blue);
    }

    [Fact]
    public void OnlyRenderTargetsArePaintedAndThoseArentUpdated()
    {
        Assert.Throws<InvalidOperationException>(() => Painter.Begin(Texture.FromPixels(1, 1, [0, 0, 0, 255])).Dispose());
        Assert.Throws<InvalidOperationException>(() => Texture.CreateRenderTarget(1, 1).Update([0, 0, 0, 255]));
    }

    [GpuFact]
    public void TexturesLeftOutOfSomeFramesDrawAgainWhenTheyComeBack()
    {
        var texture = Texture.FromPixels(1, 1, [255, 0, 0, 255]);
        void Paint(Painter painter)
        {
            painter.Clear(Color.Black);
            painter.Texture(texture, new Rect(0, 0, 8, 8));
        }

        gpu.Render(8, 8, Paint).Expect(4, 4, Rgba.Red);
        for (int frame = 0; frame <= GpuHost.FramesInFlight; frame++)
        {
            gpu.Render(8, 8, painter => painter.Clear(Color.Black));
        }

        gpu.Render(8, 8, Paint).Expect(4, 4, Rgba.Red);
        for (int frame = 0; frame <= GpuHost.FramesInFlight; frame++)
        {
            gpu.Render(8, 8, painter => painter.Clear(Color.Black));
        }

        texture.Update([0, 0, 255, 255]);
        gpu.Render(8, 8, Paint).Expect(4, 4, Rgba.Blue);
    }

    [GpuFact]
    public void TooManyTexturesFailWithoutBreakingTheRenderer()
    {
        var textures = Enumerable.Range(0, 4096).Select(_ => Texture.FromPixels(1, 1, [0, 255, 0, 255])).ToArray();

        Assert.Throws<InvalidOperationException>(() => gpu.Render(8, 8, painter =>
        {
            foreach (var texture in textures)
            {
                painter.Texture(texture, new Rect(0, 0, 8, 8));
            }
        }));

        gpu.Render(8, 8, painter => painter.Texture(textures[0], new Rect(0, 0, 8, 8))).Expect(4, 4, Rgba.Green);
    }

    [GpuFact]
    public void ShrunkTexturesAverageTheirMips()
    {
        var pixels = new byte[64 * 64 * 4];
        for (int i = 0; i < 64 * 64; i++)
        {
            var white = ((i % 64) + (i / 64)) % 2 == 0;
            pixels.AsSpan(i * 4, 4).Fill(white ? (byte)255 : (byte)0);
            pixels[(i * 4) + 3] = 255;
        }

        var texture = Texture.FromPixels(64, 64, pixels);
        var image = gpu.Render(4, 4, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.Image(texture, filter: FilterMode.Trilinear);
            painter.Rect(new Rect(0, 0, 4, 4));
        });

        image.Expect(1, 1, Rgba.Of(0.5f, 0.5f, 0.5f), 3);
    }

    private Snapshot RenderTiles(BackgroundRepeat repeat)
    {
        var texture = Quadrants();
        return gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.Image(texture, width: 10, height: 10, repeat: repeat, filter: FilterMode.Point);
            painter.Rect(new Rect(0, 0, 40, 40));
        });
    }
}
