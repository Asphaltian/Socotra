namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class PanelTests(GpuHost gpu)
{
    private Snapshot Paint(string styles, Action<RootPanel> build, int width = 100, int height = 100)
    {
        var root = new UnscaledRoot();
        root.AddClass("root");
        root.StyleSheet.Parse(styles);
        build(root);
        var list = new DrawList();
        root.Update(new Rect(0, 0, width, height), 0.016f);
        root.Update(new Rect(0, 0, width, height), 0.016f);
        root.Paint(list);
        return gpu.Render(list, width, height);
    }

    [GpuFact]
    public void BackgroundsFillTheBorderBox()
    {
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; left: 10px; top: 20px; width: 30px; height: 40px; background-color: red; }
            """, root => root.AddChild<Panel>("box"));

        image.Expect(10, 20, Rgba.Red);
        image.Expect(39, 59, Rgba.Red);
        image.Expect(9, 30, Rgba.Black);
        image.Expect(40, 30, Rgba.Black);
        image.Expect(20, 60, Rgba.Black);
    }

    [GpuFact]
    public void BackgroundBlendModesMixTheImageWithTheColor()
    {
        var yellow = Texture.FromPixels(1, 1, [255, 255, 0, 255]);
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; top: 0px; width: 20px; height: 20px; background-color: magenta; }
            .multiply { left: 0px; background-blend-mode: multiply; }
            .lighten { left: 30px; background-blend-mode: lighten; }
            .normal { left: 60px; }
            """, root =>
        {
            foreach (var mode in (string[])["multiply", "lighten", "normal"])
            {
                root.AddChild<Panel>($"box {mode}").Style.BackgroundImage = yellow;
            }
        });

        image.Expect(10, 10, new Rgba(255, 0, 0));
        image.Expect(40, 10, new Rgba(255, 255, 255));
        image.Expect(70, 10, new Rgba(255, 255, 0));
    }

    [GpuFact]
    public void BordersDrawInsideTheBox()
    {
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; left: 10px; top: 10px; width: 60px; height: 60px; background-color: blue; border: 10px solid lime; }
            """, root => root.AddChild<Panel>("box"));

        image.Expect(12, 40, Rgba.Green);
        image.Expect(40, 40, Rgba.Blue);
        image.Expect(67, 40, Rgba.Green);
    }

    [GpuFact]
    public void LaterSiblingsAndHigherZIndexDrawOnTop()
    {
        var image = Paint("""
            .root { background-color: black; }
            .a, .b { position: absolute; top: 10px; width: 40px; height: 40px; }
            .a { left: 10px; background-color: red; z-index: 2; }
            .b { left: 30px; background-color: blue; }
            """, root =>
            {
                root.AddChild<Panel>("a");
                root.AddChild<Panel>("b");
            });

        image.Expect(35, 30, Rgba.Red);
        image.Expect(60, 30, Rgba.Blue);
    }

    [GpuFact]
    public void OverflowHiddenClipsChildren()
    {
        var image = Paint("""
            .root { background-color: black; }
            .clip { position: absolute; left: 10px; top: 10px; width: 40px; height: 40px; overflow: hidden; }
            .inner { width: 80px; height: 80px; flex-shrink: 0; background-color: red; }
            """, root => root.AddChild<Panel>("clip").AddChild<Panel>("inner"));

        image.Expect(45, 45, Rgba.Red);
        image.Expect(60, 30, Rgba.Black);
        image.Expect(30, 60, Rgba.Black);
    }

    [GpuFact]
    public void OpacityMultipliesDownTheTree()
    {
        var image = Paint("""
            .root { background-color: black; }
            .outer { position: absolute; left: 0; top: 0; width: 100px; height: 100px; opacity: 0.5; }
            .inner { width: 100px; height: 100px; background-color: white; }
            """, root => root.AddChild<Panel>("outer").AddChild<Panel>("inner"));

        image.Expect(50, 50, Rgba.Of(0.5f, 0.5f, 0.5f), 2);
    }

    [GpuFact]
    public void TransformsMoveWhatIsDrawn()
    {
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; left: 10px; top: 10px; width: 20px; height: 20px; background-color: red; transform: translateX(50px); }
            """, root => root.AddChild<Panel>("box"));

        image.Expect(15, 20, Rgba.Black);
        image.Expect(65, 20, Rgba.Red);
    }

    [GpuFact]
    public void FixedPanelsDrawAboveTheirAncestors()
    {
        var image = Paint("""
            .root { background-color: black; }
            .clip { position: absolute; left: 0; top: 0; width: 20px; height: 20px; overflow: hidden; }
            .overlay { position: fixed; left: 50px; top: 50px; width: 20px; height: 20px; background-color: red; }
            .cover { position: absolute; left: 40px; top: 40px; width: 40px; height: 40px; background-color: blue; }
            """, root =>
            {
                root.AddChild<Panel>("clip").AddChild<Panel>("overlay");
                root.AddChild<Panel>("cover");
            });

        image.Expect(60, 60, Rgba.Red);
        image.Expect(45, 45, Rgba.Blue);
    }

    [GpuFact]
    public void FiltersRenderTheSubtreeThroughALayer()
    {
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; left: 10px; top: 10px; width: 40px; height: 40px; background-color: white; filter: invert(1); }
            """, root => root.AddChild<Panel>("box"));

        image.Expect(30, 30, Rgba.Black);
        image.Expect(70, 70, Rgba.Black);
    }

    [GpuFact]
    public void BoxShadowsDrawOutsideTheBox()
    {
        var image = Paint("""
            .root { background-color: black; }
            .box { position: absolute; left: 20px; top: 20px; width: 20px; height: 20px; background-color: white; box-shadow: 30px 0 0 0 red; }
            """, root => root.AddChild<Panel>("box"));

        image.Expect(30, 30, Rgba.White);
        image.Expect(60, 30, Rgba.Red);
    }

    [GpuTheory]
    [InlineData("radial-gradient(red, blue)")]
    [InlineData("radial-gradient(ellipse at 50% 50%, rgba(255, 0, 0, 1) 0%, rgba(0, 0, 255, 1) 100%)")]
    [InlineData("radial-gradient(ellipse at 50% 50%, rgba(255, 0, 0, 0.9) 0%, rgba(255, 0, 0, 0.4) 40%, rgba(0, 0, 255, 0.9) 100%)")]
    public void RadialGradientsGoFromTheMiddleOut(string gradient)
    {
        var image = Paint($$"""
            .root { background-color: black; }
            .box { position: absolute; left: 0px; top: 0px; width: 100px; height: 60px; background-image: {{gradient}}; }
            """, root => root.AddChild<Panel>("box"));

        Assert.True(image[50, 30].R > 200 && image[50, 30].B < 60);
        Assert.True(image[99, 30].B > image[99, 30].R);
    }

    [GpuFact]
    public void TextClippedBackgroundShowsOnlyThroughTheText()
    {
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
        const string Styles = """
            .root { background-color: black; }
            .box { position: absolute; left: 10px; top: 10px; width: 180px; height: 80px; }
            .label { font-family: Lato; font-size: 60px; }
            """;
        static void Build(RootPanel root) => root.AddChild<Panel>("box").AddChild<Label>("label").Text = "Hi";

        var clipped = Paint(Styles + ".box { background-color: red; background-clip: text; } .label { color: blue; }", Build, 200, 100);
        var plain = Paint(Styles + ".label { color: red; }", Build, 200, 100);

        int lit = 0, mismatched = 0, clippedInk = 0, plainInk = 0;
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                Assert.Equal(0, clipped[x, y].B);
                lit += clipped[x, y].R > 127 ? 1 : 0;
                mismatched += Math.Abs(clipped[x, y].R - plain[x, y].R) > 2 ? 1 : 0;
                clippedInk += clipped[x, y].R;
                plainInk += plain[x, y].R;
            }
        }

        Assert.True(lit > 200);
        Assert.True(mismatched <= lit / 100, $"{mismatched} of {lit} pixels differ");
        Assert.InRange(clippedInk, plainInk * 0.99, plainInk * 1.01);
    }

    private sealed class UnscaledRoot : RootPanel
    {
        protected override float GetScale(Rect bounds) => 1;
    }
}
