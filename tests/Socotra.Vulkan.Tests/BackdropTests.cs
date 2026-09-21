namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class BackdropTests(GpuHost gpu)
{
    private static readonly Rect Bounds = new(0, 0, 40, 40);
    private static readonly Rect LeftHalf = new(0, 0, 20, 40);

    [GpuFact]
    public void InvertFlipsWhatsBehind()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.Fill = Color.Black;
            painter.Rect(LeftHalf);
            painter.FilterBackdrop(new Rect(10, 10, 20, 20), new Painter.Filter { Invert = 1 });
        });

        image.Expect(15, 20, Rgba.White);
        image.Expect(25, 20, Rgba.Black);
        image.Expect(5, 20, Rgba.Black);
        image.Expect(35, 20, Rgba.White);
        image.Expect(15, 5, Rgba.Black);
    }

    [GpuFact]
    public void BlurSmoothsTheEdgeBehind()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.Fill = Color.Black;
            painter.Rect(LeftHalf);
            painter.FilterBackdrop(new Rect(4, 4, 32, 32), new Painter.Filter { Blur = 8 });
        });

        var left = image[19, 20].R;
        var right = image[20, 20].R;
        Assert.InRange(left, 40, 127);
        Assert.InRange(right, 128, 215);
        Assert.InRange(left + right, 253, 257);
        image.Expect(2, 20, Rgba.Black);
        image.Expect(37, 20, Rgba.White);
    }

    [GpuFact]
    public void RoundedBackdropKeepsItsCorners()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.FilterBackdrop(Bounds, new Painter.Filter { Invert = 1 }, 20);
        });

        image.Expect(1, 1, Rgba.White);
        image.Expect(38, 38, Rgba.White);
        image.Expect(20, 20, Rgba.Black);
    }

    [GpuFact]
    public void TintColorsWhatsBehind()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.FilterBackdrop(new Rect(0, 0, 20, 40), new Painter.Filter { Tint = new Color(1, 0, 0) });
            painter.FilterBackdrop(new Rect(20, 0, 20, 40), new Painter.Filter { Invert = 1, Tint = new Color(1, 1, 1, 0.5f) });
        });

        image.Expect(10, 20, Rgba.Red);
        image.Expect(30, 20, Rgba.Of(0.5f, 0.5f, 0.5f), 1);
    }

    [GpuFact]
    public void BackdropsCanShareOneGrab()
    {
        var list = new DrawList();
        using (var painter = Painter.Begin(list, Bounds))
        {
            painter.Clear(Color.White);
            painter.SetViewport(Bounds);
            painter.Fill = Color.Black;
            painter.Rect(new Rect(0, 0, 40, 10));
            painter.FilterBackdrop(new Rect(0, 0, 20, 20), new Painter.Filter { Invert = 1 }, BorderRadii.Zero);
            painter.Rect(new Rect(30, 30, 10, 10));
            painter.FilterBackdrop(new Rect(23, 0, 10, 20), new Painter.Filter { Invert = 1 }, BorderRadii.Zero);
        }

        Assert.Equal([false, true], list.Commands.OfType<BackdropCommand>().Select(command => command.ReuseGrab));
        var image = gpu.Render(list, 40, 40);

        image.Expect(5, 5, Rgba.White);
        image.Expect(5, 15, Rgba.Black);
        image.Expect(21, 5, Rgba.Black);
        image.Expect(25, 5, Rgba.White);
        image.Expect(25, 15, Rgba.Black);
        image.Expect(35, 5, Rgba.Black);
        image.Expect(35, 35, Rgba.Black);
        image.Expect(35, 20, Rgba.White);
    }

    [GpuFact]
    public void BackdropInsideALayerSeesTheLayer()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(new Color(1, 0, 0));
            using (painter.BeginLayer(Bounds))
            {
                painter.Fill = Color.Black;
                painter.Rect(LeftHalf);
                painter.FilterBackdrop(new Rect(10, 10, 20, 20), new Painter.Filter { Invert = 1 });
            }
        });

        image.Expect(15, 20, Rgba.White);
        image.Expect(25, 20, Rgba.White);
        image.Expect(5, 20, Rgba.Black);
        image.Expect(35, 20, Rgba.Red);
    }
}
