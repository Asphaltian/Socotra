using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A number the user picks by dragging a thumb along a track, or by typing it into the optional entry beside it.
/// Pressing anywhere on the track jumps the thumb there. While it's dragged, a tooltip above the thumb shows the value.
/// When the user changes the value, it sends <c>onchange</c>.
/// </summary>
/// <example>
/// <code>
/// // A volume slider from 0 to 100 in steps of 5, with a box to type the number into
/// var volume = new SliderControl(0, 100, 5) { Parent = options, ShowTextEntry = true, Value = 80 };
/// volume.OnValueChanged = value => audio.Volume = value / 100;
/// </code>
/// </example>
[StyleSheet.Inline("slidercontrol", Styles)]
public class SliderControl : Panel
{
    private const string Styles = """
        .slidercontrol
        {
            flex-direction: row;
            min-width: 50px;
            position: relative;
            flex-shrink: 0;
            cursor: pointer;
            gap: 8px;
            flex-grow: 1;
            align-items: center;
            pointer-events: all;

            > .inner
            {
                flex-direction: column;
                flex-shrink: 1;
                flex-grow: 1;
                min-height: 24px;
                justify-content: center;

                > .values
                {
                    width: 100%;
                    pointer-events: none;
                    font-size: 12px;
                    opacity: 0.6;
                    margin-bottom: 2px;

                    > .left
                    {
                        flex-grow: 1;
                    }
                }

                > .track
                {
                    position: relative;
                    background-color: #3a3e47;
                    height: 6px;
                    margin: 9px 8px;
                    align-items: center;
                    border-radius: 3px;

                    > .track-active
                    {
                        background-color: #3273EB;
                        position: absolute;
                        height: 100%;
                        left: 0px;
                        border-radius: 3px;
                    }

                    > .tick
                    {
                        position: absolute;
                        top: 0px;
                        bottom: 0px;
                        width: 2px;
                        background-color: #ffffff38;
                        transform: translateX( -50% );
                    }

                    > .thumb
                    {
                        position: relative;
                        background-color: #fff;
                        border-radius: 100px;
                        width: 14px;
                        height: 14px;
                        transform: translateX( -50% );
                        box-shadow: 0 1px 4px #000a;
                        transition: box-shadow 0.1s ease-out;
                        z-index: 1;
                    }
                }
            }

            &:hover > .inner > .track > .thumb
            {
                box-shadow: 0 1px 4px #000a, 0 0 0 4px #3273EB40;
            }

            &:active > .inner > .track > .thumb
            {
                box-shadow: 0 1px 4px #000a, 0 0 0 6px #3273EB60;
            }

            > .entry
            {
                flex-shrink: 0;
                flex-grow: 0;
                width: 50px;

                > numberentry
                {
                    background-color: transparent;

                    > .content-label
                    {
                        padding: 0 4px;
                    }
                }
            }
        }

        .slidercontrol .value-tooltip
        {
            position: absolute;
            bottom: 150%;
            left: -8px;
            z-index: 1000;
            flex-direction: column;

            > .label
            {
                background-color: #0c0d0f;
                padding: 4px 8px;
                border-radius: 4px;
                font-size: 12px;
            }

            > .tail
            {
                bottom: 0px;
                background-color: #0c0d0f;
                width: 10px;
                height: 10px;
                transform: rotateZ( 45deg ) translateX( 4px );
                position: absolute;
            }
        }
        """;

    private readonly List<Panel> _ticks = [];
    private readonly Panel _entryPanel;
    private readonly Panel _valuesPanel;
    private readonly Panel _trackActivePanel;
    private readonly Label _minLabel;
    private readonly Label _maxLabel;
    private float _min;
    private float _max = 100;
    private float _tickStep;
    private SliderFill _fill = SliderFill.Left;
    private bool _showRange;
    private bool _showTextEntry;
    private string _numberFormat = "0.###";
    private float _value;
    private Panel? _tooltipPanel;
    private Label? _tooltipLabel;

