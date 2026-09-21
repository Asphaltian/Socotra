namespace Socotra;

[StyleSheet.Inline("colorstrip", Styles)]
internal class ColorStrip : ColorDragPanel
{
    private const string Styles = ColorPickerStyles.Handle + """
        .colorstrip
        {
            position: relative;
            height: 14px;
            flex-grow: 1;
            flex-shrink: 0;
            border-radius: 7px;
            cursor: pointer;
            pointer-events: all;

            > .fill
            {
                position: absolute;
                left: 0;
                top: 0;
                right: 0;
                bottom: 0;
                border-radius: 7px;
            }

            &.vertical
            {
                width: 14px;
                height: auto;
                flex-grow: 0;
                align-self: stretch;
            }
        }
        """;

    private readonly Panel _fill;
    private readonly Panel _handle;
    private float _value;

    public ColorStrip(bool vertical = false, bool transparent = false)
    {
        Vertical = vertical;
        AddClass("colorstrip");
        SetClass("vertical", vertical);
        if (transparent)
        {
            Style.BackgroundImage = ColorPickerTextures.Checkerboard;
            Style.BackgroundSizeX = ColorPickerTextures.CheckerSize;
            Style.BackgroundSizeY = ColorPickerTextures.CheckerSize;
            Style.BackgroundRepeat = BackgroundRepeat.Repeat;
        }

        _fill = Add.Panel("fill");
        _handle = Add.Panel("color-handle");
        Value = 0;
    }

    public bool Vertical { get; }

    public Action<float>? ValueChanged { get; set; }

    public float Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 1);
            _handle.Style.Left = Length.Percent(Vertical ? 50 : _value * 100);
            _handle.Style.Top = Length.Percent(Vertical ? (1 - _value) * 100 : 50);
        }
    }

    public void SetGradient(params Color[] stops) =>
        _fill.Style.Set("background-image", $"linear-gradient( {(Vertical ? "to top" : "to right")}, {string.Join(", ", stops.Select(stop => stop.ToCss()))} )");

    public void SetHandleColor(Color color) => _handle.Style.BackgroundColor = color;

    protected override void OnDrag(Vector2 fraction)
    {
        Value = Vertical ? 1 - fraction.Y : fraction.X;
        ValueChanged?.Invoke(Value);
    }
}
