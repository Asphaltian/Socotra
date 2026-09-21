namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class TextTests
{
    private readonly GpuHost _gpu;

    public TextTests(GpuHost gpu)
    {
        _gpu = gpu;
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
    }

    [GpuFact]
    public void GlyphsCoverOnlyTheMeasuredArea()
    {
        var style = new TextStyle("Lato", 40, Color.White);
        var area = new Rect(10, 10, 180, 60);
        var measured = default(Rect);
        var image = _gpu.Render(200, 80, painter =>
        {
            painter.Clear(Color.Black);
            painter.TextStyle = style;
            measured = painter.MeasureText("Hello", area);
            painter.Text("Hello", area);
        });

        var bounds = measured.Grow(1);
        var lit = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image[x, y].R > 16)
                {
                    lit++;
                    Assert.True(bounds.IsInside(new Vector2(x, y)), $"Lit pixel at {x}, {y} is outside {measured}.");
                }
            }
        }

        Assert.InRange(lit, 300, (int)(measured.Width * measured.Height * 0.6f));
    }

    [GpuFact]
    public void GlyphStemsAreSolid()
    {
        var image = _gpu.Render(100, 200, painter =>
        {
            painter.Clear(Color.Black);
            painter.TextStyle = new TextStyle("Lato", 96, new Color(0, 1, 0)).WithAlignment(TextFlag.Center);
            painter.Text("l", new Rect(0, 0, 100, 200));
        });

        Assert.Equal(255, Enumerable.Range(0, 100).Max(x => image[x, 100].G));
        Assert.Equal(0, Enumerable.Range(0, 100).Max(x => image[x, 100].R));
        Assert.Equal(0, image[5, 100].G);
    }

    [GpuFact]
    public void LinesTallerThanTheRectangleAreLeftOutUnlessNotClipped()
    {
        Snapshot Draw(TextFlag flags) => _gpu.Render(100, 200, painter =>
        {
            painter.Clear(Color.Black);
            painter.TextStyle = new TextStyle("Lato", 96, Color.White).WithAlignment(flags);
            painter.Text("l", new Rect(0, 50, 100, 100));
        });

        static int Lit(Snapshot image) => Enumerable.Range(0, 200).Sum(y => Enumerable.Range(0, 100).Count(x => image[x, y].R > 16));

        Assert.Equal(0, Lit(Draw(TextFlag.Center)));
        Assert.True(Lit(Draw(TextFlag.Center | TextFlag.DontClip)) > 300);
    }
}
