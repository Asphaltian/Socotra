namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class ControlTests(GpuHost gpu)
{
    private const string HalfRedHalfBlue = """
        <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 10 10">
            <rect x="0" y="0" width="5" height="10" fill="red"/>
            <rect x="5" y="0" width="5" height="10" fill="blue" fill-opacity="0.5"/>
        </svg>
        """;

    private static RootPanel Root(string styles)
    {
        var root = new UnscaledRoot();
        root.AddClass("root");
        root.StyleSheet.Parse(styles);
        return root;
    }

    private static void Update(RootPanel root, int width, int height) => root.Update(new Rect(0, 0, width, height), 0.016f);

    private Snapshot Paint(RootPanel root, int width = 100, int height = 100)
    {
        var list = new DrawList();
        Update(root, width, height);
        Update(root, width, height);
        root.Paint(list);
        return gpu.Render(list, width, height);
    }

    private static string WriteSvg(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"socotra-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, contents);
        return path;
    }

    [GpuFact]
    public void SvgPanelDrawsTheFileAtThePanelsSize()
    {
        var path = WriteSvg(HalfRedHalfBlue);
        try
        {
            var root = Root("""
                .root { background-color: black; }
                .icon { position: absolute; left: 10px; top: 10px; width: 40px; height: 40px; }
                """);
            root.AddChild(new SvgPanel { Src = path }).AddClass("icon");

            var image = Paint(root);

            image.Expect(15, 30, Rgba.Red);
            image.Expect(44, 30, new Rgba(0, 0, 128), 3);
            image.Expect(5, 30, Rgba.Black);
            image.Expect(55, 30, Rgba.Black);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [GpuFact]
    public void SvgPanelColorPaintsTheWholePicture()
    {
        var path = WriteSvg(HalfRedHalfBlue);
        try
        {
            var root = Root("""
                .root { background-color: black; }
                .icon { position: absolute; left: 10px; top: 10px; width: 40px; height: 40px; }
                """);
            var svg = root.AddChild(new SvgPanel { Src = path, Color = "lime" });
            svg.AddClass("icon");

            var image = Paint(root);
            svg.Color = null;
            var original = Paint(root);

            image.Expect(15, 30, Rgba.Green);
            image.Expect(44, 30, new Rgba(0, 128, 0), 3);
            original.Expect(15, 30, Rgba.Red);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [GpuFact]
    public void SliderDrawsTheFillUpToTheThumb()
    {
        var root = Root(".root { background-color: black; } .slidercontrol { position: absolute; left: 0px; top: 0px; width: 200px; }");
        root.AddChild(new SliderControl(0, 100, 1) { Value = 50 });

        var image = Paint(root, 200, 40);

        image.Expect(30, 20, new Rgba(0x32, 0x73, 0xEB), 3);
        image.Expect(90, 20, new Rgba(0x32, 0x73, 0xEB), 3);
        image.Expect(170, 20, new Rgba(0x3A, 0x3E, 0x47), 3);
        image.Expect(100, 20, Rgba.White, 3);
        image.Expect(30, 4, Rgba.Black, 3);
    }

    [GpuFact]
    public void CheckboxRedrawsWhenClicked()
    {
        var root = Root("""
            .root { background-color: black; pointer-events: all; }
            .checkbox { position: absolute; left: 10px; top: 10px; width: 20px; height: 20px; background-color: red; }
            .checkbox.checked { background-color: lime; }
            .checkmark { display: none; }
            """);
        var checkbox = root.AddChild<Checkbox>();

        var before = Paint(root, 40, 40);
        root.SetMousePosition(new Vector2(20, 20));
        Update(root, 40, 40);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root, 40, 40);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root, 40, 40);
        var after = Paint(root, 40, 40);

        before.Expect(20, 20, Rgba.Red);
        Assert.True(checkbox.Checked);
        after.Expect(20, 20, Rgba.Green);
        after.Expect(5, 5, Rgba.Black);
    }

    private sealed class UnscaledRoot : RootPanel
    {
        protected override float GetScale(Rect bounds) => 1;
    }
}