    /// <summary>Makes a slider from 0 to 100.</summary>
    public SliderControl()
    {
        AddClass("slidercontrol");

        _entryPanel = Add.Panel("entry");
        _entryPanel.Style.Display = DisplayMode.None;

        TextEntryPanel = _entryPanel.AddChild(new NumberEntry { NumberFormat = _numberFormat });
        TextEntryPanel.OnTextEdited = OnTextEntryEdited;

        var inner = Add.Panel("inner");

        _valuesPanel = inner.Add.Panel("values");
        _valuesPanel.Style.Display = DisplayMode.None;
        _minLabel = _valuesPanel.Add.Label("", "left");
        _maxLabel = _valuesPanel.Add.Label("", "right");

        TrackPanel = inner.Add.Panel("track");
        _trackActivePanel = TrackPanel.Add.Panel("track-active");
        ThumbPanel = TrackPanel.Add.Panel("thumb");

        UpdateVisuals();
    }

    /// <summary>Makes a slider from <paramref name="min"/> to <paramref name="max"/> that moves in steps of <paramref name="step"/>.</summary>
    public SliderControl(float min, float max, float step = 1)
        : this()
    {
        Min = min;
        Max = max;
        Step = step;
    }

    /// <summary>Called with the new value as the user drags the slider or types a number.</summary>
    [Parameter]
    public Action<float>? OnValueChanged { get; set; }

    /// <summary>The value at the right end of the track.</summary>
    [Parameter]
    public float Max
    {
        get => _max;
        set
        {
            _max = value;
            UpdateVisuals();
            RebuildTicks();
        }
    }

    /// <summary>The value at the left end of the track.</summary>
    [Parameter]
    public float Min
    {
        get => _min;
        set
        {
            _min = value;
            UpdateVisuals();
            RebuildTicks();
        }
    }

    /// <summary>Which part of the track is drawn filled. From the left end up to the thumb by default.</summary>
    [Parameter]
    public SliderFill Fill
    {
        get => _fill;
        set
        {
            _fill = value;
            UpdateVisuals();
        }
    }

    /// <summary>Draws a mark on the track every this many units, counting up from <see cref="Min"/>. 0 draws none.</summary>
    [Parameter]
    public float TickStep
    {
        get => _tickStep;
        set
        {
            _tickStep = value;
            RebuildTicks();
        }
    }

    /// <summary>What dragged values are rounded to: 1 gives whole numbers, 10 gives tens and 0.1 gives tenths. 0 doesn't round.</summary>
    [Parameter]
    public float Step { get; set; } = 0.001f;

    /// <summary>Shows <see cref="Min"/> and <see cref="Max"/> above the track.</summary>
    [Parameter]
    public bool ShowRange
    {
        get => _showRange;
        set
        {
            _showRange = value;
            _valuesPanel.Style.Display = value ? DisplayMode.Flex : DisplayMode.None;
        }
    }

    /// <summary>Shows the value above the thumb while the slider is being dragged. On by default.</summary>
    [Parameter]
    public bool ShowValueTooltip { get; set; } = true;

    /// <summary>Shows a box beside the slider where the user can type the value.</summary>
    [Parameter]
    public bool ShowTextEntry
    {
        get => _showTextEntry;
        set
        {
            _showTextEntry = value;
            _entryPanel.Style.Display = value ? DisplayMode.Flex : DisplayMode.None;
        }
    }

    /// <summary>How numbers are shown, as a .NET format string like <c>0.00</c>.</summary>
    [Parameter]
    public string NumberFormat
    {
        get => _numberFormat;
        set
        {
            _numberFormat = value;
            TextEntryPanel.NumberFormat = value;
            UpdateVisuals();
        }
    }

