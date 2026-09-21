using System.Globalization;

namespace Socotra;

[StyleSheet.Inline("colorswatch", Styles)]
internal class ColorSwatch : Panel
{
    private const string Styles = """
        .colorswatch
        {
            position: relative;
            border-radius: 4px;
            overflow: hidden;
            flex-shrink: 0;

            > .fill
            {
                position: absolute;
                left: 0;
                top: 0;
                right: 0;
                bottom: 0;
            }

            > .hdr
            {
                position: absolute;
                right: 2px;
                bottom: 1px;
                font-size: 9px;
                font-weight: 600;
                padding: 1px 3px;
                border-radius: 3px;
                background-color: #000c;
                color: #fff;
            }

            &.compact > .hdr
            {
                right: 0;
                bottom: 0;
                font-size: 7px;
                padding: 0 2px;
                border-radius: 2px 0 0 0;
            }
        }
        """;

    private readonly Panel _fill;
    private readonly Label _badge;

    public ColorSwatch()
    {
        AddClass("colorswatch");
        Style.BackgroundSizeX = ColorPickerTextures.CheckerSize;
        Style.BackgroundSizeY = ColorPickerTextures.CheckerSize;
        Style.BackgroundRepeat = BackgroundRepeat.Repeat;
        _fill = Add.Panel("fill");
        _badge = Add.Label(null, "hdr");
        _badge.Style.Display = DisplayMode.None;
    }

    public bool Compact { get; set; }

    public PickerColor Color { get; private set; }

    public void Set(PickerColor color, bool showAlpha = true, bool showBrightness = true)
    {
        Color = color;
        var fill = color.BaseColor;
        if (!showAlpha)
        {
            fill = fill.WithAlpha(1);
        }

        var transparent = fill.A < 1;
        Style.BackgroundImage = transparent ? ColorPickerTextures.Checkerboard : null;
        Style.BackgroundColor = transparent ? null : fill;
        _fill.Style.Display = transparent ? DisplayMode.Flex : DisplayMode.None;
        _fill.Style.BackgroundColor = fill;

        var hdr = showBrightness && color.Brightness != 1;
        _badge.Style.Display = hdr ? DisplayMode.Flex : DisplayMode.None;
        if (hdr)
        {
            _badge.Text = "×" + color.Brightness.ToString(Compact ? "0" : "0.##", CultureInfo.InvariantCulture);
        }
    }
}
