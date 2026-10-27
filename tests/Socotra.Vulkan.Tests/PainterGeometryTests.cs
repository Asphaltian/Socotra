namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class PainterGeometryTests(GpuHost gpu)
{
    private static Texture LeftHalfMask(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width / 2; x++)
            {
                pixels.AsSpan(((y * width) + x) * 4, 4).Fill(255);
            }
        }

        return Texture.FromPixels(width, height, pixels);
    }

    private static int Difference(Rgba a, Rgba b) => Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A)));

    [GpuTheory]
    [InlineData(48f, 20f, 6f, false)]
    [InlineData(20f, 48f, 6f, false)]
    [InlineData(47f, 43f, 6f, true)]
    [InlineData(12f, 9f, 3f, true)]
    [InlineData(20.5f, 20.25f, 10f, true)]
    [InlineData(48f, 35f, 0.75f, true)]
    public void StraightLinesMatchTheGeneralPath(float x, float y, float width, bool styled)
    {
        foreach (var cap in Enum.GetValues<Stroke.LineCap>())
        {
            foreach (var style in new[] { BorderStyle.Solid, BorderStyle.Dashed, BorderStyle.Dotted })
            {
                foreach (float phase in new[] { -9f, 0, 3, 7 })
                {
                    CompareLines(new Vector2(x, y), width, styled, cap, style, phase);
                }
            }
        }
    }

    [GpuTheory]
    [InlineData(12f, 2f, 1f, false)]
    [InlineData(0.25f, 0.25f, 0.1f, true)]
    [InlineData(3f, 5f, 0f, false)]
    public void DensePatternsMatchTheGeneralPath(float width, float dash, float gap, bool skew)
    {
        foreach (var cap in Enum.GetValues<Stroke.LineCap>())
        {
            foreach (var style in new[] { BorderStyle.Dashed, BorderStyle.Dotted })
            {
                CompareLines(new Vector2(47, 43), width, true, cap, style, -0.5f, dash, gap, skew);
            }
        }
    }

    private void CompareLines(Vector2 to, float width, bool styled, Stroke.LineCap cap, BorderStyle style, float phase, float dash = 5, float gap = 3, bool skew = false)
    {
        var actual = gpu.Render(64, 64, painter => DrawLine(painter, false, to, width, styled, cap, style, phase, dash, gap, skew));
        var expected = gpu.Render(64, 64, painter => DrawLine(painter, true, to, width, styled, cap, style, phase, dash, gap, skew));
        int maximum = 0;
        int alpha = 0;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                alpha = Math.Max(alpha, expected[x, y].A);
                maximum = Math.Max(maximum, Difference(actual[x, y], expected[x, y]));
            }
        }

        if (style == BorderStyle.Solid)
        {
            Assert.True(alpha > 25, "The line should draw something.");
        }

        Assert.True(maximum <= 5, $"{style}, {cap}, phase {phase}: pixels differ by up to {maximum}.");
    }

    private static void DrawLine(Painter painter, bool general, Vector2 to, float width, bool styled, Stroke.LineCap cap, BorderStyle style, float phase, float dash, float gap, bool skew)
    {
        painter.Clear(Color.Transparent);
        painter.Fill = new Color(0, 1, 0);
        var stroke = Stroke.Solid(styled ? Fill.LinearGradient(new Color(1, 0, 0), new Color(0, 0, 1), 35) : Color.White, width).WithCap(cap);
        painter.Stroke = stroke with { Style = style, DashLength = dash, Gap = gap, Offset = phase };
        if (styled)
        {
            painter.Opacity = 0.6f;
            painter.Clip(new Rect(12, 8, 35, 42));
            painter.Transform = Matrix4x4.CreateScale(0.8f, 1.1f, 1);
            if (skew)
            {
                var skewed = Matrix4x4.Identity;
                skewed.M21 = MathF.Tan(float.DegreesToRadians(70));
                painter.Transform = skewed * Matrix4x4.CreateScale(0.2f, 1, 1);
            }
        }

        var from = new Vector2(20);
        if (general)
        {
            painter.Line([from, from, to]);
        }
        else
        {
            painter.Line(from, to);
        }
    }

    [GpuTheory]
    [InlineData(9, 0)]
    [InlineData(32, 0)]
    [InlineData(64, 0)]
    [InlineData(128, 0)]
    [InlineData(129, 0)]
    [InlineData(9, 1)]
    [InlineData(9, 2)]
    [InlineData(9, 3)]
    public void PolygonPointsFillLikeTheirEdges(int count, int topology)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * (topology == 2 ? i * 2 : i) / count;
            float radius = topology == 1 && i % 2 == 0 ? 16 : 35;
            points[i] = new Vector2(48) + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        if (topology == 3)
        {
            points[2] = points[1];
        }

        foreach (bool reversed in new[] { false, true })
        {
            if (reversed)
            {
                Array.Reverse(points);
            }

            var expected = gpu.Render(128, 128, painter => DrawPolygon(painter, points, true));
            var actual = gpu.Render(128, 128, painter => DrawPolygon(painter, points, false));
            int visible = 0;
            int maximum = 0;
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    if (expected[x, y].A > 25)
                    {
                        visible++;
                    }

                    maximum = Math.Max(maximum, Difference(actual[x, y], expected[x, y]));
                }
            }

            Assert.True(visible > 40);
            Assert.True(maximum <= 1, $"{count} points, topology {topology}, reversed {reversed}: pixels differ by up to {maximum}.");
        }
    }

    private static void DrawPolygon(Painter painter, Vector2[] points, bool edges)
    {
        painter.Clear(Color.Transparent);
        painter.Translate(3.25f, 2.75f);
        painter.Rotate(3);
        painter.Scale(1.05f, 0.9f);
        painter.Clip(new Rect(12, 10, 80, 80));
        painter.Opacity = 0.7f;
        var color = new Color(1, 0, 0, 0.4f);
        for (int copy = 0; copy < 2; copy++)
        {
            if (edges)
            {
                new Painter.CachedPolygon().Draw(painter, points, color);
            }
            else
            {
                painter.Fill = color;
                painter.Stroke = Stroke.None;
                painter.Polygon(points);
            }

            painter.Translate(13, 7);
        }
    }

    [GpuTheory]
    [InlineData(4, 0.25f, true)]
    [InlineData(4, 0.25f, false)]
    [InlineData(4, 4f, true)]
    [InlineData(4, 4f, false)]
    [InlineData(5, 0.25f, true)]
    [InlineData(5, 0.25f, false)]
    [InlineData(5, 4f, true)]
    [InlineData(5, 4f, false)]
    [InlineData(9, 0.25f, true)]
    [InlineData(9, 0.25f, false)]
    [InlineData(9, 4f, true)]
    [InlineData(9, 4f, false)]
    [InlineData(9, 16f, true)]
    [InlineData(9, 16f, false)]
    [InlineData(8, 0.25f, false)]
    [InlineData(8, 4f, false)]
    public void SharedOutlinesMatchTheGeneralPath(int count, float width, bool hasFill)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = MathF.Tau * (count == 5 ? i * 2 : i) / count;
            float radius = i % 2 == 0 ? 29 : 19;
            points[i] = new Vector2(44 + MathF.Cos(angle) * radius, 44 + MathF.Sin(angle) * radius);
        }

        foreach (var alignment in Enum.GetValues<Stroke.StrokeAlignment>())
        {
            foreach (bool reversed in new[] { false, true })
            {
                if (reversed)
                {
                    Array.Reverse(points);
                }

                var expected = gpu.Render(96, 96, painter => DrawOutline(painter, points, width, alignment, false, hasFill));
                var actual = gpu.Render(96, 96, painter => DrawOutline(painter, points, width, alignment, true, hasFill));
                double total = 0;
                int maximum = 0;
                for (int y = 0; y < 96; y++)
                {
                    for (int x = 0; x < 96; x++)
                    {
                        int difference = Difference(actual[x, y], expected[x, y]);
                        total += difference;
                        maximum = Math.Max(maximum, difference);
                    }
                }

                Assert.True(total / (96 * 96) < 0.002 * 255, $"{alignment}, reversed {reversed}: pixels differ by {total / (96 * 96)} on average.");
                Assert.True(maximum < 0.12 * 255, $"{alignment}, reversed {reversed}: pixels differ by up to {maximum}.");
                if (reversed)
                {
                    Array.Reverse(points);
                }
            }
        }
    }

    private static void DrawOutline(Painter painter, Vector2[] points, float width, Stroke.StrokeAlignment alignment, bool shared, bool hasFill)
    {
        painter.Clear(Color.Transparent);
        painter.Translate(3.25f, 2.75f);
        painter.Rotate(3);
        painter.Scale(1.05f, 0.9f);
        painter.Clip(new Rect(12, 10, 70, 70));
        painter.Opacity = 0.7f;
        painter.Fill = hasFill ? Fill.LinearGradient(new Color(1, 0, 0, 0.4f), new Color(0, 0, 1, 0.6f)) : Fill.None;
        painter.Stroke = Stroke.Solid(Fill.LinearGradient(Color.White, new Color(0, 1, 0)), width).WithAlignment(alignment);
        var context = painter.ActiveContext;
        if (shared)
        {
            painter.Polygon(points);
            var batcher = context.Batcher;
            Assert.Equal(ShapeKind.PolygonStroke, batcher.Shapes[batcher.Boxes[batcher.Boxes.Count - 1].ShapeIndex].Kind);
        }
        else
        {
            if (hasFill)
            {
                Painter.Path.DrawPolygon(context, points, painter.Fill);
            }

            Painter.Path.DrawPolyline(context, points, painter.Stroke, closed: true);
        }
    }

    [GpuFact]
    public void FillMasksClipPolygonsButNotLines()
    {
        var mask = LeftHalfMask(48, 24);
        var image = gpu.Render(64, 64, painter =>
        {
            painter.Clear(Color.Transparent);
            painter.Opacity = 0.5f;
            painter.Clip(new Rect(0, 0, 48, 64));
            painter.MaskFill(mask, new Rect(8, 8, 48, 24));
            painter.Fill = new Color(1, 0, 0);
            painter.Polygon([new Vector2(8, 8), new Vector2(56, 8), new Vector2(56, 32), new Vector2(8, 32)]);
            painter.Stroke = Stroke.Solid(new Color(0, 0, 1), 8);
            painter.Line(new Vector2(8, 48), new Vector2(56, 48));
        });

        Assert.InRange(image[16, 20].A, 122, 133);
        Assert.InRange(image[40, 20].A, 0, 5);
        Assert.InRange(image[16, 48].A, 122, 133);
        Assert.InRange(image[40, 48].A, 122, 133);
        Assert.InRange(image[54, 48].A, 0, 5);
    }

    [GpuFact]
    public void OutlinesOfMaskedPolygonsIgnoreTheMask()
    {
        var mask = LeftHalfMask(64, 64);
        var image = gpu.Render(64, 64, painter =>
        {
            painter.Clear(Color.Transparent);
            painter.MaskFill(mask, new Rect(0, 0, 64, 64));
            painter.Fill = new Color(1, 0, 0);
            painter.Stroke = Stroke.Solid(new Color(0, 0, 1), 4);
            painter.Polygon([new Vector2(8, 8), new Vector2(56, 8), new Vector2(56, 56), new Vector2(8, 56)]);
        });

        Assert.InRange(image[20, 32].A, 250, 255);
        Assert.InRange(image[40, 32].A, 0, 5);
        Assert.InRange(image[56, 32].B, 250, 255);
    }
}
