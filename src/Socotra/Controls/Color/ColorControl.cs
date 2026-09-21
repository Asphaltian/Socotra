using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A compact color field: a swatch showing the color and a box the user can type it into, written any way a stylesheet
/// would, like <c>#ff8000</c>, <c>rgb( 255, 128, 0 )</c> or <c>orange</c>. Add <c>* 4</c> after it for a color four
/// times brighter than normal. Text that isn't a color turns red and changes nothing. Clicking the swatch opens a
/// <see cref="ColorPickerControl"/> under it.
/// </summary>
/// <example>
/// <code>
/// // A tint field without transparency
/// var tint = new ColorControl { Parent = row, HasAlpha = false, Value = sprite.Tint };
/// tint.ValueChanged = color => sprite.Tint = color;
/// </code>
/// </example>
[StyleSheet.Inline("colorcontrol", Styles)]
public class ColorControl : Panel
{
    private const string Styles = """
        .colorcontrol
        {
            flex-direction: row;
            align-items: center;
            gap: 6px;
            flex-grow: 1;
            height: 28px;
            padding: 2px;
            border-radius: 4px;
            background-color: #0000004d;
            pointer-events: all;

            > .colorswatch
            {
                height: 100%;
                width: 34px;
                cursor: pointer;
            }

            > .colortextentry
            {
                flex-grow: 1;
                min-width: 0px;
                background-color: transparent;
            }
        }
        """;

    private readonly ColorSwatch _swatch;
    private readonly ColorTextEntry _text;
    private bool _hasAlpha = true;
    private bool _isHdr = true;
    private PickerColor _color = PickerColor.FromColor(Color.White);
    private Color _applied = Color.White;

    /// <summary>Makes a field showing white.</summary>
    public ColorControl()
    {
        AddClass("colorcontrol");
        _swatch = AddChild(new ColorSwatch());
        _swatch.AddEventListener("onmousedown", OpenPopup);
        _text = AddChild(new ColorTextEntry());
        _text.ColorEntered = text =>
        {
            if (PickerColor.TryParse(text, _color.Hue, out var color))
            {
                _color = color;
                Apply();
            }
        };
        _text.Blurred = Sync;
        Sync();
    }

    /// <summary>Called with the new color when the user types or picks one.</summary>
    [Parameter]
    public Action<Color>? ValueChanged { get; set; }

    /// <summary>The color shown.</summary>
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
            LimitApplied();
            Sync();
        }
    }

    /// <summary>Whether the color can be see-through. When it's off, typed colors lose their transparency. On by default.</summary>
    [Parameter]
    public bool HasAlpha
    {
        get => _hasAlpha;
        set
        {
            _hasAlpha = value;
            LimitApplied();
            Sync();
        }
    }

    /// <summary>Whether the color can be brighter than white. When it's off, typed colors lose their <c>* N</c> brightness. On by default.</summary>
    [Parameter]
    public bool IsHdr
    {
        get => _isHdr;
        set
        {
            _isHdr = value;
            LimitApplied();
            Sync();
        }
    }

    private void Apply()
    {
        _color = _color.Limit(_hasAlpha, _isHdr);
        _applied = _color.ToColor();
        ValueChanged?.Invoke(_applied);
        Sync();
    }

    private void LimitApplied()
    {
        var limited = _color.Limit(_hasAlpha, _isHdr);
        if (!limited.Equals(_color))
        {
            _color = limited;
            _applied = _color.ToColor();
        }
    }

    private void OpenPopup()
    {
        _text.Blur();
        var popup = new Popup(_swatch, Popup.PositionMode.BelowLeft, 4);
        var picker = popup.AddChild(new ColorPickerControl { HasAlpha = _hasAlpha, IsHdr = _isHdr, Value = _applied });
        picker.ValueChanged = color =>
        {
            _applied = color;
            _color = PickerColor.FromColor(color, _color.Hue);
            ValueChanged?.Invoke(color);
            Sync();
        };
    }

    private void Sync()
    {
        _swatch.Set(_color, _hasAlpha, _isHdr);
        _text.Value = _color.ToText();
    }
}
