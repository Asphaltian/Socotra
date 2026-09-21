namespace Socotra.Vulkan.Tests;

[Collection(GpuCollection.Name)]
public class PopupDrawTests(GpuHost gpu)
{
    private const int Size = 100;

    private static void Update(RootPanel root) => root.Update(new Rect(0, 0, Size, Size), 0.016f);

    private static RootPanel Root(string styles)
    {
        var root = new UnscaledRoot();
        root.AddClass("root");
        root.StyleSheet.Parse(styles);
        return root;
    }

    private Snapshot Paint(RootPanel root)
    {
        Update(root);
        Update(root);
        var list = new DrawList();
        root.Paint(list);
        return gpu.Render(list, Size, Size);
    }

    [GpuFact]
    public void APopupDrawsAbovePanelsAddedAfterIt()
    {
        var root = Root("""
            .root { background-color: black; }
            .source { position: absolute; left: 10px; top: 10px; width: 40px; height: 10px; background-color: lime; }
            .popup-panel { width: 40px; height: 30px; background-color: red; }
            .cover { position: absolute; left: 0px; top: 0px; width: 100px; height: 100px; background-color: blue; }
            """);
        var source = root.AddChild<Panel>("source");
        Update(root);
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        root.AddChild<Panel>("cover");

        var image = Paint(root);

        Assert.Equal(new Rect(10, 20, 40, 30), popup.Box.Rect);
        image.Expect(12, 22, Rgba.Red);
        image.Expect(47, 47, Rgba.Red);
        image.Expect(55, 30, Rgba.Blue);
        image.Expect(30, 15, Rgba.Blue);
    }

    [GpuFact]
    public void ATooltipDrawsBesideTheMouse()
    {
        var root = Root("""
            .root { background-color: black; pointer-events: all; }
            .item { position: absolute; left: 0px; top: 40px; width: 60px; height: 40px; background-color: blue; }
            .tip { width: 20px; height: 10px; background-color: red; }
            """);
        var item = root.AddChild<Panel>("item");
        item.OnTooltip = _ => { };
        item.TooltipClass = "tip";
        Update(root);
        root.SetMousePosition(new Vector2(30, 60));

        var image = Paint(root);

        Assert.True(root.Tooltips.IsShowing);
        image.Expect(52, 32, Rgba.Red);
        image.Expect(69, 39, Rgba.Red);
        image.Expect(52, 45, Rgba.Blue);
        image.Expect(75, 35, Rgba.Black);
    }

    [GpuFact]
    public void PressingAColorFieldsSwatchDrawsThePickerUnderIt()
    {
        const int width = 400;
        const int height = 700;
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
        var root = Root(".root { background-color: black; pointer-events: all; font-family: Lato; font-size: 12px; }");
        var control = root.AddChild(new ColorControl { Value = new Color(1, 0, 0) });
        control.Style.Position = PositionMode.Absolute;
        control.Style.Left = 10;
        control.Style.Top = 10;
        control.Style.Width = 200;
        Frame();
        var swatch = control.Children.OfType<ColorSwatch>().Single();
        root.SetMousePosition(swatch.Box.Rect.Center);
        Frame();
        root.SetMouseButton(MouseButtons.Left, true);
        Frame();
        root.SetMouseButton(MouseButtons.Left, false);
        Frame();
        Frame();

        var list = new DrawList();
        root.Paint(list);
        var image = gpu.Render(list, width, height);

        var picker = root.Descendants.OfType<ColorPickerControl>().Single();
        var square = picker.Descendants.OfType<ColorSquare>().Single(x => x.IsVisible).Box.Rect;
        var current = picker.Descendants.OfType<ColorSwatch>().First(x => x.Parent!.HasClass("compare") && !x.HasClass("old")).Box.Rect;
        Assert.True(square.Top > swatch.Box.Rect.Bottom);
        image.Expect((int)current.Center.X, (int)current.Center.Y, Rgba.Red);
        image.Expect((int)square.Left + 2, (int)square.Top + 2, Rgba.White, 8);
        image.Expect((int)square.Left + 2, (int)square.Bottom - 2, Rgba.Black, 8);
        image.Expect((int)square.Right - 2, (int)square.Bottom - 2, Rgba.Black, 8);
        var middle = image[(int)square.Right - 2, (int)square.Center.Y];
        Assert.True(middle.R > 64 && middle.G < 16 && middle.B < 16, $"{middle}");
        image.Expect(width - 5, height - 5, Rgba.Black);

        void Frame() => root.Update(new Rect(0, 0, width, height), 0.016f);
    }

    private sealed class UnscaledRoot : RootPanel
    {
        protected override float GetScale(Rect bounds) => 1;
    }
}
