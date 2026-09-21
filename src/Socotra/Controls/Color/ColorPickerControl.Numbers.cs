using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Socotra;

public partial class ColorPickerControl
{
    private const string NumbersStyles = """
        .colorpickercontrol
        {
            > .numbers
            {
                flex-direction: row;
                gap: 6px;

                > .fields
                {
                    flex-direction: row;
                    flex-grow: 1;
                    gap: 6px;

                    > numberentry
                    {
                        flex-grow: 1;
                        flex-basis: 0px;
                        min-width: 0px;

                        .prefix-label
                        {
                            margin-right: 5px;
                            opacity: 0.6;
                        }
                    }
                }

                > .colortextentry
                {
                    flex-grow: 1;
                }
            }
        }
        """;

    private ButtonGroup _tabs;
    private ColorTextEntry _hexEntry;
    private ColorTextEntry _textEntry;
    private Panel _rgbFields;
    private Panel _hsvFields;
    private NumberEntry _red;
    private NumberEntry _green;
    private NumberEntry _blue;
    private NumberEntry _alpha255;
    private NumberEntry _hue;
    private NumberEntry _saturation;
    private NumberEntry _value;
    private NumberEntry _alpha100;

    private static string Whole(float value) => MathF.Round(value).ToString(CultureInfo.InvariantCulture);

    [MemberNotNull(nameof(_tabs))]
    private void BuildTabs(Panel header)
    {
        _tabs = header.AddChild(new ButtonGroup());
        _tabs.AddClass("tabs");
        AddTab("HEX", NumberMode.Hex);
        AddTab("RGB", NumberMode.Rgb);
        AddTab("HSV", NumberMode.Hsv);
        _tabs.Value = "HEX";
    }

    private void AddTab(string text, NumberMode mode) => _tabs.AddButton(text, () => SetNumberMode(mode)).Value = text;

    [MemberNotNull(nameof(_hexEntry), nameof(_rgbFields), nameof(_hsvFields), nameof(_red), nameof(_green), nameof(_blue), nameof(_alpha255), nameof(_hue), nameof(_saturation), nameof(_value), nameof(_alpha100))]
    private void BuildNumbers()
    {
        var numbers = Add.Panel("numbers");
        _hexEntry = numbers.AddChild(new ColorTextEntry());
        WireTextEntry(_hexEntry);

        _rgbFields = numbers.Add.Panel("fields");
        _red = Channel(_rgbFields, "R", 255, v => SetRgb(r: v / 255));
        _green = Channel(_rgbFields, "G", 255, v => SetRgb(g: v / 255));
        _blue = Channel(_rgbFields, "B", 255, v => SetRgb(b: v / 255));
        _alpha255 = Channel(_rgbFields, "A", 255, v => SetAlpha(v / 255));

        _hsvFields = numbers.Add.Panel("fields");
        _hue = Channel(_hsvFields, "H", 360, SetHue);
        _saturation = Channel(_hsvFields, "S", 100, v => SetSaturationValue(v / 100, _color.Value));
        _value = Channel(_hsvFields, "V", 100, v => SetValue(v / 100));
        _alpha100 = Channel(_hsvFields, "A", 100, v => SetAlpha(v / 100));

        SetNumberMode(NumberMode.Hex);
    }

    [MemberNotNull(nameof(_textEntry))]
    private void BuildTextRow()
    {
        var row = Add.Panel("text-row");
        _textEntry = row.AddChild(new ColorTextEntry { Placeholder = "#ff8800, rgba( 255, 136, 0, 0.5 ), white * 4 ..." });
        WireTextEntry(_textEntry);
    }

    private NumberEntry Channel(Panel parent, string prefix, float max, Action<float> set)
    {
        var entry = parent.AddChild(new NumberEntry { Prefix = prefix, WholeNumbers = true, MinValue = 0, MaxValue = max });
        entry.OnTextEdited = text =>
        {
            if (TryParseNumber(text, out var v))
            {
                set(Math.Clamp(v, 0, max));
            }
        };
        entry.AddEventListener("onblur", OnTextBlurred);
        entry.ScrubEnded += Commit;
        return entry;
    }

    private void WireTextEntry(ColorTextEntry entry)
    {
        entry.ColorEntered = text =>
        {
            if (PickerColor.TryParse(text, _color.Hue, out var color))
            {
                SetColor(color);
            }
        };
        entry.Blurred = OnTextBlurred;
    }

    private void OnTextBlurred()
    {
        Commit();
        Sync();
    }

    private void SetNumberMode(NumberMode mode)
    {
        Show(_hexEntry, mode == NumberMode.Hex);
        Show(_rgbFields, mode == NumberMode.Rgb);
        Show(_hsvFields, mode == NumberMode.Hsv);
    }

    private void ShowAlphaNumbers(bool show)
    {
        Show(_alpha255, show);
        Show(_alpha100, show);
    }

    private void SyncNumbers()
    {
        var color = _color.BaseColor;
        _red.Value = Whole(color.R * 255);
        _green.Value = Whole(color.G * 255);
        _blue.Value = Whole(color.B * 255);
        _alpha255.Value = Whole(_color.Alpha * 255);
        _hue.Value = Whole(_color.Hue);
        _saturation.Value = Whole(_color.Saturation * 100);
        _value.Value = Whole(_color.Value * 100);
        _alpha100.Value = Whole(_color.Alpha * 100);
    }

    private void SyncText()
    {
        var text = _color.ToText();
        _hexEntry.Value = text;
        _textEntry.Value = text;
    }

    private enum NumberMode
    {
        Hex,
        Rgb,
        Hsv,
    }
}
