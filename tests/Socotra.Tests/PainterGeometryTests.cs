namespace Socotra.Tests;

public class PainterGeometryTests
{
    private readonly DrawList _list = new();

    private Painter Begin() => Painter.Begin(_list, new Rect(0, 0, 200, 200));

    private PainterBatcher Batcher => _list.Batcher;

    private ShapeInstance ShapeOf(int box) => Batcher.Shapes[Batcher.Boxes[box].ShapeIndex];

    [Theory]
    [InlineData(BorderStyle.Solid, Stroke.LineCap.Round, false, true)]
    [InlineData(BorderStyle.Dashed, Stroke.LineCap.Round, false, true)]
    [InlineData(BorderStyle.Dotted, Stroke.LineCap.Round, false, true)]
    [InlineData(BorderStyle.Solid, Stroke.LineCap.Butt, false, true)]
    [InlineData(BorderStyle.Solid, Stroke.LineCap.Square, false, true)]
    [InlineData(BorderStyle.Dashed, Stroke.LineCap.Triangle, false, true)]
    [InlineData(BorderStyle.Dashed, Stroke.LineCap.Arrow, false, true)]
    [InlineData(BorderStyle.Solid, Stroke.LineCap.Round, true, false)]
    public void TwoPointLinesDrawWithoutPathGeometry(BorderStyle style, Stroke.LineCap cap, bool closed, bool simple)
    {
        using (Begin())
        {
            var stroke = Stroke.Solid(new Color(1, 0, 0), 6) with { Style = style, Cap = cap };
            Painter.Path.DrawPolyline(_list.PainterContext, [new(20, 30), new(70, 60)], stroke, closed);
        }

        Assert.Equal(1, Batcher.Boxes.Count);
        Assert.Equal(simple ? ShapeKind.SimpleLine : ShapeKind.StrokePath, ShapeOf(0).Kind);
        Assert.Equal(simple, Batcher.Paths.Count == 0);
    }

    [Fact]
    public void LinesWithoutLengthOrWithBrokenEndsDrawNothing()
    {
        using (var painter = Begin())
        {
            painter.Stroke = Stroke.Solid(Color.White, 6).WithCap(Stroke.LineCap.Round);
            painter.Line(new Vector2(20), new Vector2(20));
            painter.Line(new Vector2(float.NaN, 20), new Vector2(40));
            painter.Line(new Vector2(20), new Vector2(float.PositiveInfinity, 40));
        }

        Assert.Equal(0, Batcher.Boxes.Count);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(129)]
    public void MediumPolygonsKeepTheirPointsAndLargeOnesKeepEdges(int count)
    {
        var points = new Vector2[count];
        var origin = new Vector2(float.MaxValue);
        for (int i = 0; i < count; i++)
        {
            points[i] = new Vector2(30, 40) + new Vector2(MathF.Cos(i * MathF.Tau / count), MathF.Sin(i * MathF.Tau / count)) * 10;
            origin = Vector2.Min(origin, points[i]);
        }

        using (var painter = Begin())
        {
            painter.Fill = Color.White;
            painter.Stroke = Stroke.None;
            painter.Polygon(points);
            painter.Polygon(points);
        }

        bool raw = count > 8 && count <= PainterBatcher.MaxPolygonPoints;
        bool large = count > PainterBatcher.MaxPolygonPoints;
        Assert.Equal(2, Batcher.Shapes.Count);
        Assert.Equal(raw ? count * 2 : 0, Batcher.PolygonPoints.Count);
        Assert.Equal(large ? count * 2 : 0, Batcher.Paths.Count);
        Assert.Equal(large ? (count * 2 - 1) * 2 : 0, Batcher.PathNodes.Count);
        if (raw)
        {
            Assert.Equal(-1, Batcher.Shapes[0].PathNodeCount);
            Assert.Equal(0, Batcher.Shapes[0].PathOffset);
            Assert.Equal(count, Batcher.Shapes[1].PathOffset);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(points[i] - origin, Batcher.PolygonPoints[i]);
            }
        }

