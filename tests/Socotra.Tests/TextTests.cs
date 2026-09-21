using System.Runtime.CompilerServices;
using SkiaSharp;
using Topten.RichTextKit;

namespace Socotra.Tests;

public class TextTests
{
    private const string EmojiFont = @"C:\Windows\Fonts\seguiemj.ttf";

    public TextTests() => Fonts.Load("Data/Fonts/Lato-Regular.ttf");

    private static SKTypeface Lato => FontManager.Instance.TypefaceFromStyle(new Style { FontFamily = "Lato" }, false);

    private static (Vector2[] Curves, uint[] Bands, GpuFontGlyphCache.Glyph[] Table) GlyphData()
    {
        (Vector2[], uint[], GpuFontGlyphCache.Glyph[]) data = default;
        GpuFontGlyphCache.Read((curves, bands, table) => data = (curves.ToArray(), bands.ToArray(), table.ToArray()));
        return data;
    }

    private static ushort GlyphId(SKTypeface typeface, int codePoint)
    {
        using var font = new SKFont(typeface);
        return font.GetGlyph(codePoint);
    }

    private static List<TextInstance> Build(Topten.RichTextKit.TextBlock block, GpuFontText.Options? options = null)
    {
        var instances = new List<TextInstance>();
        GpuFontText.Build(block, Vector2.Zero, options ?? GpuFontText.Options.Default, instances);
        return instances;
    }

    private static Topten.RichTextKit.TextBlock Block(string text, Style style)
    {
        style.FontFamily = "Lato";
        style.FontSize = 20;
        var block = new Topten.RichTextKit.TextBlock { FontMapper = FontManager.Instance };
        block.AddText(text, style);
        return block;
    }

    private static int CountMode(List<TextInstance> instances, int mode) => instances.Count(instance => instance.Mode == mode);

    [Fact]
    public void GpuStructsMatchTheShaderLayout()
    {
        Assert.Equal(292, Unsafe.SizeOf<TextInstance>());
        Assert.Equal(36, Unsafe.SizeOf<GpuFontGlyphCache.Glyph>());
    }

    [Fact]
    public void GlyphOutlinesEncodeIntoBandsAndCurves()
    {
        var glyph = GpuFontGlyphCache.Get(Lato, GlyphId(Lato, 'O'));
        var (curves, bands, table) = GlyphData();

        Assert.False(glyph.IsEmpty);
        Assert.InRange(glyph.BandCount, 1, 128);
        Assert.Equal(glyph, table[glyph.Index]);
        Assert.True((glyph.CurveStart + glyph.CurveCount) * 3 <= curves.Length);
        Assert.True(glyph.BandOffset + (glyph.BandCount * 4) <= bands.Length);

        Assert.InRange(glyph.Bounds.X, 0f, 0.2f);
        Assert.InRange(glyph.Bounds.Z, 0.5f, 1f);
        Assert.InRange(glyph.Bounds.Y, -0.8f, -0.6f);
        Assert.InRange(glyph.Bounds.W, -0.05f, 0.05f);

        for (int axis = 0; axis < 2; axis++)
        {
            long listed = 0;
            for (int band = 0; band < glyph.BandCount; band++)
            {
                int header = glyph.BandOffset + (axis * glyph.BandCount * 2) + (band * 2);
                Assert.True((bands[header] + bands[header + 1]) * 3 <= curves.Length);
                listed += bands[header + 1];
            }

            Assert.True(listed >= glyph.CurveCount, $"axis {axis} lists {listed} of {glyph.CurveCount} curves");
        }
    }

    [Fact]
    public void GlyphsAreEncodedOnce()
    {
        var id = GlyphId(Lato, 'Q');
        var first = GpuFontGlyphCache.Get(Lato, id);
        var second = GpuFontGlyphCache.Get(Lato, id);

        Assert.Equal(first, second);
        Assert.Equal(first, GlyphData().Table[first.Index]);
    }

    [Fact]
    public void GlyphsWithoutInkAreEmpty()
    {
        var space = GpuFontGlyphCache.Get(Lato, GlyphId(Lato, ' '));

        Assert.True(space.IsEmpty);
        Assert.Equal(default, space);
    }

