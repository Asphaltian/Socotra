namespace Socotra;

[StyleSheet.Inline("huering", Styles)]
internal class HueRing : ColorDragPanel
{
    private const string Styles = ColorPickerStyles.Handle + """
        .huering
        {
            position: relative;
            width: 220px;
            height: 220px;
            flex-shrink: 0;
            cursor: pointer;
            pointer-events: all;

            > .ring
            {
                position: absolute;
                left: 0;
                top: 0;
                right: 0;
                bottom: 0;
                pointer-events: none;
            }

            > .colorsquare
            {
                position: absolute;
                left: 47px;
                top: 47px;
                width: 126px;
                height: 126px;
                flex-grow: 0;
            }
        }
        """;

    private readonly Panel _handle;

    public HueRing()
    {
        AddClass("huering");
        var ring = Add.Panel("ring");
        ring.Style.BackgroundImage = ColorPickerTextures.HueRing;
        Square = AddChild<ColorSquare>();
        _handle = Add.Panel("color-handle");
    }

    public Action<float>? HueChanged { get; set; }

    public ColorSquare Square { get; }

    public void Set(PickerColor color)
    {
        var radians = float.DegreesToRadians(color.Hue);
        var radius = (ColorPickerTextures.RingOuter + ColorPickerTextures.RingInner) * 0.5f;
        var center = ColorPickerTextures.WheelSize * 0.5f;
        _handle.Style.Left = Length.Pixels(center + (MathF.Cos(radians) * radius));
        _handle.Style.Top = Length.Pixels(center + (MathF.Sin(radians) * radius));
        _handle.Style.BackgroundColor = Color.FromHsv(color.Hue, 1, 1, 1);
        Square.Set(color);
    }

    protected override void OnDrag(Vector2 fraction)
    {
        var hue = (float.RadiansToDegrees(MathF.Atan2(fraction.Y - 0.5f, fraction.X - 0.5f)) + 360) % 360;
        HueChanged?.Invoke(MathF.Min(hue, 359.999f));
    }
}
