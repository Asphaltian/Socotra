namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class ClipTests(GpuHost gpu)
{
    private static readonly Color Red = new(1, 0, 0);

    [GpuFact]
    public void ClipCutsOffDrawingOutsideIt()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Clip(new Rect(10, 10, 20, 20));
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(10, 10, Rgba.Red);
        image.Expect(29, 29, Rgba.Red);
        image.Expect(9, 20, Rgba.Black);
        image.Expect(30, 20, Rgba.Black);
        image.Expect(20, 9, Rgba.Black);
    }

    [GpuFact]
    public void RoundedClipCutsTheCorners()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Clip(new Rect(0, 0, 40, 40), 20);
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(2, 2, Rgba.Black);
        image.Expect(37, 37, Rgba.Black);
        image.Expect(20, 20, Rgba.Red);
        image.Expect(36, 20, Rgba.Red);
    }

    [GpuFact]
    public void NestedClipsIntersect()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Clip(new Rect(0, 0, 30, 30));
            painter.Clip(new Rect(10, 10, 30, 30));
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(20, 20, Rgba.Red);
        image.Expect(5, 20, Rgba.Black);
        image.Expect(35, 20, Rgba.Black);
        image.Expect(20, 35, Rgba.Black);
    }

    [GpuFact]
    public void ScopeRestoresTheClip()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.Scope())
            {
                painter.Clip(new Rect(0, 0, 10, 10));
            }

            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(30, 30, Rgba.Red);
    }

    [GpuFact]
    public void RotatedClipIsADiamond()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Translate(20, 20);
            painter.Rotate(45);
            painter.Clip(new Rect(-10, -10, 20, 20));
            painter.Transform = Matrix4x4.Identity;
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(20, 20, Rgba.Red);
        image.Expect(20, 12, Rgba.Red);
        image.Expect(27, 20, Rgba.Red);
        image.Expect(12, 12, Rgba.Black);
        image.Expect(28, 28, Rgba.Black);
        image.Expect(20, 3, Rgba.Black);
    }

    [GpuFact]
    public void TranslateAndScaleMoveTheDrawing()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Translate(10, 10);
            painter.Scale(2);
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 10, 10));
        });

        image.Expect(10, 10, Rgba.Red);
        image.Expect(29, 29, Rgba.Red);
        image.Expect(9, 20, Rgba.Black);
        image.Expect(30, 20, Rgba.Black);
    }

    [GpuFact]
    public void RotationTurnsClockwise()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Translate(20, 20);
            painter.Rotate(90);
            painter.Fill = Red;
            painter.Rect(new Rect(0, -2, 15, 4));
        });

        image.Expect(20, 30, Rgba.Red);
        image.Expect(20, 10, Rgba.Black);
        image.Expect(30, 20, Rgba.Black);
    }

    [GpuFact]
    public void PerspectiveTransformForeshortensTheFarSide()
    {
        var perspective = Matrix4x4.Identity;
        perspective.M34 = -1f / 100;
        var transform = Matrix4x4.CreateTranslation(-20, -20, 0) * Matrix4x4.CreateRotationY(float.DegreesToRadians(60)) * perspective * Matrix4x4.CreateTranslation(20, 20, 0);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using var destination = painter.WithDestination(new Rect(0, 0, 40, 40), 1, 1, BlendMode.Normal, transform);
            var inner = destination.Painter;
            inner.Fill = Red;
            inner.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(20, 20, Rgba.Red);
        image.Expect(10, 1, Rgba.Red);
        image.Expect(27, 1, Rgba.Black);
        image.Expect(4, 20, Rgba.Black);
        image.Expect(34, 20, Rgba.Black);
    }

    [GpuFact]
    public void ClipFollowsAPerspectiveTransform()
    {
        var perspective = Matrix4x4.Identity;
        perspective.M34 = -1f / 100;
        var transform = Matrix4x4.CreateTranslation(-20, -20, 0) * Matrix4x4.CreateRotationY(float.DegreesToRadians(60)) * perspective * Matrix4x4.CreateTranslation(20, 20, 0);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            using var destination = painter.WithDestination(new Rect(0, 0, 40, 40), 1, 1, BlendMode.Normal, transform);
            var inner = destination.Painter;
            inner.Clip(new Rect(20, 0, 20, 40));
            inner.Fill = Red;
            inner.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(24, 20, Rgba.Red);
        image.Expect(16, 20, Rgba.Black);
    }
}
