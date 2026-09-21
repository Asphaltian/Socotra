namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class LabelDrawTests
{
    private readonly GpuHost _gpu;

    public LabelDrawTests(GpuHost gpu)
    {
        _gpu = gpu;
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
    }

    private static RootPanel Root()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("rootpanel { flex-direction: column; align-items: flex-start; font-family: Lato; font-size: 40px; color: white; }");
        return root;
    }

    private static void Update(RootPanel root) => root.Update(new Rect(0, 0, 1920, 1080), 0.016f);

    private static Label Add(Panel parent, string text, string style = "")
    {
        var label = parent.AddChild<Label>();
        label.Text = text;
        label.Style.Set(style);
        return label;
    }

    private Snapshot Draw(Panel panel, int width = 240, int height = 100) => _gpu.Render(width, height, painter =>
    {
        painter.Clear(Color.Black);
        panel.OnDraw(painter);
    });

    private static List<Vector2> Lit(Snapshot image, Func<Rgba, bool> test)
    {
        var lit = new List<Vector2>();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (test(image[x, y]))
                {
                    lit.Add(new Vector2(x, y));
                }
            }
        }

        return lit;
    }

    [GpuFact]
    public void TextIsDrawnInsideTheContentBox()
    {
        var root = Root();
        var label = Add(root, "Hello", "padding: 10px 20px;");
        Update(root);
        Update(root);

        var image = Draw(label);
        var lit = Lit(image, pixel => pixel.R > 16);
        var content = label.Box.RectInner.Grow(2);
        Assert.InRange(lit.Count, 300, (int)(content.Width * content.Height * 0.6f));
        Assert.All(lit, pixel => Assert.True(content.IsInside(pixel), $"Lit pixel {pixel} is outside {content}."));
        Assert.True(lit.Min(p => p.X) >= 19);
        Assert.Equal(255, lit.Max(p => (int)image[(int)p.X, (int)p.Y].R));
    }

    [GpuFact]
    public void TextAlignCenterMovesTheText()
    {
        var root = Root();
        var left = Add(root, "i", "width: 200px;");
        var centered = Add(root, "i", "width: 200px; text-align: center;");
        Update(root);
        Update(root);

        var leftX = Lit(Draw(left), pixel => pixel.R > 16).Average(p => p.X);
        var centerX = Lit(Draw(centered, 240, 200), pixel => pixel.R > 16).Average(p => p.X);
        Assert.InRange(leftX, 0, 20);
        Assert.InRange(centerX, 90, 110);
    }

    [GpuFact]
    public void FontColorAndOpacityTintTheGlyphs()
    {
        var root = Root();
        var label = Add(root, "l", "color: #00ff00;");
        Update(root);
        Update(root);

        var image = _gpu.Render(60, 60, painter =>
        {
            painter.Clear(Color.Black);
            using (painter.BeginLayer(new Rect(0, 0, 60, 60), 0.5f))
            {
                label.OnDraw(painter);
            }
        });

        var lit = Lit(image, pixel => pixel.G > 16);
        Assert.NotEmpty(lit);
        Assert.InRange(lit.Max(p => (int)image[(int)p.X, (int)p.Y].G), 120, 135);
        Assert.All(lit, p => Assert.Equal(0, image[(int)p.X, (int)p.Y].R));
    }

    [GpuFact]
    public void SelectionIsHighlightedBehindTheSelectedCharacters()
    {
        var root = Root();
        var label = Add(root, "ab cd", "white-space: nowrap;");
        Update(root);
        Update(root);
        label.SelectionColor = new Color(0, 0, 1);
        label.ShouldDrawSelection = true;
        label.SetSelection(3, 5);

        var image = Draw(label);
        var selected = Lit(image, pixel => pixel.B > 200 && pixel.R < 16);
        var start = label.GetCaretRect(3).Left;
        Assert.NotEmpty(selected);
        Assert.InRange(selected.Min(p => p.X), start - 2, start + 1);
        Assert.True(selected.Max(p => p.X) <= label.Box.Rect.Right + 1);
    }

    [GpuFact]
    public void FontSmoothNeverDrawsHardEdges()
    {
        var root = Root();
        var smooth = Add(root, "Ola");
        var aliased = Add(root, "Ola", "font-smooth: never;");
        Update(root);
        Update(root);

        var image = _gpu.Render(120, 140, painter =>
        {
            painter.Clear(Color.Black);
            smooth.OnDraw(painter);
            aliased.OnDraw(painter);
        });

        int Partial(Label label) => Lit(image, pixel => pixel.R is > 16 and < 240).Count(p => label.Box.Rect.IsInside(p));
        Assert.True(Partial(smooth) > 20);
        Assert.True(Partial(aliased) < Partial(smooth) / 4);
    }

    [GpuFact]
    public void TextGradientColorsTheGlyphs()
    {
        var root = Root();
        var label = Add(root, "IIIIIIIIII", "font-size: 60px; color: linear-gradient(90deg, red, blue);");
        Update(root);
        Update(root);

        var image = Draw(label, 400, 100);
        var lit = Lit(image, pixel => pixel.R + pixel.B > 64);
        var left = lit.Where(p => p.X < label.Box.Rect.Left + 20).ToList();
        var right = lit.Where(p => p.X > label.Box.Rect.Right - 20).ToList();
        Assert.NotEmpty(left);
        Assert.NotEmpty(right);
        Assert.True(left.Average(p => image[(int)p.X, (int)p.Y].R) > left.Average(p => image[(int)p.X, (int)p.Y].B));
        Assert.True(right.Average(p => image[(int)p.X, (int)p.Y].B) > right.Average(p => image[(int)p.X, (int)p.Y].R));
    }

    [GpuFact]
    public void InlineParagraphsDrawEverySpan()
    {
        var root = Root();
        var paragraph = root.AddChild<Panel>();
        paragraph.Style.Set("display: block; width: 400px;");
        var first = Add(paragraph, "red ", "display: inline; color: red;");
        var second = Add(paragraph, "blue", "display: inline; color: #0000ff;");
        Update(root);
        Update(root);

        var image = _gpu.Render(400, 60, painter =>
        {
            painter.Clear(Color.Black);
            first.OnDraw(painter);
            second.OnDraw(painter);
        });
        Assert.Empty(Lit(image, pixel => pixel.R > 16 || pixel.B > 16));

        image = _gpu.Render(400, 60, painter =>
        {
            painter.Clear(Color.Black);
            paragraph.LayoutTree.DrawInlineContent(painter);
        });
        var red = Lit(image, pixel => pixel.R > 128 && pixel.B < 16);
        var blue = Lit(image, pixel => pixel.B > 128 && pixel.R < 16);
        Assert.NotEmpty(red);
        Assert.NotEmpty(blue);
        Assert.True(red.Max(p => p.X) < blue.Min(p => p.X));
    }

    [GpuFact]
    public void ImagesDrawTheirTextureWithObjectFit()
    {
        var root = Root();
        var image = root.AddChild<Image>();
        image.Texture = Texture.FromPixels(20, 10, [.. Enumerable.Range(0, 200).SelectMany(i => i % 20 < 10 ? new byte[] { 255, 0, 0, 255 } : [0, 0, 255, 255])]);
        image.Style.Set("width: 40px; height: 40px; object-fit: contain; background-repeat: no-repeat;");
        Update(root);
        Update(root);

        var snapshot = _gpu.Render(40, 40, painter =>
        {
            painter.Clear(Color.Black);
            image.OnDraw(painter);
        });

        snapshot.Expect(5, 10, Rgba.Red);
        snapshot.Expect(35, 10, Rgba.Blue);
        snapshot.Expect(5, 30, Rgba.Black);
        snapshot.Expect(35, 30, Rgba.Black);
    }

    [GpuFact]
    public void FocusedTextEntriesDrawACaret()
    {
        var root = Root();
        root.StyleSheet.Parse("rootpanel { flex-direction: column; align-items: flex-start; font-family: Lato; font-size: 40px; color: white; } .textentry { width: 200px; caret-color: #00ff00; }");
        var entry = root.AddChild<CaretEntry>();
        entry.Text = "ab";
        Update(root);
        entry.Focus();
        Update(root);
        Update(root);
        entry.CaretPosition = 1;

        var snapshot = _gpu.Render(240, 100, painter =>
        {
            painter.Clear(Color.Black);
            entry.OnDraw(painter);
        });

        var caret = Lit(snapshot, pixel => pixel.G > 200 && pixel.R < 16);
        Assert.NotEmpty(caret);
        var x = (int)MathF.Floor(entry.ContentLabel.GetCaretRect(1).Left);
        Assert.All(caret, p => Assert.Equal(x, (int)p.X));
    }

    private sealed class CaretEntry : TextEntry
    {
        public Label ContentLabel => Label;
    }
}