    [Fact]
    public void InterceptsFindEachStrokeAcrossABand()
    {
        var glyph = GpuFontGlyphCache.Get(Lato, GlyphId(Lato, 'O'));
        var spans = new List<Vector2>();

        GpuFontGlyphCache.Intercepts(glyph, -0.37f, -0.33f, spans);

        Assert.Equal(2, spans.Count);
        Assert.True(spans[0].X < spans[0].Y && spans[0].Y < spans[1].X && spans[1].X < spans[1].Y);
        Assert.InRange(spans[0].X, glyph.Bounds.X - 0.01f, glyph.Bounds.Z);
        Assert.InRange(spans[1].Y, glyph.Bounds.X, glyph.Bounds.Z + 0.01f);
    }

    [Fact]
    public void ColorGlyphsHaveLayers()
    {
        Assert.Null(GpuFontGlyphCache.GetLayers(Lato, GlyphId(Lato, 'A')));

        if (!File.Exists(EmojiFont))
        {
            return;
        }

        using var emoji = SKTypeface.FromFile(EmojiFont);
        var layers = GpuFontGlyphCache.GetLayers(emoji, GlyphId(emoji, 0x1F600));

        Assert.NotNull(layers);
        Assert.True(layers.Length > 1);
        Assert.Contains(layers, layer => layer.Color is { A: > 0 });
        Assert.All(layers, layer => Assert.False(GpuFontGlyphCache.Get(emoji, layer.Glyph).IsEmpty));
    }

    [Fact]
    public void LoadedFontsFillInMissingCharacters()
    {
        var face = FontFallback.CharacterMatcher.MatchCharacter("No Such Family", 400, 5, SKFontStyleSlant.Upright, null, 'A');

        Assert.Contains(face.FamilyName, Fonts.Families);
    }

    [Fact]
    public void MeasuredWidthGrowsWithTheText()
    {
        using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
        painter.TextStyle = new TextStyle("Lato", 20);

        var short1 = painter.MeasureText("Hi");
        var long1 = painter.MeasureText("Hi there, friend");

        Assert.True(long1.X > short1.X * 3);
        Assert.Equal(short1.Y, long1.Y);
        Assert.InRange(short1.Y, 20, 30);
        Assert.Equal(Vector2.Zero, painter.MeasureText(string.Empty));
    }

    [Fact]
    public void TextWrapsAtTheMaximumWidth()
    {
        using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
        painter.TextStyle = new TextStyle("Lato", 20);
        const string text = "The quick brown fox jumps over the lazy dog";

        var line = painter.MeasureText(text);
        var wrapped = painter.MeasureText(text, new Vector2(120, 1000));

        Assert.True(line.X > 300);
        Assert.InRange(wrapped.X, 60, 125);
        Assert.True(wrapped.Y >= line.Y * 3, $"{wrapped.Y} vs {line.Y}");
    }

    [Fact]
    public void LineHeightScalesEachLine()
    {
        using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
        painter.TextStyle = new TextStyle("Lato", 20);
        var normal = painter.MeasureText("One\nTwo");

        painter.TextStyle = painter.TextStyle.WithLineHeight(2);
        var tall = painter.MeasureText("One\nTwo");

        Assert.InRange(tall.Y / normal.Y, 1.8f, 2.2f);
        Assert.Equal(normal.X, tall.X);
    }

    [Fact]
    public void MeasuredRectFollowsTheAlignment()
    {
        using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
        painter.TextStyle = new TextStyle("Lato", 20).WithAlignment(TextFlag.Center);
        var area = new Rect(100, 100, 400, 200);

        var size = painter.MeasureText("Hello", area.Size);
        var rect = painter.MeasureText("Hello", area);

        Assert.Equal(size, rect.Size);
        Assert.Equal(MathF.Floor(area.Left + ((area.Width - size.X) / 2)), rect.Left);
        Assert.Equal(MathF.Floor(area.Top + ((area.Height - size.Y) / 2)), rect.Top);
    }

    [Fact]
    public void ShadowsWidenTheMeasuredSize()
    {
        using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
        painter.TextStyle = new TextStyle("Lato", 20);
        var plain = painter.MeasureText("Hello");

        painter.TextStyle = painter.TextStyle.WithShadow(Color.Black, 4, 4, blur: 2);
        var shadowed = painter.MeasureText("Hello");

        Assert.Equal(plain + new Vector2(2 + 10, 2 + 10), shadowed);
    }