        using (Begin())
        {
        }

        Assert.Equal(0, Batcher.PolygonPoints.Count);
    }

    [Theory]
    [InlineData(4, Stroke.StrokeAlignment.Center, true)]
    [InlineData(9, Stroke.StrokeAlignment.Center, true)]
    [InlineData(9, Stroke.StrokeAlignment.Inside, true)]
    [InlineData(9, Stroke.StrokeAlignment.Outside, true)]
    [InlineData(4, Stroke.StrokeAlignment.Center, false)]
    [InlineData(4, Stroke.StrokeAlignment.Inside, false)]
    [InlineData(4, Stroke.StrokeAlignment.Outside, false)]
    [InlineData(8, Stroke.StrokeAlignment.Center, false)]
    [InlineData(8, Stroke.StrokeAlignment.Inside, false)]
    [InlineData(8, Stroke.StrokeAlignment.Outside, false)]
    [InlineData(9, Stroke.StrokeAlignment.Center, false)]
    [InlineData(9, Stroke.StrokeAlignment.Inside, false)]
    [InlineData(9, Stroke.StrokeAlignment.Outside, false)]
    public void PolygonFillAndOutlineShareOneContour(int count, Stroke.StrokeAlignment alignment, bool hasFill)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * i / count;
            points[i] = new Vector2(100 + MathF.Cos(angle) * 80, 100 + MathF.Sin(angle) * 80);
        }

        using (var painter = Begin())
        {
            painter.Fill = hasFill ? new Color(1, 0, 0) : Fill.None;
            painter.Stroke = Stroke.Solid(new Color(0, 0, 1), 4).WithAlignment(alignment);
            painter.Polygon(points);
        }

        Assert.Equal(hasFill ? 2 : 1, Batcher.Boxes.Count);
        Assert.Equal(2, Batcher.Shapes.Count);
        int fillIndex = hasFill ? Batcher.Boxes[0].ShapeIndex : 0;
        var outline = ShapeOf(hasFill ? 1 : 0);
        Assert.Equal(ShapeKind.PolygonStroke, outline.Kind);
        Assert.Equal(fillIndex, outline.PolygonCount);
        Assert.Equal(count > 8 ? count : 0, Batcher.PolygonPoints.Count);
        Assert.Equal(0, Batcher.Paths.Count);
        Assert.Equal(0, Batcher.PathNodes.Count);
        Assert.Equal(0, outline.PathCount);
        Assert.Equal(alignment == Stroke.StrokeAlignment.Center ? 4f : 8f, outline.Circle.Z);
        Assert.Equal(alignment == Stroke.StrokeAlignment.Center ? 0f : alignment == Stroke.StrokeAlignment.Inside ? 1f : -1f, outline.Circle.W);
        float offset = alignment == Stroke.StrokeAlignment.Inside ? 0 : alignment == Stroke.StrokeAlignment.Center ? -2 : -4;
        Assert.Equal(new Vector2(offset), new Vector2(outline.Circle.X, outline.Circle.Y));
    }

    [Theory]
    [InlineData("short", true)]
    [InlineData("short", false)]
    [InlineData("duplicate", true)]
    [InlineData("duplicate", false)]
    [InlineData("miter", true)]
    [InlineData("miter", false)]
    [InlineData("bevel", true)]
    [InlineData("bevel", false)]
    [InlineData("dashed", true)]
    [InlineData("dashed", false)]
    [InlineData("dotted", true)]
    [InlineData("dotted", false)]
    public void OutlinesTheContourCantDrawKeepTheirPath(string kind, bool hasFill)
    {
        Vector2[] points = [new(20, 20), new(80, 20), new(80, 80), new(20, 80)];
        if (kind == "short")
        {
            points[1] = new(21, 20);
        }

        if (kind == "duplicate")
        {
            points[1] = points[0];
        }

        using (var painter = Begin())
        {
            painter.Fill = hasFill ? new Color(1, 0, 0) : Fill.None;
            painter.Stroke = kind switch
            {
                "miter" => Stroke.Solid(Color.White, 8) with { Join = Stroke.LineJoin.Miter },
                "bevel" => Stroke.Solid(Color.White, 8) with { Join = Stroke.LineJoin.Bevel },
                "dashed" => Stroke.Dashed(Color.White, 8, 8, 8),
                "dotted" => Stroke.Dotted(Color.White, 8, 8),
                _ => Stroke.Solid(Color.White, 8),
            };
            painter.Polygon(points);
        }

        Assert.Equal(hasFill ? 2 : 1, Batcher.Boxes.Count);
        Assert.Equal(ShapeKind.StrokePath, ShapeOf(hasFill ? 1 : 0).Kind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SolidShapesResolveLikeAnyOtherBox(bool polygon, bool gradient)
    {
        var context = _list.PainterContext;
        var color = new Color(0.2f, 0.4f, 0.8f, 0.75f);
        var fill = gradient ? Fill.LinearGradient(color, new Color(1, 0, 0)) : Fill.Solid(color);
        Rect bounds;
        BoxInstance actual;
        int commands;
        using (var painter = Begin())
        {
            context.InheritedOpacity = 0.6f;
            context.BaseTransform = Matrix4x4.CreateTranslation(7, 11, 0);
            painter.Opacity = 0.4f;
            painter.Transform = Matrix4x4.CreateTranslation(13, 17, 0);
            painter.BlendMode = BlendMode.Multiply;
            context.State.FillInsets = new Vector4(5, 6, 7, 8);
            painter.Clip(new Rect(0, 0, 200, 200));
            if (polygon)
            {
                painter.Fill = fill;
                painter.Polygon([new Vector2(10, 20), new Vector2(90, 20), new Vector2(90, 80)]);
                bounds = new Rect(10, 20, 80, 60);
            }
            else
            {
                painter.Stroke = Stroke.Solid(fill, 4);
                painter.Line(new Vector2(10, 20), new Vector2(90, 20));
                bounds = new Rect(8, 18, 84, 4);
            }

            Assert.Equal(1, Batcher.Boxes.Count);
            actual = Batcher.Boxes[0];
            fill.CreateDescriptor(bounds, context, out var descriptor, clipFill: polygon);
            descriptor.ShapeIndex = actual.ShapeIndex;
            var transform = context.State.Transform * context.BaseTransform;
            Batcher.Add(descriptor, context.State.Opacity, context.State.OverrideBlendMode, transform, context.State.ClipIndex);
            Assert.Equal(Batcher.Boxes[1], actual);
            Assert.Equal(0, actual.Mode);
            Assert.Equal(-1, actual.InverseScissorIndex);
            Assert.True(actual.ShapeIndex >= 0);
            Assert.True(actual.ScissorIndex >= 0);
            Assert.Equal(polygon ? (int)BackgroundClip.ContentBox : (int)BackgroundClip.BorderBox, actual.BackgroundClip);
            Assert.Equal(context.State.FillInsets, actual.BackgroundClipRect);
            Assert.Equal(transform, Batcher.Transforms[actual.TransformIndex]);
            if (gradient)
            {
                Assert.True(actual.TextureIndex < 0);
                Assert.Equal(1, Batcher.Gradients.Count);
            }
            else
            {
                Assert.Equal(color.WithAlphaMultiplied(0.6f).WithAlphaMultiplied(0.4f), actual.Color);
                Assert.Equal(0, actual.TextureIndex);
            }

            commands = _list.Commands.Count;
            painter.BlendMode = BlendMode.Normal;
            painter.Stroke = Stroke.Solid(Color.White, 4);
            painter.Line(new Vector2(10, 20), new Vector2(90, 20));
            Assert.Equal(commands + 1, _list.Commands.Count);
        }
    }

    [Fact]
    public void FillMasksClipPolygonsButNotLines()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 0, 0, 0, 0]);
        using (var painter = Begin())
        {
            painter.Opacity = 0.5f;
            painter.Clip(new Rect(0, 0, 48, 64));
            painter.MaskFill(mask, new Rect(8, 8, 48, 24));
            painter.Fill = new Color(1, 0, 0);
            painter.Polygon([new Vector2(8, 8), new Vector2(56, 8), new Vector2(56, 32), new Vector2(8, 32)]);
            painter.Stroke = Stroke.Solid(new Color(0, 0, 1), 8);
            painter.Line(new Vector2(8, 48), new Vector2(56, 48));
        }

        Assert.Equal(2, Batcher.Boxes.Count);
        Assert.Equal((int)BackgroundClip.Text, Batcher.Boxes[0].BackgroundClip);
        Assert.Equal(new Vector4(0, 0, 48, 24), Batcher.Boxes[0].BackgroundClipRect);
        Assert.Equal((int)BackgroundClip.BorderBox, Batcher.Boxes[1].BackgroundClip);
        Assert.Equal(Batcher.Boxes[0].ScissorIndex, Batcher.Boxes[1].ScissorIndex);
    }

    [Fact]
    public void OutlinesOfMaskedPolygonsIgnoreTheMask()
    {
        var mask = Texture.FromPixels(2, 1, [255, 255, 255, 255, 0, 0, 0, 0]);
        using (var painter = Begin())
        {
            painter.MaskFill(mask, new Rect(0, 0, 64, 64));
            painter.Fill = new Color(1, 0, 0);
            painter.Stroke = Stroke.Solid(new Color(0, 0, 1), 4);
            painter.Polygon([new Vector2(8, 8), new Vector2(56, 8), new Vector2(56, 56), new Vector2(8, 56)]);
        }

        Assert.Equal(ShapeKind.PolygonStroke, ShapeOf(1).Kind);
        Assert.Equal((int)BackgroundClip.Text, Batcher.Boxes[0].BackgroundClip);
        Assert.Equal((int)BackgroundClip.BorderBox, Batcher.Boxes[1].BackgroundClip);
    }

    private static Vector2[] Circle(int count, float radius)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * i / count;
            points[i] = new Vector2(100) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        return points;
    }

    private long AllocationsOfSecondDraw(Action<Painter> draw)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            using var painter = Begin();
            draw(painter);
        }

        using (var painter = Begin())
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            draw(painter);
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
    }

    [Theory]
    [InlineData("line", 2, BorderStyle.Solid)]
    [InlineData("line", 2, BorderStyle.Dashed)]
    [InlineData("line", 16, BorderStyle.Solid)]
    [InlineData("line", 16, BorderStyle.Dashed)]
    [InlineData("line", 16, BorderStyle.Dotted)]
    [InlineData("line", 256, BorderStyle.Solid)]
    [InlineData("line", 256, BorderStyle.Dashed)]
    [InlineData("line", 256, BorderStyle.Dotted)]
    [InlineData("arc", 0, BorderStyle.Solid)]
    [InlineData("arc", 0, BorderStyle.Dashed)]
    [InlineData("arc", 0, BorderStyle.Dotted)]
    [InlineData("polygon", 4, BorderStyle.Solid)]
    [InlineData("polygon", 9, BorderStyle.Solid)]
    [InlineData("polygon", 129, BorderStyle.Solid)]
    [InlineData("polygon", 9, BorderStyle.Dashed)]
    [InlineData("ring", 0, BorderStyle.Solid)]
    [InlineData("crescent", 0, BorderStyle.Solid)]
    [InlineData("bezier", 0, BorderStyle.Solid)]
    [InlineData("smooth", 24, BorderStyle.Solid)]
    public void DrawingAgainDoesNotAllocate(string shape, int count, BorderStyle style)
    {
        var points = Circle(Math.Max(count, 3), 60);
        foreach (var alignment in Enum.GetValues<Stroke.StrokeAlignment>())
        {
            long allocated = AllocationsOfSecondDraw(painter =>
            {
                painter.Fill = new Color(1, 0, 0);
                painter.Stroke = Stroke.Solid(Color.White, 3).WithAlignment(alignment) with { Style = style, DashLength = 6, Gap = 3 };
                switch (shape)
                {
                    case "line":
                        painter.Line(points.AsSpan(0, count));
                        break;
                    case "arc":
                        painter.Arc(new Vector2(100), 50, 10, 300);
                        break;
                    case "polygon":
                        painter.Polygon(points);
                        break;
                    case "ring":
                        painter.Ring(new Vector2(100), 30, 50);
                        break;
                    case "crescent":
                        painter.Crescent(new Vector2(100), 50, 30, new Vector2(10, 0));
                        break;
                    case "bezier":
                        painter.Bezier(new Vector2(0), new Vector2(0, 4096), new Vector2(4096), new Vector2(4096, 0));
                        break;
                    default:
                        painter.LineSmooth(points);
                        break;
                }
            });

            Assert.True(allocated == 0, $"{shape}, {alignment}: {allocated} bytes.");
        }
    }

    [Fact]
    public void LongCurvesKeepEveryPartOfTheirShape()
    {
        using (var painter = Begin())
        {
            painter.Stroke = Stroke.Solid(Color.White, 1);
            painter.Bezier(new Vector2(0, 0), new Vector2(0, 4096), new Vector2(4096, 4096), new Vector2(4096, 0));
        }

        var segments = Batcher.Paths.Span.ToArray().Where(p => p.Kind == PathPrimitiveKind.Segment && p.Count == 0 && p.B == default).ToArray();
        Assert.True(segments.Length > 64);
        Assert.Equal(new Vector2(0, 0), new Vector2(segments[0].A.X, segments[0].A.Y));
        Assert.Equal(new Vector2(4096, 0), new Vector2(segments[^1].A.Z, segments[^1].A.W));
        Assert.Contains(segments, p => p.A.Z == 2048 && p.A.W == 3072);
        for (int i = 1; i < segments.Length; i++)
        {
            Assert.Equal(new Vector2(segments[i - 1].A.Z, segments[i - 1].A.W), new Vector2(segments[i].A.X, segments[i].A.Y));
        }
    }

    [Fact]
    public void ShapesDrawnAfterBiggerOnesDontPickUpTheirLeftovers()
    {
        void Draw(Painter painter, bool big)
        {
            painter.Stroke = Stroke.Solid(Color.White, 2) with { Join = Stroke.LineJoin.Miter };
            painter.Line(Circle(big ? 300 : 40, big ? 90 : 30));
        }

        using (var painter = Begin())
        {
            Draw(painter, false);
        }

        var expectedPaths = Batcher.Paths.Span.ToArray();
        var expectedNodes = Batcher.PathNodes.Span.ToArray();
        using (var painter = Begin())
        {
            Draw(painter, true);
        }

        using (var painter = Begin())
        {
            Draw(painter, false);
        }

        Assert.Equal(expectedPaths, Batcher.Paths.Span.ToArray());
        Assert.Equal(expectedNodes, Batcher.PathNodes.Span.ToArray());
    }

    [Fact]
    public void ChangingAStrokesFillKeepsItAValue()
    {
        var original = Stroke.Solid(new Color(1, 0, 0), 4);
        foreach (var fill in new[] { Fill.Solid(new Color(0, 0, 1)), Fill.LinearGradient(new Color(1, 0, 0), new Color(0, 0, 1)) })
        {
            var updated = original with { Fill = fill };
            var expected = Stroke.Solid(fill, 4);
            Assert.Equal(expected, updated);
            Assert.True(expected == updated);
            Assert.Equal(expected.GetHashCode(), updated.GetHashCode());
            Assert.NotEqual(original, updated);
            Assert.Equal(Fill.Solid(new Color(1, 0, 0)), original.Fill);
            Assert.Equal(fill, updated.Fill);
            Assert.Equal(fill, Stroke.GetFill(in updated));
        }

        Assert.Equal(Fill.None, default(Stroke).Fill);
        Assert.Equal(Fill.Solid(new Color(0, 1, 0)), new Stroke { Fill = new Color(0, 1, 0) }.Fill);
    }
}
