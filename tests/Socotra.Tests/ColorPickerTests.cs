using System.Text.Json;

namespace Socotra.Tests;

public class ColorPickerTests
{
    private static RootPanel CreateRoot()
    {
        var root = new UnscaledRoot();
        root.StyleSheet.Parse("rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }");
        return root;
    }

    private static void Update(RootPanel root)
    {
        root.Update(new Rect(0, 0, 1000, 1000), 0.016f);
        root.Update(new Rect(0, 0, 1000, 1000), 0.016f);
    }

    private static Panel Palette(ColorPickerControl picker, string title) => picker.Children
        .First(section => section.Children.OfType<Label>().Any(label => label.Text == title))
        .Children.First(panel => panel.HasClass("swatches"));

    private static ColorTextEntry Hex(ColorPickerControl picker) => picker.Children.First(p => p.HasClass("numbers")).Children.OfType<ColorTextEntry>().Single();

    private static void Type(RootPanel root, TextEntry entry, string text)
    {
        entry.Focus();
        Update(root);
        Assert.True(entry.HasFocus);
        entry.Text = text;
        entry.OnValueChanged();
    }

    private static void Mouse(Panel panel, string name, Vector2 position = default) =>
        panel.DispatchEventImmediate(new MousePanelEvent(name, panel, "mouseleft") { LocalPosition = position });

    private static void AssertClose(Color expected, Color actual)
    {
        Assert.Equal(expected.R, actual.R, 0.01f);
        Assert.Equal(expected.G, actual.G, 0.01f);
        Assert.Equal(expected.B, actual.B, 0.01f);
        Assert.Equal(expected.A, actual.A, 0.01f);
    }

    [Fact]
    public void PickerColorRoundTripsSdrColors()
    {
        var color = new Color(0.2f, 0.6f, 0.9f, 0.5f);
        var picker = PickerColor.FromColor(color);

        Assert.Equal(1, picker.Brightness);
        Assert.Equal(0.5f, picker.Alpha);
        AssertClose(color, picker.ToColor());
    }

    [Fact]
    public void PickerColorFoldsHdrIntoBrightness()
    {
        var color = new Color(1, 0.5f, 0).ScaleBrightness(4);
        var picker = PickerColor.FromColor(color);

        Assert.Equal(4, picker.Brightness, 0.01f);
        Assert.True(picker.BaseColor.R <= 1 && picker.BaseColor.G <= 1 && picker.BaseColor.B <= 1);
        AssertClose(color, picker.ToColor());
    }

    [Fact]
    public void PickerColorTextIsHexWithTheBrightnessAfterIt()
    {
        Assert.Equal("#3273EB", PickerColor.FromColor(Color.Parse("#3273eb")!.Value).ToText());
        Assert.Equal("#3273EB80", PickerColor.FromColor(Color.Parse("#3273eb80")!.Value).ToText());

        Assert.True(PickerColor.TryParse("#3273eb * 4", 0, out var typed));
        Assert.Equal(4, typed.Brightness);
        Assert.Equal("#3273EB * 4", typed.ToText());
        AssertClose(Color.Parse("#3273eb * 4")!.Value, typed.ToColor());

        var raw = PickerColor.FromColor(Color.Parse("#3273eb * 4")!.Value);
        Assert.Equal(1, raw.Value, 0.001f);
        Assert.True(raw.Brightness > 1);
        Assert.True(PickerColor.TryParse(raw.ToText(), 0, out var parsed));
        AssertClose(raw.ToColor(), parsed.ToColor());
    }

    [Fact]
    public void PickerColorParsesStylesheetColorsAndKeepsTheHueOfGreys()
    {
        Assert.True(PickerColor.TryParse("white", 0, out var named));
        Assert.Equal(1, named.Value);
        Assert.True(PickerColor.TryParse("rgba( 255, 0, 0, 0.5 )", 0, out var rgba));
        Assert.Equal(0.5f, rgba.Alpha, 0.01f);
        Assert.True(PickerColor.TryParse("white * 2", 0, out var bright));
        Assert.Equal(2, bright.Brightness, 0.01f);
        Assert.False(PickerColor.TryParse("nonsense", 0, out _));
        Assert.False(PickerColor.TryParse("", 0, out _));

        Assert.Equal(210, PickerColor.FromColor(new Color(0.5f, 0.5f, 0.5f), 210).Hue);
        Assert.Equal(210, PickerColor.FromColor(Color.Black, 210).Hue);
        Assert.Equal(0, PickerColor.FromColor(new Color(1, 0, 0), 210).Hue);
    }

