namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class ShapeTests(GpuHost gpu)
{
    private static readonly Color Red = new(1, 0, 0);

    [GpuFact]
    public void CircleFillsItsRadius()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = new Color(0, 1, 0);
            painter.Circle(new Vector2(20), 10);
        });

        image.Expect(20, 20, Rgba.Green);
        image.Expect(27, 20, Rgba.Green);
        image.Expect(20, 8, Rgba.Black);
        image.Expect(27, 27, Rgba.Black);
    }

    [GpuFact]
    public void PolygonFillsInsideItsEdges()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Red;
            painter.Triangle(new Vector2(0, 0), new Vector2(40, 0), new Vector2(0, 40));
        });

        image.Expect(5, 5, Rgba.Red);
        image.Expect(30, 5, Rgba.Red);
        image.Expect(5, 30, Rgba.Red);
        image.Expect(25, 25, Rgba.Black);
        image.Expect(35, 35, Rgba.Black);
    }

    [GpuFact]
    public void LineStrokesItsWidth()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Stroke = new Stroke(Red, 4);
            painter.Line(new Vector2(4, 20), new Vector2(36, 20));
        });

        image.Expect(20, 18, Rgba.Red);
        image.Expect(20, 21, Rgba.Red);
        image.Expect(20, 16, Rgba.Black);
        image.Expect(20, 23, Rgba.Black);
        image.Expect(2, 20, Rgba.Black);
        image.Expect(37, 20, Rgba.Black);
    }

    [GpuFact]
    public void RingLeavesItsMiddleEmpty()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Red;
            painter.Ring(new Vector2(20), 8, 16);
        });

        image.Expect(20, 20, Rgba.Black);
        image.Expect(31, 20, Rgba.Red);
        image.Expect(20, 8, Rgba.Red);
        image.Expect(38, 20, Rgba.Black);
    }

    [GpuFact]
    public void OutlineSurroundsTheRectangle()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Stroke = Stroke.Solid(Red, 2);
            painter.Outline(new Rect(10, 10, 20, 20), 0, 2);
        });

        image.Expect(6, 20, Rgba.Red);
        image.Expect(7, 20, Rgba.Red);
        image.Expect(9, 20, Rgba.Black);
        image.Expect(5, 20, Rgba.Black);
        image.Expect(20, 20, Rgba.Black);
    }

    [GpuFact]
    public void SharpShadowFallsOutsideItsRectangle()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.RectShadow(new Rect(10, 10, 20, 20), color: Color.Black, offset: new Vector2(5, 5));
        });

        image.Expect(32, 32, Rgba.Black);
        image.Expect(20, 32, Rgba.Black);
        image.Expect(32, 20, Rgba.Black);
        image.Expect(20, 20, Rgba.White);
        image.Expect(12, 32, Rgba.White);
        image.Expect(36, 36, Rgba.White);
    }

    [GpuFact]
    public void InsetShadowFallsInsideItsRectangle()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.RectShadow(new Rect(10, 10, 20, 20), color: Red, offset: new Vector2(5, 0), inset: true);
        });

        image.Expect(12, 20, Rgba.Red);
        image.Expect(20, 20, Rgba.White);
        image.Expect(5, 20, Rgba.White);
    }

    [GpuFact]
    public void BlurredShadowFadesLikeAGaussian()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.White);
            painter.RectShadow(new Rect(10, 10, 20, 20), color: Color.Black, blur: 10);
        });

        foreach (var x in new[] { 31, 34, 38 })
        {
            var alpha = Gaussian.Coverage(x + 0.5f, 10, 30, 5) * Gaussian.Coverage(20.5f, 10, 30, 5);
            image.Expect(x, 20, Rgba.Of(1 - alpha, 1 - alpha, 1 - alpha), 4);
        }

        image.Expect(20, 20, Rgba.White);
    }

    [GpuFact]
    public void CapsuleRoundsBothEnds()
    {
        var image = gpu.Render(40, 20, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Red;
            painter.Capsule(new Vector2(10, 10), new Vector2(30, 10), 6, 6);
        });

        image.Expect(20, 10, Rgba.Red);
        image.Expect(5, 10, Rgba.Red);
        image.Expect(34, 10, Rgba.Red);
        image.Expect(20, 3, Rgba.Black);
        image.Expect(4, 4, Rgba.Black);
        image.Expect(38, 10, Rgba.Black);
    }
}
