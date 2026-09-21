namespace Socotra;

[StyleSheet.Inline("colorsquare", Styles)]
internal class ColorSquare : ColorDragPanel
{
    private const string Styles = ColorPickerStyles.Handle + """
        .colorsquare
        {
            position: relative;
            flex-grow: 1;
            height: 220px;
            cursor: crosshair;
            pointer-events: all;

            > .gradient
            {
                position: absolute;
                left: 0;
                top: 0;
                right: 0;
                bottom: 0;
                background: linear-gradient( to right, #fff, #fff0 );

                &:after
                {
                    content: "";
                    position: absolute;
                    left: 0;
                    top: 0;
                    right: 0;
                    bottom: 0;
                    background: linear-gradient( to top, #000, #0000 );
                }
            }
        }
        """;

    private readonly Panel _handle;

    public ColorSquare()
    {
        AddClass("colorsquare");
        Add.Panel("gradient");
        _handle = Add.Panel("color-handle");
    }

    public Action<float, float>? Changed { get; set; }

    public void Set(PickerColor color)
    {
        Style.BackgroundColor = Color.FromHsv(color.Hue, 1, 1, 1);
        _handle.Style.Left = Length.Percent(color.Saturation * 100);
        _handle.Style.Top = Length.Percent((1 - color.Value) * 100);
        _handle.Style.BackgroundColor = color.BaseColor.WithAlpha(1);
    }

    protected override void OnDrag(Vector2 fraction) => Changed?.Invoke(fraction.X, 1 - fraction.Y);
}
