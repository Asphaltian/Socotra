using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Socotra;

/// <summary>
/// A full color picker: a square with hue and alpha strips to pick the color, a brightness slider for colors brighter
/// than white, number fields for hex, RGB or HSV, a palette of recent and kept colors, and a box that takes a color
/// written any way a stylesheet would. The menu in the corner lets the user swap the square for a hue ring, a hue disc
/// or sliders, and right-clicking a kept color replaces or removes it.
/// </summary>
/// <example>
/// <code>
/// // Pick a light color with no transparency
/// var picker = new ColorPickerControl { Parent = panel, HasAlpha = false, Value = light.Color };
/// picker.ValueChanged = color => light.Color = color;
/// </code>
/// </example>
[StyleSheet.Inline("colorpickercontrol", Styles + NumbersStyles)]
public partial class ColorPickerControl : Panel
{
    private const int MaxStops = 8;

    private const string Styles = """
        .colorpickercontrol
        {
            flex-direction: column;
            flex-shrink: 0;
            gap: 10px;
            width: 300px;
            padding: 12px;
            pointer-events: all;

            > .header
            {
                flex-direction: row;
                align-items: center;
                gap: 8px;

                > .compare
                {
                    flex-direction: row;
                    flex-grow: 1;
                    height: 26px;
                    border-radius: 5px;
                    overflow: hidden;

                    > .colorswatch
                    {
                        flex-grow: 1;
                        border-radius: 0;
                    }

                    > .old
                    {
                        cursor: pointer;
                    }
                }

                > .tabs
                {
                    flex-shrink: 0;
                    background-color: #0c0d0f;
                    border: 0px;
                    border-radius: 5px;
                    padding: 2px;
                    gap: 2px;

                    > .button
                    {
                        padding: 2px 8px;
                        font-size: 11px;
                        border-radius: 4px;
                        background-color: transparent;
                        opacity: 0.6;

                        &.active
                        {
                            opacity: 1;
                            background-color: #ffffff18;
                        }
                    }
                }

                > .layout-button
                {
                    flex-shrink: 0;
                    width: 26px;
                    height: 26px;
                    padding: 0;
                    justify-content: center;
                }
            }

            > .body
            {
                flex-direction: row;
                align-items: stretch;
                gap: 8px;
            }

            > .slider-rows
            {
                flex-direction: column;
                gap: 8px;
            }

            .slider-row
            {
                flex-direction: row;
                align-items: center;
                gap: 8px;

                > .label
                {
                    width: 44px;
                    flex-shrink: 0;
                    font-size: 11px;
                }

                > .entry
                {
                    width: 62px;
                    flex-shrink: 0;
                    flex-grow: 0;

                    > numberentry
                    {
                        flex-grow: 1;
                        min-width: 0px;
                    }
                }

                > .slidercontrol
                {
                    flex-grow: 1;
                    flex-shrink: 1;
                    flex-basis: 0px;
                    min-width: 0px;
                }
            }

            > .section
            {
                flex-direction: column;
                gap: 5px;

                > .title
                {
                    font-size: 10px;
                    letter-spacing: 1px;
                    text-transform: uppercase;
                    opacity: 0.7;
                }

                > .swatches
                {
                    flex-direction: row;
                    flex-wrap: wrap;
                    gap: 4px;
                    min-height: 18px;

                    > .colorswatch
                    {
                        width: 20px;
                        height: 18px;
                        cursor: pointer;
                        border: 1px solid #0006;
                    }

                    > .add
                    {
                        width: 20px;
                        height: 18px;
                        border: 1px dashed #ffffff30;
                        border-radius: 3px;
                        font-size: 14px;
                        align-items: center;
                        justify-content: center;
                        cursor: pointer;
                        opacity: 0.6;

                        &:hover
                        {
                            opacity: 1;
                        }
                    }
                }
            }

            > .text-row > .colortextentry
            {
                flex-grow: 1;
            }
        }
        """;

