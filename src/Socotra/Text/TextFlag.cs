namespace Socotra;

/// <summary>
/// Where text sits inside the rectangle you draw it in, set through <see cref="TextStyle.Alignment"/>.
/// Combine one horizontal and one vertical value; leave an axis out and it sticks to the top or left.
/// </summary>
[Flags]
public enum TextFlag
{
    /// <summary>Top left.</summary>
    None = 0,

    /// <summary>Align to the left.</summary>
    Left = 0x0001,

    /// <summary>Align to the right.</summary>
    Right = 0x0002,

    /// <summary>Center horizontally.</summary>
    CenterHorizontally = 0x0004,

    /// <summary>Anchor to the top.</summary>
    Top = 0x0020,

    /// <summary>Anchor to the bottom.</summary>
    Bottom = 0x0040,

    /// <summary>Center vertically.</summary>
    CenterVertically = 0x0080,

    /// <summary>Anchor to the top left corner.</summary>
    LeftTop = Left | Top,

    /// <summary>Anchor to the left side, centered vertically.</summary>
    LeftCenter = Left | CenterVertically,

    /// <summary>Anchor to the bottom left corner.</summary>
    LeftBottom = Left | Bottom,

    /// <summary>Anchor to the top side, centered horizontally.</summary>
    CenterTop = CenterHorizontally | Top,

    /// <summary>Center on both axes.</summary>
    Center = CenterHorizontally | CenterVertically,

    /// <summary>Anchor to the bottom side, centered horizontally.</summary>
    CenterBottom = CenterHorizontally | Bottom,

    /// <summary>Anchor to the top right corner.</summary>
    RightTop = Right | Top,

    /// <summary>Anchor to the right side, centered vertically.</summary>
    RightCenter = Right | CenterVertically,

    /// <summary>Anchor to the bottom right corner.</summary>
    RightBottom = Right | Bottom,

    /// <summary>Only shows the first line of the text.</summary>
    SingleLine = 0x0100,

    /// <summary>Lets the text run past the rectangle instead of wrapping or getting cut off.</summary>
    DontClip = 0x0200,
}
