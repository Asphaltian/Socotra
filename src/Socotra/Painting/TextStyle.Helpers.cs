namespace Socotra;

public readonly partial record struct TextStyle
{
    /// <summary>A copy in another font, and optionally at another size and weight.</summary>
    public TextStyle WithFont(string fontName, float? size = null, int? weight = null) =>
        this with { FontName = fontName, FontSize = size ?? FontSize, FontWeight = weight ?? FontWeight };

    /// <summary>A copy at <paramref name="size"/> pixels.</summary>
    public TextStyle WithSize(float size) => this with { FontSize = size };

    /// <summary>A copy with another weight, from 1 to 1000. Normal is 400, bold is 700.</summary>
    public TextStyle WithWeight(int weight) => this with { FontWeight = weight };

    /// <summary>A copy in bold, or back at normal weight with <c>false</c>.</summary>
    public TextStyle WithBold(bool bold = true) => WithWeight(bold ? 700 : 400);

    /// <summary>A copy in italics, or without them with <c>false</c>.</summary>
    public TextStyle WithItalic(bool italic = true) => this with { Italic = italic };

    /// <summary>A copy in another color. Its alpha fades the text together with its shadow and outline.</summary>
    public TextStyle WithColor(Color color) => this with { Color = color };

    /// <summary>A copy that sits somewhere else in its rectangle, like <see cref="TextFlag.Center"/>.</summary>
    public TextStyle WithAlignment(TextFlag alignment) => this with { Alignment = alignment };

    /// <summary>A copy with other line spacing, as a multiple of the font's normal line height.</summary>
    public TextStyle WithLineHeight(float height) => this with { LineHeight = height };

    /// <summary>A copy with <paramref name="spacing"/> extra pixels between letters.</summary>
    public TextStyle WithLetterSpacing(float spacing) => this with { LetterSpacing = spacing };

    /// <summary>A copy with <paramref name="spacing"/> extra pixels between words.</summary>
    public TextStyle WithWordSpacing(float spacing) => this with { WordSpacing = spacing };

    /// <summary>
    /// A copy with a drop shadow. Leave the arguments out for a soft, half transparent black shadow 2 pixels down and to the
    /// right; a <paramref name="blur"/> of 0 gives a hard one.
    /// </summary>
    public TextStyle WithShadow(Color? color = null, float blur = 4, Vector2? offset = null) => this with
    {
        Shadow = new TextRendering.Shadow { Enabled = true, Color = color ?? Color.Black.WithAlpha(0.5f), Size = blur, Offset = offset ?? new Vector2(2) },
    };

    /// <summary>A copy with a <paramref name="color"/> drop shadow moved <paramref name="offsetX"/> right and <paramref name="offsetY"/> down.</summary>
    public TextStyle WithShadow(Color color, float offsetX, float offsetY, float blur = 4) => WithShadow(color, blur, new Vector2(offsetX, offsetY));

    /// <summary>A copy without a drop shadow.</summary>
    public TextStyle WithoutShadow() => this with { Shadow = default };

    /// <summary>A copy with an outline, black and 1 pixel wide unless you say otherwise.</summary>
    public TextStyle WithOutline(Color? color = null, float width = 1) => this with
    {
        Outline = new TextRendering.Outline { Enabled = true, Color = color ?? Color.Black, Size = width },
    };

    /// <summary>A copy without an outline.</summary>
    public TextStyle WithoutOutline() => this with { Outline = default };
}
