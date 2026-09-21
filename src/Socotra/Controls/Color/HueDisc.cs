namespace Socotra;

[StyleSheet.Inline("huedisc", Styles)]
internal class HueDisc : ColorDragPanel
{
    private const string Styles = ColorPickerStyles.Handle + """
        .huedisc
        {
            position: relative;
            width: 220px;
            height: 220px;
            flex-shrink: 0;
            cursor: crosshair;
            pointer-events: all;
        }
        """;

    private readonly Panel _handle;

    public HueDisc()
    {
        AddClass("huedisc");
        Style.BackgroundImage = ColorPickerTextures.HueDisc;
        _handle = Add.Panel("color-handle");
    }

    public Action<float, float>? Changed { get; set; }

    public void Set(PickerColor color)
    {
        var radians = float.DegreesToRadians(color.Hue);
        var radius = color.Saturation * ColorPickerTextures.DiscRadius;
        var center = ColorPickerTextures.WheelSize * 0.5f;
        _handle.Style.Left = Length.Pixels(center + (MathF.Cos(radians) * radius));
        _handle.Style.Top = Length.Pixels(center + (MathF.Sin(radians) * radius));
        _handle.Style.BackgroundColor = Color.FromHsv(color.Hue, color.Saturation, 1, 1);
    }

    protected override void OnDrag(Vector2 fraction)
    {
        var dx = (fraction.X - 0.5f) * ColorPickerTextures.WheelSize;
        var dy = (fraction.Y - 0.5f) * ColorPickerTextures.WheelSize;
        var hue = (float.RadiansToDegrees(MathF.Atan2(dy, dx)) + 360) % 360;
        var saturation = MathF.Min(1, MathF.Sqrt((dx * dx) + (dy * dy)) / ColorPickerTextures.DiscRadius);
        Changed?.Invoke(MathF.Min(hue, 359.999f), saturation);
    }
}