    private static PickerLayout _savedLayout = PickerLayout.ColorSquare;

    private readonly ColorSwatch _oldSwatch;
    private readonly ColorSwatch _newSwatch;
    private readonly Menu _layoutMenu;
    private readonly List<(Menu Option, PickerLayout Layout)> _layoutOptions = [];
    private readonly Panel _body;
    private readonly ColorStrip _hueSide;
    private readonly ColorStrip _valueSide;
    private readonly ColorStrip _alphaSide;
    private readonly ColorSquare _square;
    private readonly HueRing _ring;
    private readonly HueDisc _disc;
    private readonly Panel _hsvRows;
    private readonly Panel _rgbRows;
    private readonly Panel _alphaRow;
    private readonly ColorStrip[] _hueStrips;
    private readonly ColorStrip[] _saturationStrips;
    private readonly ColorStrip[] _valueStrips;
    private readonly ColorStrip[] _alphaStrips;
    private readonly ColorStrip _redStrip;
    private readonly ColorStrip _greenStrip;
    private readonly ColorStrip _blueStrip;
    private readonly NumberEntry _hueRowEntry;
    private readonly NumberEntry _saturationRowEntry;
    private readonly NumberEntry _valueRowEntry;
    private readonly NumberEntry _alphaRowEntry;
    private readonly NumberEntry _redRowEntry;
    private readonly NumberEntry _greenRowEntry;
    private readonly NumberEntry _blueRowEntry;
    private readonly Panel _brightnessRow;
    private readonly SliderControl _brightness;
    private readonly NumberEntry _brightnessEntry;
    private readonly bool _built;
    private bool _hasAlpha = true;
    private bool _isHdr = true;
    private PickerColor _color = PickerColor.FromColor(Color.White);
    private PickerColor _original = PickerColor.FromColor(Color.White);
    private Color _applied = Color.White;
    private PickerLayout _layout = _savedLayout;

