namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class BlendTests(GpuHost gpu)
{
    [GpuFact]
    public void NormalBlendsByAlpha()
    {
        var image = RenderBlend(BlendMode.Normal, Color.White, new Color(0, 0, 1, 0.5f));

        image.Expect(10, 10, Rgba.Of(0.5f, 0.5f, 1), 1);
    }

    [GpuFact]
    public void MultiplyDarkens()
    {
        var image = RenderBlend(BlendMode.Multiply, new Color(1, 0.5f, 1), new Color(0.5f, 0.5f, 0.5f));

        image.Expect(10, 10, Rgba.Of(0.5f, 0.25f, 0.5f), 1);
    }

    [GpuFact]
    public void LightenAdds()
    {
        var image = RenderBlend(BlendMode.Lighten, new Color(0, 0, 1), new Color(1, 0, 0, 0.5f));

        image.Expect(10, 10, Rgba.Of(0.5f, 0, 1), 1);
    }

    [GpuFact]
    public void PremultipliedAlphaAddsTheColorAsIs()
    {
        var image = RenderBlend(BlendMode.PremultipliedAlpha, Color.White, new Color(0.5f, 0, 0, 0.5f));

        image.Expect(10, 10, Rgba.Of(1, 0.5f, 0.5f), 1);
    }

    [GpuFact]
    public void BlendModesSplitBatches()
    {
        var image = gpu.Render(40, 20, painter =>
        {
            painter.Clear(new Color(0.5f, 0.5f, 0.5f));
            painter.BlendMode = BlendMode.Multiply;
            painter.Fill = new Color(1, 0, 0);
            painter.Rect(new Rect(0, 0, 20, 20));
            painter.BlendMode = BlendMode.Lighten;
            painter.Fill = new Color(0.5f, 0, 0);
            painter.Rect(new Rect(20, 0, 20, 20));
        });

        image.Expect(10, 10, Rgba.Of(0.5f, 0, 0), 1);
        image.Expect(30, 10, Rgba.Of(1, 0.5f, 0.5f), 1);
    }

    private Snapshot RenderBlend(BlendMode mode, Color background, Color fill) => gpu.Render(20, 20, painter =>
    {
        painter.Clear(background);
        painter.BlendMode = mode;
        painter.Fill = fill;
        painter.Rect(new Rect(0, 0, 20, 20));
    });
}
