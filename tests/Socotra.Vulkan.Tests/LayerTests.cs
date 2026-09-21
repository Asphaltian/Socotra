namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class LayerTests(GpuHost gpu)
{
    private static readonly Color Red = new(1, 0, 0);
    private static readonly Rect Bounds = new(0, 0, 40, 40);

    [GpuFact]
    public void LayerOpacityFadesTheGroup()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.BeginLayer(Bounds, 0.5f))
            {
                painter.Fill = Red;
                painter.Rect(new Rect(10, 10, 20, 20));
                painter.Rect(new Rect(15, 15, 20, 20));
            }
        });

        image.Expect(20, 20, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(12, 12, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(5, 5, Rgba.Black);
    }

    [GpuFact]
    public void NestedLayersMultiplyTheirOpacity()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.BeginLayer(Bounds, 0.5f))
            {
                using (painter.BeginLayer(new Rect(10, 10, 20, 20), 0.5f))
                {
                    painter.Fill = Red;
                    painter.Rect(Bounds);
                }
            }
        });

        image.Expect(20, 20, Rgba.Of(0.25f, 0, 0), 1);
        image.Expect(5, 20, Rgba.Black);
    }

    [GpuFact]
    public void LayerCutsOffDrawingOutsideItsBounds()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.BeginLayer(new Rect(10, 10, 20, 20)))
            {
                painter.Fill = Red;
                painter.Rect(Bounds);
            }
        });

        image.Expect(15, 15, Rgba.Red);
        image.Expect(29, 29, Rgba.Red);
        image.Expect(5, 5, Rgba.Black);
        image.Expect(35, 20, Rgba.Black);
    }

    [GpuFact]
    public void BlurSoftensTheEdges()
    {
        var image = RenderFiltered(new Painter.Filter { Blur = 3 }, Color.Black);

        image.Expect(20, 20, Rgba.Red, 3);
        image.Expect(1, 20, Rgba.Black, 3);
        var edge = image[10, 20].R;
        Assert.InRange(edge, 110, 170);
        Assert.InRange(image[8, 20].R, 20, edge - 20);
    }

    [GpuFact]
    public void BrightnessScalesTheColor() => RenderFiltered(new Painter.Filter { Brightness = 0.5f }, Color.Black).Expect(20, 20, Rgba.Of(0.5f, 0, 0), 1);

    [GpuFact]
    public void InvertFlipsTheColor() => RenderFiltered(new Painter.Filter { Invert = 1 }, Color.Black).Expect(20, 20, new Rgba(0, 255, 255));

    [GpuFact]
    public void ZeroSaturationGreysTheColor() => RenderFiltered(new Painter.Filter { Saturation = 0 }, Color.Black).Expect(20, 20, Rgba.Of(0.213f, 0.213f, 0.213f));

    [GpuFact]
    public void HueRotationTurnsTheHue() => RenderFiltered(new Painter.Filter { HueRotation = 180 }, Color.Black).Expect(20, 20, Rgba.Of(0, 0.426f, 0.426f));

    [GpuFact]
    public void SepiaTonesTheColor() => RenderFiltered(new Painter.Filter { Sepia = 1 }, Color.Black).Expect(20, 20, Rgba.Of(0.393f, 0.349f, 0.272f));

    [GpuFact]
    public void ContrastPullsTowardGrey() => RenderFiltered(new Painter.Filter { Contrast = 0.5f }, Color.Black).Expect(20, 20, Rgba.Of(0.75f, 0.25f, 0.25f));

    [GpuFact]
    public void TintMultipliesInLinearLight()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.BeginLayer(Bounds, filter: new Painter.Filter { Tint = new Color(0.5f, 1, 1) }))
            {
                painter.Fill = Color.White;
                painter.Rect(Bounds);
            }
        });

        image.Expect(20, 20, new Rgba(188, 255, 255));
    }

    [GpuFact]
    public void AlphaMaskHidesTransparentParts()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 255, 255, 255, 0]);
        var image = RenderMasked(new Painter.Mask(mask, Bounds, MaskMode.Alpha, Sampling: FilterMode.Point));

        image.Expect(5, 20, Rgba.Red);
        image.Expect(30, 20, Rgba.Black);
    }

    [GpuFact]
    public void LuminanceMaskHidesDarkParts()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 0, 0, 0, 255]);
        var image = RenderMasked(new Painter.Mask(mask, Bounds, MaskMode.Luminance, Sampling: FilterMode.Point));

        image.Expect(5, 20, Rgba.Red);
        image.Expect(30, 20, Rgba.Black);
    }

    [GpuFact]
    public void RepeatedMaskTiles()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 255, 255, 255, 0]);
        var image = RenderMasked(new Painter.Mask(mask, new Rect(0, 0, 10, 40), MaskMode.Alpha, BackgroundRepeat.Repeat, Sampling: FilterMode.Point));

        image.Expect(2, 20, Rgba.Red);
        image.Expect(7, 20, Rgba.Black);
        image.Expect(12, 20, Rgba.Red);
        image.Expect(37, 20, Rgba.Black);
    }

    [GpuFact]
    public void LayerCompositesWithItsBlendMode()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Red);
            painter.BlendMode = BlendMode.Multiply;
            using (painter.BeginLayer(Bounds))
            {
                painter.Fill = new Color(0.5f, 0.5f, 0.5f);
                painter.Rect(Bounds);
            }
        });

        image.Expect(20, 20, Rgba.Of(0.5f, 0, 0), 1);
    }

    [GpuFact]
    public void DropShadowCopiesTheLayersShape()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            var target = painter.Target(Bounds);
            painter.Fill = Red;
            painter.Rect(new Rect(10, 10, 10, 10));
            target.Dispose();
            painter.Composite(target.Layer, Bounds, new Painter.Filter(), null, MaskScope.Default,
                [new Shadow(10, 0, 0, 0, new Color(0, 0, 0.5f), false)], 0, default);
        });

        image.Expect(15, 15, Rgba.Red);
        image.Expect(25, 15, Rgba.Of(0, 0, 0.5f), 1);
        image.Expect(35, 15, Rgba.White);
        image.Expect(15, 25, Rgba.White);
    }

    [GpuFact]
    public void BlurredDropShadowSpreads()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            var target = painter.Target(Bounds);
            painter.Fill = Red;
            painter.Rect(new Rect(10, 10, 10, 10));
            target.Dispose();
            painter.Composite(target.Layer, Bounds, new Painter.Filter(), null, MaskScope.Default,
                [new Shadow(0, 10, 2, 0, Color.Black, false)], 0, default);
        });

        image.Expect(15, 15, Rgba.Red);
        foreach (var y in new[] { 25, 30, 33 })
        {
            var alpha = Gaussian.Coverage(15.5f, 10, 20, 2) * Gaussian.Coverage(y + 0.5f, 20, 30, 2);
            image.Expect(15, y, Rgba.Of(1 - alpha, 1 - alpha, 1 - alpha), 5);
        }
    }

    [GpuFact]
    public void BorderWrapOutlinesTheLayersShape()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            var target = painter.Target(Bounds);
            painter.Fill = Red;
            painter.Rect(new Rect(10, 10, 20, 20));
            target.Dispose();
            painter.Composite(target.Layer, Bounds, new Painter.Filter(), null, MaskScope.Default, [], 3, new Color(0.5f, 0.5f, 1));
        });

        image.Expect(8, 20, Rgba.Of(0.5f, 0.5f, 1), 1);
        image.Expect(31, 20, Rgba.Of(0.5f, 0.5f, 1), 1);
        image.Expect(20, 20, Rgba.Red);
        image.Expect(4, 20, Rgba.White);
    }

    [GpuFact]
    public void LayerFollowsTheTransform()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Translate(10, 10);
            using (painter.BeginLayer(new Rect(0, 0, 10, 10), 0.5f))
            {
                painter.Fill = Red;
                painter.Rect(new Rect(0, 0, 10, 10));
            }
        });

        image.Expect(15, 15, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(5, 5, Rgba.Black);
        image.Expect(22, 15, Rgba.Black);
    }

    [GpuFact]
    public void ClipCutsTheLayer()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Clip(new Rect(0, 0, 20, 40));
            using (painter.BeginLayer(Bounds, filter: new Painter.Filter { Brightness = 0.5f }))
            {
                painter.Fill = Red;
                painter.Rect(Bounds);
            }
        });

        image.Expect(10, 20, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(30, 20, Rgba.Black);
    }

    [GpuFact]
    public void FilterScopedMaskBlendsFilteredAndOriginal()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 255, 255, 255, 0]);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            var target = painter.Target(Bounds);
            painter.Fill = Red;
            painter.Rect(Bounds);
            target.Dispose();
            painter.Composite(target.Layer, Bounds, new Painter.Filter { Invert = 1 },
                new Painter.Mask(mask, Bounds, MaskMode.Alpha, Sampling: FilterMode.Point), MaskScope.Filter, [], 0, default);
        });

        image.Expect(10, 20, new Rgba(0, 255, 255));
        image.Expect(30, 20, Rgba.Red);
    }

    [GpuFact]
    public void OffsetTargetMapsItsBoundsOntoItsImage()
    {
        var bounds = new Rect(10, 10, 20, 20);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            var target = painter.Target(bounds);
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 20, 40));
            target.Dispose();
            painter.Composite(target.Layer, bounds, new Painter.Filter { Brightness = 0.5f }, null, MaskScope.Default, [], 0, default);
        });

        image.Expect(12, 20, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(19, 12, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(22, 20, Rgba.Black);
        image.Expect(5, 20, Rgba.Black);
        image.Expect(15, 5, Rgba.Black);
    }

    private Snapshot RenderFiltered(Painter.Filter filter, Color background) => gpu.Render(40, 40, painter =>
    {
        painter.Clear(background);
        using (painter.BeginLayer(Bounds, filter: filter))
        {
            painter.Fill = Red;
            painter.Rect(new Rect(10, 10, 20, 20));
        }
    });

    private Snapshot RenderMasked(Painter.Mask mask) => gpu.Render(40, 40, painter =>
    {
        painter.Clear(Color.Black);
        using (painter.BeginLayer(Bounds, mask: mask))
        {
            painter.Fill = Red;
            painter.Rect(Bounds);
        }
    });
}