    /// <summary>The slider's value. Setting it doesn't call <see cref="OnValueChanged"/>.</summary>
    [Parameter]
    public float Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            UpdateVisuals();
        }
    }

    private Panel TrackPanel { get; }

    private Panel ThumbPanel { get; }

    private TextEntry TextEntryPanel { get; }

    /// <summary>The value under a point on screen, kept between <see cref="Min"/> and <see cref="Max"/> and rounded to <see cref="Step"/>.</summary>
    public virtual float ScreenPosToValue(Vector2 pos)
    {
        var normalized = MathX.LerpInverse(pos.X, TrackPanel.Box.Rect.Left, TrackPanel.Box.Rect.Right);
        var scaled = MathX.Lerp(Min, Max, normalized);
        return Step > 0 ? MathX.SnapToGrid(scaled, Step) : scaled;
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        UpdateTooltip();
    }

    /// <summary>Moves the thumb to the mouse while the slider is held down.</summary>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        if (!HasActive || e.MouseButton == MouseButtons.Middle)
        {
            return;
        }

        SetValueFromMouse();
        e.StopPropagation();
    }

    /// <summary>Jumps the thumb to where the slider was pressed.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        SetValueFromMouse();
        e.StopPropagation();
        TextEntryPanel.Blur();
    }

    /// <summary>Keeps middle clicks from reaching the slider's parents.</summary>
    protected override void OnMiddleClick(MousePanelEvent e)
    {
        base.OnMiddleClick(e);
        e.StopPropagation();
    }

    private void RebuildTicks()
    {
        foreach (var tick in _ticks)
        {
            tick.Delete(true);
        }

        _ticks.Clear();
        if (_tickStep <= 0 || Max <= Min)
        {
            return;
        }

        var count = (int)MathF.Floor((Max - Min) / _tickStep);
        if (count > 200)
        {
            return;
        }

        for (int i = 0; i <= count; i++)
        {
            var tick = TrackPanel.Add.Panel("tick");
            tick.Style.Left = Length.Percent(MathX.LerpInverse(Min + (i * _tickStep), Min, Max) * 100);
            _ticks.Add(tick);
        }
    }

    private void UpdateVisuals()
    {
        var position = MathX.LerpInverse(_value, Min, Max) * 100;
        var (fillStart, fillWidth) = _fill switch
        {
            SliderFill.Right => (position, 100 - position),
            SliderFill.Center => (MathF.Min(50, position), MathF.Abs(position - 50)),
            SliderFill.None => (0.0f, 0.0f),
            _ => (0.0f, position),
        };

        _trackActivePanel.Style.Display = fillWidth > 0 ? DisplayMode.Flex : DisplayMode.None;
        _trackActivePanel.Style.Left = Length.Percent(fillStart);
        _trackActivePanel.Style.Width = Length.Percent(fillWidth);
        ThumbPanel.Style.Left = Length.Percent(position);

        _minLabel.Text = Min.ToString(NumberFormat, CultureInfo.InvariantCulture);
        _maxLabel.Text = Max.ToString(NumberFormat, CultureInfo.InvariantCulture);
        if (!TextEntryPanel.HasFocus)
        {
            TextEntryPanel.Text = _value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }
    }

    private void UpdateTooltip()
    {
        var show = HasActive && ShowValueTooltip;
        if (show && _tooltipPanel is null)
        {
            _tooltipPanel = ThumbPanel.Add.Panel("value-tooltip");
            _tooltipLabel = _tooltipPanel.Add.Label("");
            _tooltipPanel.Add.Panel("tail");
        }
        else if (!show && _tooltipPanel is not null)
        {
            _tooltipPanel.Delete(true);
            _tooltipPanel = null;
            _tooltipLabel = null;
        }

        if (_tooltipLabel is not null)
        {
            _tooltipLabel.Text = Value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }
    }

    private void OnTextEntryEdited(string text)
    {
        if (Translation.TryParseTypedNumber(text, out var value))
        {
            SetValueFromUser(value);
        }
    }

    private void SetValueFromMouse() => SetValueFromUser(ScreenPosToValue(ScreenMousePosition));

    private void SetValueFromUser(float value)
    {
        if (value == _value)
        {
            return;
        }

        Value = value;
        OnValueChanged?.Invoke(Value);
        CreateEvent("onchange", Value);
        CreateValueEvent("value", Value);
    }
}

/// <summary>Which part of a <see cref="SliderControl"/>'s track is drawn filled.</summary>
public enum SliderFill
{
    /// <summary>From the left end up to the thumb.</summary>
    Left,

    /// <summary>From the thumb to the right end.</summary>
    Right,

    /// <summary>From the middle of the track to the thumb, either way. Good for values that swing either side of zero.</summary>
    Center,

    /// <summary>No fill, just the thumb on the track.</summary>
    None,
}