    /// <summary>Makes a picker showing white.</summary>
    public ColorPickerControl()
    {
        AddClass("colorpickercontrol");

        var header = Add.Panel("header");
        var compare = header.Add.Panel("compare");
        _oldSwatch = compare.AddChild(new ColorSwatch());
        _oldSwatch.AddClass("old");
        _oldSwatch.AddEventListener("onclick", () => PickColor(_original));
        _newSwatch = compare.AddChild(new ColorSwatch());
        BuildTabs(header);

        _layoutMenu = new Menu();
        var layoutButton = header.AddChild(new Button("", "more_vert"));
        layoutButton.AddClass("layout-button");
        layoutButton.AddEventListener("onclick", () => _layoutMenu.Open(layoutButton, Popup.PositionMode.BelowRight, 4));
        AddLayoutOption("Color Square", PickerLayout.ColorSquare);
        AddLayoutOption("Hue Ring", PickerLayout.HueRing);
        AddLayoutOption("Hue Disc", PickerLayout.HueDisc);
        AddLayoutOption("HSV Sliders", PickerLayout.HsvSliders);
        AddLayoutOption("RGB Sliders", PickerLayout.RgbSliders);
        _layoutMenu.AboutToShow += _ =>
        {
            foreach (var (option, layout) in _layoutOptions)
            {
                option.Checked = layout == _layout;
            }
        };

        _body = Add.Panel("body");
        _hueSide = _body.AddChild(new ColorStrip(true));
        _hueSide.ValueChanged = v => SetHue(v * 360);
        _square = _body.AddChild(new ColorSquare());
        _square.Changed = SetSaturationValue;
        _ring = _body.AddChild(new HueRing());
        _ring.HueChanged = SetHue;
        _ring.Square.Changed = SetSaturationValue;
        _disc = _body.AddChild(new HueDisc());
        _disc.Changed = SetHueSaturation;
        _valueSide = _body.AddChild(new ColorStrip(true));
        _valueSide.ValueChanged = SetValue;
        _alphaSide = _body.AddChild(new ColorStrip(true, transparent: true));
        _alphaSide.ValueChanged = SetAlpha;

        _hsvRows = Add.Panel("slider-rows");
        SliderRow(_hsvRows, "Hue", out var hueStrip, out _hueRowEntry, 360, "°", SetHue);
        hueStrip.ValueChanged = v => SetHue(v * 360);
        SliderRow(_hsvRows, "Sat", out var saturationStrip, out _saturationRowEntry, 100, "%", v => SetSaturationValue(v / 100, _color.Value));
        saturationStrip.ValueChanged = v => SetSaturationValue(v, _color.Value);
        SliderRow(_hsvRows, "Value", out var valueStrip, out _valueRowEntry, 100, "%", v => SetValue(v / 100));
        valueStrip.ValueChanged = SetValue;

        _rgbRows = Add.Panel("slider-rows");
        SliderRow(_rgbRows, "Red", out _redStrip, out _redRowEntry, 255, "", v => SetRgb(r: v / 255));
        _redStrip.ValueChanged = v => SetRgb(r: v);
        SliderRow(_rgbRows, "Green", out _greenStrip, out _greenRowEntry, 255, "", v => SetRgb(g: v / 255));
        _greenStrip.ValueChanged = v => SetRgb(g: v);
        SliderRow(_rgbRows, "Blue", out _blueStrip, out _blueRowEntry, 255, "", v => SetRgb(b: v / 255));
        _blueStrip.ValueChanged = v => SetRgb(b: v);

        _alphaRow = SliderRow(this, "Alpha", out var alphaStrip, out _alphaRowEntry, 100, "%", v => SetAlpha(v / 100), transparent: true);
        alphaStrip.ValueChanged = SetAlpha;

        _hueStrips = [_hueSide, hueStrip];
        _saturationStrips = [saturationStrip];
        _valueStrips = [_valueSide, valueStrip];
        _alphaStrips = [_alphaSide, alphaStrip];

        var hueStops = Enumerable.Range(0, 7).Select(i => Color.FromHsv(i * 60, 1, 1, 1)).ToArray();
        foreach (var strip in _hueStrips)
        {
            strip.SetGradient(hueStops);
        }

        foreach (var panel in new ColorDragPanel[] { _hueSide, _square, _ring, _ring.Square, _disc, _valueSide, _alphaSide, hueStrip, saturationStrip, valueStrip, alphaStrip, _redStrip, _greenStrip, _blueStrip })
        {
            panel.DragEnded = Commit;
        }

        _brightnessRow = Add.Panel("slider-row");
        _brightnessRow.Add.Label("Bright", "label");
        _brightness = _brightnessRow.AddChild(new SliderControl(0, MaxStops, 0.01f) { TickStep = 1, ShowValueTooltip = false });
        _brightness.OnValueChanged = stops =>
        {
            FindRootPanel()?.Focused?.Blur();
            SetBrightness(MathF.Pow(2, stops));
        };
        _brightness.AddEventListener("onmouseup", Commit);
        _brightnessEntry = _brightnessRow.Add.Panel("entry").AddChild(new NumberEntry());
        _brightnessEntry.Prefix = "×";
        _brightnessEntry.NumberFormat = "0.##";
        _brightnessEntry.MinValue = 1;
        _brightnessEntry.MaxValue = MathF.Pow(2, MaxStops);
        _brightnessEntry.OnTextEdited = text =>
        {
            if (TryParseNumber(text, out var v))
            {
                SetBrightness(v);
            }
        };
        _brightnessEntry.AddEventListener("onblur", OnTextBlurred);
        _brightnessEntry.ScrubEnded += Commit;

        BuildNumbers();
        BuildPalette();
        BuildTextRow();

        _built = true;
        ApplyLayout();
        Sync();
    }

