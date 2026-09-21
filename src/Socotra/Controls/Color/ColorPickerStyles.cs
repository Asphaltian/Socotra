namespace Socotra;

internal static class ColorPickerStyles
{
    public const string Handle = """
        .color-handle
        {
            position: absolute;
            width: 14px;
            height: 14px;
            border-radius: 50%;
            border: 2px solid #fff;
            box-shadow: 0 0 0 1px #0008;
            transform: translateX( -50% ) translateY( -50% );
            pointer-events: none;
            z-index: 2;
        }
        """;
}