    [Fact]
    public void InvalidStylesAreRejected()
    {
        static void MeasureInNegativeSpace()
        {
            using var painter = Painter.Begin(new DrawList(), new Rect(0, 0, 800, 600));
            painter.MeasureText("Hi", new Vector2(-1, 10));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => TextStyle.Default.WithSize(0).Validate());
        Assert.Throws<ArgumentException>(() => default(TextStyle).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(MeasureInNegativeSpace);
    }

    [Fact]
    public void EachGlyphBecomesOneInstance()
    {
        var instances = Build(Block("Hello", new Style()));

        Assert.Equal(5, instances.Count);
        Assert.All(instances, instance => Assert.Equal(4, instance.Mode));
        Assert.True(instances.Zip(instances.Skip(1)).All(pair => pair.Second.BackgroundRect.X > pair.First.BackgroundRect.X));
        Assert.All(instances, instance => Assert.Equal(20, instance.BackgroundRect.Z));
    }

    [Fact]
    public void EffectsRepeatTheGlyphsUnderneath()
    {
        var style = new Style { TextColor = new SKColorF(1, 1, 1, 1) };
        style.AddEffect(TextEffect.DropShadow(new SKColorF(0, 0, 0, 1), 2, 2, 3));
        style.AddEffect(TextEffect.Outline(new SKColorF(0, 0, 1, 1), 2));

        var instances = Build(Block("Hello", style));

        Assert.Equal(15, instances.Count);
        Assert.All(instances.Take(5), instance => Assert.Equal(3, instance.BorderRadiusV.X));
        Assert.All(instances.Skip(5).Take(5), instance => Assert.Equal(1, instance.BackgroundRect.W));
        Assert.All(instances.Skip(10), instance => Assert.Equal(new Color(1, 1, 1), instance.Color));
    }

    [Fact]
    public void DecorationsAddLines()
    {
        var instances = Build(Block("Hello", new Style { Underline = UnderlineStyle.Solid, StrikeThrough = StrikeThroughStyle.Solid }));

        Assert.Equal(5, CountMode(instances, 4));
        Assert.Equal(2, CountMode(instances, 5));
    }

    [Fact]
    public void GappedUnderlinesSkipDescenders()
    {
        var instances = Build(Block("gyp", new Style { Underline = UnderlineStyle.Gapped, UnderlineOffset = -3 }));

        Assert.Equal(3, CountMode(instances, 4));
        Assert.True(CountMode(instances, 5) > 1);
    }

    [Fact]
    public void SelectionAddsARectangleBehindTheGlyphs()
    {
        var options = GpuFontText.Options.Default;
        options.SelectionStart = 1;
        options.SelectionEnd = 3;
        options.SelectionColor = new Color(0, 1, 1, 0.5f);

        var instances = Build(Block("Hello", new Style()), options);

        Assert.Equal(6, instances.Count);
        Assert.Equal(0, instances[0].Mode);
        Assert.Equal(options.SelectionColor, instances[0].Color);
    }

    [Fact]
    public void PainterTextRecordsGlyphBoxes()
    {
        var list = new DrawList();
        using (var painter = Painter.Begin(list, new Rect(0, 0, 800, 600)))
        {
            painter.TextStyle = new TextStyle("Lato", 20, new Color(1, 0, 0, 0.5f));
            painter.Opacity = 0.5f;
            painter.Text("Hello", new Rect(10, 10, 200, 50));
        }

        Assert.Equal(5, list.Batcher.Texts.Count);
        Assert.Equal(5, list.Batcher.Boxes.Count);
        for (int i = 0; i < 5; i++)
        {
            var box = list.Batcher.Boxes[i];
            Assert.Equal(BoxInstance.TextFlag, box.Flags);
            Assert.Equal(i, box.TextureIndex);
            Assert.Equal(new Color(1, 0, 0, 0.25f), box.Color);
            Assert.True(box.Rect.X >= 10);
        }
    }

    [Fact]
    public void GradientTextPointsAtTheGradient()
    {
        var options = GpuFontText.Options.Default;
        options.HasGradient = true;
        var instances = Build(Block("Hi", new Style()), options);
        var gradient = new GradientInfo { Stops = new GradientStops().Add(new ColorStop(Color.White, 0)).Add(new ColorStop(Color.Black, 1)) };

        var list = new DrawList();
        using (var painter = Painter.Begin(list, new Rect(0, 0, 800, 600)))
        {
            painter.Glyphs(instances, BlendMode.Normal, gradient);
        }

        Assert.Equal(1, list.Batcher.Gradients.Count);
        Assert.All(list.Batcher.Texts.Span.ToArray(), text => Assert.Equal(-1, text.TextureIndex));
    }
}