    /// <summary>Called with the new color as the user changes it.</summary>
    [Parameter]
    public Action<Color>? ValueChanged { get; set; }

    /// <summary>The picked color. Setting it also makes it the color shown on the left of the header, which the user can click to go back to.</summary>
    [Parameter]
    public Color Value
    {
        get => _applied;
        set
        {
            if (value == _applied)
            {
                return;
            }

            _applied = value;
            _color = PickerColor.FromColor(value, _color.Hue);
            _original = _color;
            LimitApplied();
            Sync();
        }
    }

    /// <summary>Whether the user can pick how transparent the color is. On by default.</summary>
    [Parameter]
    public bool HasAlpha
    {
        get => _hasAlpha;
        set
        {
            _hasAlpha = value;
            LimitApplied();
            ApplyLayout();
            Sync();
        }
    }

    /// <summary>Whether the user can pick colors brighter than white, for lights and glows. On by default.</summary>
    [Parameter]
    public bool IsHdr
    {
        get => _isHdr;
        set
        {
            _isHdr = value;
            LimitApplied();
            ApplyLayout();
            Sync();
        }
    }

    internal PickerLayout Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            ApplyLayout();
        }
    }

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        _layoutMenu.Delete(true);
        _swatchMenu.Delete(true);
        base.OnDeleted();
    }

    private static bool TryParseNumber(string text, out float value) => Translation.TryParseTypedNumber(text, out value);

    private static void Show(Panel panel, bool show) => panel.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;

    private void AddLayoutOption(string text, PickerLayout layout)
    {
        var option = _layoutMenu.AddOption(text, _ =>
        {
            Layout = layout;
            _savedLayout = layout;
        });
        option.StaysOpen = false;
        _layoutOptions.Add((option, layout));
    }

    private Panel SliderRow(Panel parent, string label, out ColorStrip strip, out NumberEntry entry, float max, string suffix, Action<float> onNumber, bool transparent = false)
    {
        var row = parent.Add.Panel("slider-row");
        row.Add.Label(label, "label");
        strip = row.AddChild(new ColorStrip(transparent: transparent));
        entry = Channel(row.Add.Panel("entry"), "", max, onNumber);
        entry.Suffix = suffix;
        return row;
    }

    private void SetHue(float hue)
    {
        _color.Hue = MathF.Min(hue, 359.999f);
        Apply();
    }

    private void SetSaturationValue(float saturation, float value)
    {
        _color.Saturation = saturation;
        _color.Value = value;
        Apply();
    }

    private void SetHueSaturation(float hue, float saturation)
    {
        _color.Hue = hue;
        _color.Saturation = saturation;
        Apply();
    }

    private void SetValue(float value)
    {
        _color.Value = value;
        Apply();
    }

    private void SetAlpha(float alpha)
    {
        _color.Alpha = alpha;
        Apply();
    }

    private void SetBrightness(float brightness)
    {
        _color.Brightness = Math.Clamp(brightness, 1, MathF.Pow(2, MaxStops));
        Apply();
    }

    private void SetRgb(float? r = null, float? g = null, float? b = null)
    {
        var color = _color.BaseColor;
        color = new Color(r ?? color.R, g ?? color.G, b ?? color.B, color.A);
        var brightness = _color.Brightness;
        _color = PickerColor.FromColor(color, _color.Hue);
        _color.Brightness = brightness;
        Apply();
    }

    private void SetColor(PickerColor color)
    {
        _color = color;
        Apply();
    }

    private void PickColor(PickerColor color)
    {
        FindRootPanel()?.Focused?.Blur();
        SetColor(color);
        Commit();
    }

    private void Apply()
    {
        _color = _color.Limit(HasAlpha, IsHdr);
        _applied = _color.ToColor();
        ValueChanged?.Invoke(_applied);
        Sync();
    }

    private void LimitApplied()
    {
        var limited = _color.Limit(HasAlpha, IsHdr);
        if (!limited.Equals(_color))
        {
            _color = limited;
            _applied = _color.ToColor();
        }
    }

    private void Sync()
    {
        if (!_built)
        {
            return;
        }

        var color = _color;
        var pureHue = Color.FromHsv(color.Hue, 1, 1, 1);
        var opaque = color.BaseColor.WithAlpha(1);

        _oldSwatch.Set(_original, HasAlpha, IsHdr);
        _newSwatch.Set(color, HasAlpha, IsHdr);
        _square.Set(color);
        _ring.Set(color);
        _disc.Set(color);

        foreach (var strip in _hueStrips)
        {
            strip.Value = color.Hue / 360;
            strip.SetHandleColor(pureHue);
        }

        foreach (var strip in _saturationStrips)
        {
            strip.SetGradient(Color.FromHsv(color.Hue, 0, color.Value, 1), Color.FromHsv(color.Hue, 1, color.Value, 1));
            strip.Value = color.Saturation;
            strip.SetHandleColor(opaque);
        }

        foreach (var strip in _valueStrips)
        {
            strip.SetGradient(Color.Black, Color.FromHsv(color.Hue, color.Saturation, 1, 1));
            strip.Value = color.Value;
            strip.SetHandleColor(opaque);
        }

        foreach (var strip in _alphaStrips)
        {
            strip.SetGradient(opaque.WithAlpha(0), opaque);
            strip.Value = color.Alpha;
            strip.SetHandleColor(color.BaseColor);
        }

        SyncChannelStrip(_redStrip, opaque with { R = 0 }, opaque with { R = 1 }, opaque.R, opaque);
        SyncChannelStrip(_greenStrip, opaque with { G = 0 }, opaque with { G = 1 }, opaque.G, opaque);
        SyncChannelStrip(_blueStrip, opaque with { B = 0 }, opaque with { B = 1 }, opaque.B, opaque);

        _brightness.Value = MathF.Log2(color.Brightness);
        _brightnessEntry.Value = color.Brightness.ToString("0.##", CultureInfo.InvariantCulture);

        _hueRowEntry.Value = Whole(color.Hue);
        _saturationRowEntry.Value = Whole(color.Saturation * 100);
        _valueRowEntry.Value = Whole(color.Value * 100);
        _alphaRowEntry.Value = Whole(color.Alpha * 100);
        _redRowEntry.Value = Whole(opaque.R * 255);
        _greenRowEntry.Value = Whole(opaque.G * 255);
        _blueRowEntry.Value = Whole(opaque.B * 255);

        SyncNumbers();
        SyncText();
    }

    private static void SyncChannelStrip(ColorStrip strip, Color from, Color to, float value, Color handle)
    {
        strip.SetGradient(from, to);
        strip.Value = value;
        strip.SetHandleColor(handle);
    }

    private void ApplyLayout()
    {
        if (!_built)
        {
            return;
        }

        var square = _layout == PickerLayout.ColorSquare;
        var sliders = _layout is PickerLayout.HsvSliders or PickerLayout.RgbSliders;

        Show(_body, !sliders);
        Show(_hueSide, square);
        Show(_square, square);
        Show(_ring, _layout == PickerLayout.HueRing);
        Show(_disc, _layout == PickerLayout.HueDisc);
        Show(_valueSide, _layout == PickerLayout.HueDisc);
        Show(_alphaSide, HasAlpha);
        Show(_hsvRows, _layout == PickerLayout.HsvSliders);
        Show(_rgbRows, _layout == PickerLayout.RgbSliders);
        Show(_alphaRow, sliders && HasAlpha);
        Show(_brightnessRow, IsHdr);

        ShowAlphaNumbers(HasAlpha);
        SyncPaletteUsage();
    }

    internal enum PickerLayout
    {
        ColorSquare,
        HueRing,
        HueDisc,
        HsvSliders,
        RgbSliders,
    }
}
