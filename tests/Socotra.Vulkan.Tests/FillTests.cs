namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class FillTests(GpuHost gpu)
{
    private static readonly Color Red = new(1, 0, 0);
    private static readonly Color Blue = new(0, 0, 1);

    [GpuFact]
    public void HalfCoveredPixelsBlend()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Red;
            painter.Rect(new Rect(10.5f, 10, 20, 20));
        });

        image.Expect(10, 20, Rgba.Of(0.5f, 0, 0), 3);
        image.Expect(11, 20, Rgba.Red);
    }

    [GpuFact]
    public void OpacityFadesTheFill()
    {
        var image = gpu.Render(20, 20, painter =>
        {
            painter.Clear(Color.Black);
            painter.Opacity = 0.5f;
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 20, 20));
        });

        image.Expect(10, 10, Rgba.Of(0.5f, 0, 0), 1);
    }

    [GpuFact]
    public void RoundedCornersCutTheCorners()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Color.White;
            painter.Rect(new Rect(0, 0, 40, 40), 12);
        });

        image.Expect(1, 1, Rgba.Black);
        image.Expect(38, 1, Rgba.Black);
        image.Expect(1, 38, Rgba.Black);
        image.Expect(38, 38, Rgba.Black);
        image.Expect(20, 1, Rgba.White);
        image.Expect(20, 20, Rgba.White);
        image.Expect(5, 5, Rgba.White);
    }

    [GpuFact]
    public void EachBorderSideHasItsColor()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Color.White;
            painter.Rect(new Rect(0, 0, 40, 40), new Vector4(4), Red, new Color(0, 1, 0), Blue, new Color(1, 1, 0));
        });

        image.Expect(1, 20, Rgba.Red);
        image.Expect(20, 1, Rgba.Green);
        image.Expect(38, 20, Rgba.Blue);
        image.Expect(20, 38, new Rgba(255, 255, 0));
        image.Expect(20, 20, Rgba.White);
        image.Expect(5, 20, Rgba.White);
    }

    [GpuFact]
    public void InsideStrokeStaysInsideTheRectangle()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Stroke = Stroke.Solid(Red, 4).WithAlignment(Stroke.StrokeAlignment.Inside);
            painter.Rect(new Rect(10, 10, 20, 20));
        });

        image.Expect(11, 20, Rgba.Red);
        image.Expect(13, 20, Rgba.Red);
        image.Expect(14, 20, Rgba.Black);
        image.Expect(9, 20, Rgba.Black);
    }

    [GpuFact]
    public void CenteredStrokeStraddlesTheEdge()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Stroke = Stroke.Solid(Red, 4);
            painter.Rect(new Rect(10, 10, 20, 20));
        });

        image.Expect(8, 20, Rgba.Red);
        image.Expect(11, 20, Rgba.Red);
        image.Expect(6, 20, Rgba.Black);
        image.Expect(13, 20, Rgba.Black);
        image.Expect(20, 20, Rgba.Black);
    }

    [GpuFact]
    public void LinearGradientRunsAlongItsAngle()
    {
        var image = gpu.Render(64, 8, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.LinearGradient(Red, Blue);
            painter.Rect(new Rect(0, 0, 64, 8));
        });

        foreach (var x in new[] { 0, 16, 32, 48, 63 })
        {
            var t = (x + 0.5f) / 64;
            image.Expect(x, 4, Rgba.Of(1 - t, 0, t));
        }
    }

    [GpuFact]
    public void VerticalLinearGradientRunsDown()
    {
        var image = gpu.Render(8, 64, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.LinearGradient(Red, Blue, 90);
            painter.Rect(new Rect(0, 0, 8, 64));
        });

        image.Expect(4, 0, Rgba.Of(1 - (0.5f / 64), 0, 0.5f / 64));
        image.Expect(4, 40, Rgba.Of(1 - (40.5f / 64), 0, 40.5f / 64));
    }

    [GpuFact]
    public void RadialGradientGrowsFromTheCenter()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.RadialGradient(Color.White, Color.Black);
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        foreach (var (x, y) in new[] { (20, 20), (30, 20), (20, 5), (26, 26) })
        {
            var t = MathF.Min(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(20)) / 20, 1);
            image.Expect(x, y, Rgba.Of(1 - t, 1 - t, 1 - t));
        }

        image.Expect(0, 0, Rgba.Black);
    }

    [GpuFact]
    public void ConicGradientTurnsClockwiseFromTheRight()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.ConicGradient(Red, Blue);
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        foreach (var (x, y) in new[] { (35, 24), (20, 35), (5, 20), (18, 4), (34, 8) })
        {
            var turn = MathF.Atan2(y + 0.5f - 20, x + 0.5f - 20) / (2 * MathF.PI);
            var t = turn - MathF.Floor(turn);
            image.Expect(x, y, Rgba.Of(1 - t, 0, t));
        }
    }

    [GpuFact]
    public void GradientsWithSeveralStopsHitEachStop()
    {
        var image = gpu.Render(60, 4, painter =>
        {
            painter.Clear(Color.Black);
            painter.Fill = Fill.LinearGradient().WithStop(0, Red).WithStop(0.5f, new Color(0, 1, 0)).WithStop(0.5f, Blue).WithStop(1, Color.White);
            painter.Rect(new Rect(0, 0, 60, 4));
        });

        image.Expect(0, 2, Rgba.Of(1 - (0.5f / 30), 0.5f / 30, 0));
        image.Expect(29, 2, Rgba.Of(1 - (29.5f / 30), 29.5f / 30, 0));
        image.Expect(30, 2, Rgba.Of(0.5f / 30, 0.5f / 30, 1));
        image.Expect(59, 2, Rgba.Of(29.5f / 30, 29.5f / 30, 1));
    }

    [GpuFact]
    public void BorderImageFramesTheBox()
    {
        var red = new byte[] { 255, 0, 0, 255 };
        var green = new byte[] { 0, 255, 0, 255 };
        var blue = new byte[] { 0, 0, 255, 255 };
        var texture = Texture.FromPixels(3, 3, [.. red, .. green, .. red, .. green, .. blue, .. green, .. red, .. green, .. red]);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.Rect(new Painter.BoxDescriptor(new Rect(0, 0, 40, 40), Color.Transparent)
            {
                Stroke = new Painter.BoxStroke { Size = new Vector4(4) },
                BorderImage = new NineSliceImage(texture, new Vector4(1), BorderImageRepeat.Stretch, BorderImageFill.Filled, Color.White),
            });
        });

        image.Expect(1, 1, Rgba.Red);
        image.Expect(38, 38, Rgba.Red);
        image.Expect(20, 1, Rgba.Green);
        image.Expect(1, 20, Rgba.Green);
        image.Expect(20, 20, Rgba.Blue);
    }

    [GpuFact]
    public void FillMaskShowsTheFillThroughTheMask()
    {
        var mask = Texture.FromPixels(4, 1, [255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0]);
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.MaskFill(mask, new Rect(0, 0, 40, 40));
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(5, 20, Rgba.Red);
        image.Expect(34, 20, Rgba.Black);
    }

    [GpuFact]
    public void FillInsetsClipTheBackground()
    {
        var image = gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            painter.ClipFill(new Vector4(10, 5, 10, 5));
            painter.Fill = Red;
            painter.Rect(new Rect(0, 0, 40, 40));
        });

        image.Expect(20, 20, Rgba.Red);
        image.Expect(12, 7, Rgba.Red);
        image.Expect(8, 20, Rgba.Black);
        image.Expect(20, 3, Rgba.Black);
    }
}