    [Fact]
    public void ValueRoundTripsThroughThePicker()
    {
        var picker = new ColorPickerControl();
        var color = new Color(0.25f, 0.5f, 0.75f, 0.5f);

        picker.Value = color;
        Assert.Equal(color, picker.Value);
        Assert.Equal("#3F7FBF7F", Hex(picker).Text);

        var hdr = new Color(1, 0.5f, 0).ScaleBrightness(8);
        picker.Value = hdr;
        Assert.Equal(hdr, picker.Value);
        Assert.Equal("×8", picker.Descendants.OfType<Label>().First(l => l.HasClass("hdr")).Text);
    }

    [Fact]
    public void TypingAColorSetsTheValue()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl());
        var changes = new List<Color>();
        picker.ValueChanged = changes.Add;
        Update(root);

        Type(root, Hex(picker), "#ff000080 * 4");

        Assert.True(picker.Value.R > 1);
        Assert.Equal(128 / 255.0f, picker.Value.A, 0.001f);
        Assert.Equal(picker.Value, changes.Last());

        Type(root, Hex(picker), "not a color");
        Assert.True(Hex(picker).HasClass("invalid"));
        Assert.Single(changes);
    }

    [Fact]
    public void AlphaAndHdrCanBeTurnedOff()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { HasAlpha = false, IsHdr = false });
        Update(root);

        Type(root, Hex(picker), "#ff000080 * 4");

        AssertClose(new Color(1, 0, 0), picker.Value);
        var brightnessRow = picker.Children.Single(p => p.HasClass("slider-row") && p.Children.OfType<SliderControl>().Any());
        Assert.Equal(DisplayMode.None, brightnessRow.Style.Display);
    }

    [Fact]
    public void DraggingTheSquarePicksSaturationAndValue()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Value = new Color(1, 0, 0) });
        var square = picker.Children.First(p => p.HasClass("body")).Children.OfType<ColorSquare>().Single();
        Update(root);
        var size = square.Box.Rect.Size;

        Mouse(square, "onmousedown", new Vector2(size.X * 0.5f, size.Y * 0.25f));
        Mouse(square, "onmouseup");

        AssertClose(new Color(0.75f, 0.375f, 0.375f), picker.Value);
        var recent = Palette(picker, "Recent").Children.OfType<ColorSwatch>().First();
        AssertClose(picker.Value, recent.Color.ToColor());
    }

    [Fact]
    public void BrightnessSliderMakesTheColorBrighterThanWhite()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Value = new Color(1, 0, 0) });
        Update(root);
        var slider = picker.Descendants.OfType<SliderControl>().Single();
        var track = slider.Descendants.First(p => p.HasClass("track")).Box.Rect;

        root.SetMousePosition(new Vector2(track.Left + (track.Width / 2), track.Center.Y));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.True(picker.Value.R > 1);
        Assert.Equal("#FF0000 * 16", Hex(picker).Text);
    }

    [Fact]
    public void ClickingASwatchPicksItsColorAndTheOldSwatchGoesBack()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Value = new Color(0, 0, 1) });
        Update(root);

        var black = Palette(picker, "Saved").Children.OfType<ColorSwatch>().ElementAt(1);
        Mouse(black, "onclick");
        Assert.Equal(Color.Black, picker.Value);
        Assert.Equal("#000000", Hex(picker).Text);

        var old = picker.Descendants.OfType<ColorSwatch>().Single(s => s.HasClass("old"));
        Mouse(old, "onclick");
        Assert.Equal(new Color(0, 0, 1), picker.Value);
    }

    [Fact]
    public void RgbTabShowsChannelsThatSetTheColor()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Value = Color.Black });
        Update(root);
        var tabs = picker.Descendants.OfType<ButtonGroup>().Single();
        var rgb = tabs.Children.OfType<Button>().Single(b => b.Text == "RGB");

        Mouse(rgb, "onclick");
        Update(root);
        Assert.Equal("RGB", tabs.Value);
        Assert.True(rgb.HasClass("active"));
        Assert.Equal(DisplayMode.None, Hex(picker).Style.Display);

        var red = picker.Descendants.OfType<NumberEntry>().Single(e => e.Prefix == "R");
        Type(root, red, "255");
        Assert.Equal(new Color(1, 0, 0), picker.Value);

        red.Blur();
        Update(root);
        Mouse(Palette(picker, "Saved").Children.OfType<ColorSwatch>().ElementAt(1), "onclick");
        Assert.Equal("0", red.Text);
    }

    [Fact]
    public void AddingToTheSavedPaletteKeepsTheColor()
    {
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Palette = new ColorPalette(), Value = new Color(0.1f, 0.2f, 0.3f) });
        Update(root);
        var saved = Palette(picker, "Saved");
        var count = saved.Children.OfType<ColorSwatch>().Count();

        Mouse(saved.Children.OfType<IconPanel>().Single(), "onclick");
        saved = Palette(picker, "Saved");
        Assert.Equal(count + 1, saved.Children.OfType<ColorSwatch>().Count());
        AssertClose(picker.Value, saved.Children.OfType<ColorSwatch>().Last().Color.ToColor());

        Mouse(saved.Children.OfType<IconPanel>().Single(), "onclick");
        Assert.Equal(count + 1, Palette(picker, "Saved").Children.OfType<ColorSwatch>().Count());
    }

    [Fact]
    public void PickersShareAPaletteThatSavesAndLoadsAsJson()
    {
        var palette = new ColorPalette();
        var root = CreateRoot();
        var picker = root.AddChild(new ColorPickerControl { Palette = palette, Value = new Color(0.1f, 0.2f, 0.3f) });
        var other = root.AddChild(new ColorPickerControl { Palette = palette });
        var changes = 0;
        palette.Changed += () => changes++;
        Update(root);

        Mouse(Palette(picker, "Saved").Children.OfType<IconPanel>().Single(), "onclick");
        Update(root);

        Assert.Equal(palette.Saved.Count, Palette(other, "Saved").Children.OfType<ColorSwatch>().Count());
        Assert.Equal(1, changes);
        var loaded = new ColorPalette();
        loaded.Load(palette.Save());
        Assert.Equal(palette.Saved, loaded.Saved);
        Assert.Throws<JsonException>(() => loaded.Load("{}"));
        Assert.Same(ColorPalette.Shared, new ColorPickerControl().Palette);
    }

    [Fact]
    public void TurningAlphaOffRedrawsTheSwatches()
    {
        var picker = new ColorPickerControl();
        var swatch = Palette(picker, "Saved").Children.OfType<ColorSwatch>().ElementAt(2);
        Assert.Null(swatch.Style.BackgroundColor);

        picker.HasAlpha = false;
        Assert.False(swatch.IsDeleting);
        Assert.Equal(1, swatch.Style.BackgroundColor!.Value.A);

        picker.HasAlpha = true;
        Assert.Null(swatch.Style.BackgroundColor);
    }

    [Fact]
    public void OtherLayoutsShowTheirOwnControls()
    {
        var picker = new ColorPickerControl { Value = new Color(0, 1, 0) };
        var ring = picker.Descendants.OfType<HueRing>().Single();
        var disc = picker.Descendants.OfType<HueDisc>().Single();
        var square = picker.Descendants.OfType<ColorSquare>().First(s => s.Parent != ring);
        Assert.Equal(DisplayMode.None, ring.Style.Display);

        picker.Layout = ColorPickerControl.PickerLayout.HueRing;
        Assert.Equal(DisplayMode.Flex, ring.Style.Display);
        Assert.Equal(DisplayMode.None, square.Style.Display);

        picker.Layout = ColorPickerControl.PickerLayout.HueDisc;
        Assert.Equal(DisplayMode.Flex, disc.Style.Display);

        picker.Layout = ColorPickerControl.PickerLayout.RgbSliders;
        Assert.Equal(DisplayMode.None, picker.Children.First(p => p.HasClass("body")).Style.Display);
    }

    [Fact]
    public void ColorControlTakesTypedColors()
    {
        var root = CreateRoot();
        var control = root.AddChild(new ColorControl { Value = new Color(1, 1, 1) });
        var entry = control.Children.OfType<ColorTextEntry>().Single();
        Color? reported = null;
        control.ValueChanged = color => reported = color;
        Update(root);
        Assert.Equal("#FFFFFF", entry.Text);

        Type(root, entry, "#ff8000 * 2");
        Assert.NotNull(reported);
        Assert.True(control.Value.R > 1);

        control.IsHdr = false;
        control.HasAlpha = false;
        AssertClose(new Color(1, 0.5f, 0), control.Value);
        Type(root, entry, "#00ff0080 * 2");
        AssertClose(new Color(0, 1, 0), control.Value);
    }

    [Fact]
    public void TurningAlphaOffMakesThePickedColorOpaque()
    {
        var picker = new ColorPickerControl { Value = new Color(1, 0, 0, 0.5f) };

        picker.HasAlpha = false;

        Assert.Equal(1, picker.Value.A);
        AssertClose(new Color(1, 0, 0), picker.Value);
    }
}
