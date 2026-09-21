using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public sealed class ColorPopupTests
{
    private static Panel Saved(ColorPickerControl picker) => picker.Children
        .First(section => section.Children.OfType<Label>().Any(label => label.Text == "Saved"))
        .Children.First(panel => panel.HasClass("swatches"));

    private static ColorSwatch[] SavedSwatches(ColorPickerControl picker) => [.. Saved(picker).Children.OfType<ColorSwatch>()];

    private static ColorTextEntry Hex(ColorPickerControl picker) => picker.Children.First(p => p.HasClass("numbers")).Children.OfType<ColorTextEntry>().Single();

    private static Menu[] OpenMenuOptions(RootPanel root) => [.. root.Children.OfType<Popup>().Single(p => p.HasClass("menulist")).Children.OfType<Menu>()];

    private static void Type(RootPanel root, TextEntry entry, string text)
    {
        entry.Focus();
        Update(root);
        entry.Text = text;
        entry.OnValueChanged();
    }

    [Fact]
    public void PressingTheSwatchOpensAPickerThatChangesTheValue()
    {
        var root = Root();
        var control = root.AddChild(new ColorControl { Value = new Color(0, 0, 1), HasAlpha = false });
        control.Style.FlexGrow = 0;
        control.Style.Width = 200;
        var changes = new List<Color>();
        control.ValueChanged = changes.Add;
        Update(root);
        var swatch = control.Children.OfType<ColorSwatch>().Single();

        Press(root, swatch.Box.Rect.Center);
        var popup = root.Children.OfType<Popup>().Single();
        var picker = popup.Children.OfType<ColorPickerControl>().Single();
        Assert.Same(swatch, popup.PopupSource);
        Assert.True(popup.HasClass("below-left"));
        Assert.Equal(new Color(0, 0, 1), picker.Value);
        Assert.False(picker.HasAlpha);
        Assert.True(picker.IsHdr);
        Assert.True(popup.Box.Rect.Top >= swatch.Box.Rect.Bottom);

        Type(root, Hex(picker), "#ff000080");
        Assert.Equal(new Color(1, 0, 0), control.Value);
        Assert.Equal([new Color(1, 0, 0)], changes);
        Assert.Equal("#FF0000", control.Children.OfType<ColorTextEntry>().Single().Text);

        Press(root, new Vector2(1500, 1000));
        Assert.True(popup.IsDeleted);
        Assert.Equal(new Color(1, 0, 0), control.Value);
    }

    [Fact]
    public void TheLayoutMenuSwitchesTheLayoutAndNewPickersKeepIt()
    {
        var root = Root();
        var picker = root.AddChild(new ColorPickerControl());
        Update(root);
        var button = picker.Descendants.OfType<Button>().Single(b => b.HasClass("layout-button"));
        var ring = picker.Descendants.OfType<HueRing>().Single();
        Assert.Equal(DisplayMode.None, ring.Style.Display);

        Click(root, button);
        var options = OpenMenuOptions(root);
        Assert.Equal(["Color Square", "Hue Ring", "Hue Disc", "HSV Sliders", "RGB Sliders"], options.Select(o => o.Text));
        Assert.True(options[0].Checked);
        Assert.False(options[1].Checked);

        Click(root, options[1]);
        Assert.Empty(root.Children.OfType<Popup>());
        Assert.Equal(ColorPickerControl.PickerLayout.HueRing, picker.Layout);
        Assert.Equal(DisplayMode.Flex, ring.Style.Display);

        var next = root.AddChild(new ColorPickerControl());
        Update(root);
        Assert.Equal(ColorPickerControl.PickerLayout.HueRing, next.Layout);
        Click(root, next.Descendants.OfType<Button>().Single(b => b.HasClass("layout-button")));
        options = OpenMenuOptions(root);
        Assert.True(options[1].Checked);
        Assert.False(options[0].Checked);

        Click(root, options[0]);
        Assert.Equal(ColorPickerControl.PickerLayout.ColorSquare, next.Layout);
        Assert.Equal(ColorPickerControl.PickerLayout.ColorSquare, new ColorPickerControl().Layout);
    }

    [Fact]
    public void RightClickingASavedColorReplacesRemovesOrClears()
    {
        var root = Root();
        var picker = root.AddChild(new ColorPickerControl { Palette = new ColorPalette(), Value = new Color(0.1f, 0.2f, 0.3f) });
        Update(root);
        var original = SavedSwatches(picker).Select(s => s.Color).ToArray();
        var current = Hex(picker).Text;

        Mouse(root, SavedSwatches(picker)[1], "onrightclick");
        var options = OpenMenuOptions(root);
        Assert.Equal(["Replace with current", "Remove", "Clear all"], options.Select(o => o.Text));
        Click(root, options[0]);
        Assert.Empty(root.Children.OfType<Popup>());
        Assert.Equal(original.Length, SavedSwatches(picker).Length);
        Assert.Equal(current, SavedSwatches(picker)[1].Color.ToText());

        Mouse(root, SavedSwatches(picker)[0], "onrightclick");
        Click(root, OpenMenuOptions(root)[1]);
        Assert.Equal(original.Length - 1, SavedSwatches(picker).Length);
        Assert.Equal(current, SavedSwatches(picker)[0].Color.ToText());

        Mouse(root, SavedSwatches(picker)[0], "onrightclick");
        Click(root, OpenMenuOptions(root)[2]);
        Assert.Empty(SavedSwatches(picker));

        foreach (var color in original)
        {
            picker.Value = color.ToColor();
            Mouse(root, Saved(picker).Children.OfType<IconPanel>().Single(), "onclick");
        }

        Assert.Equal(original.Select(c => c.ToText()), SavedSwatches(picker).Select(s => s.Color.ToText()));
    }
}
