namespace Socotra;

/// <summary>
/// How <see cref="Painter.Text"/> draws text: font, size, color, alignment, spacing, shadow and outline. Set it on
/// <see cref="Painter.TextStyle"/>. Sizes are in pixels and grow with the panel's <see cref="Panel.ScaleToScreen"/>.
/// </summary>
/// <example>
/// <code>
/// // 24px white Poppins with a soft shadow, centered in the box
/// painter.TextStyle = new TextStyle("Poppins", 24, Color.White).WithShadow().WithAlignment(TextFlag.Center);
/// painter.Text("Hello", new Rect(0, 0, 200, 40));
/// </code>
/// </example>
public readonly partial record struct TextStyle
{
    /// <summary>White, 16px Roboto at normal weight, in the top left corner.</summary>
    public TextStyle()
    {
    }

    /// <summary>White text in <paramref name="fontName"/> at <paramref name="fontSize"/> pixels.</summary>
    public TextStyle(string fontName, float fontSize)
        : this()
    {
        FontName = fontName;
        FontSize = fontSize;
    }

    /// <summary>Text in <paramref name="fontName"/> at <paramref name="fontSize"/> pixels, in <paramref name="color"/>.</summary>
    public TextStyle(string fontName, float fontSize, Color color)
        : this(fontName, fontSize)
    {
        Color = color;
    }

    /// <summary>White, 16px Roboto at normal weight, in the top left corner.</summary>
    public static TextStyle Default => new();

    /// <summary>The font family, like <c>"Roboto"</c>. If it isn't loaded, a system font is used instead.</summary>
    public string FontName { get; init; } = "Roboto";

    /// <summary>The font size in pixels.</summary>
    public float FontSize { get; init; } = 16;

    /// <summary>How bold the text is, from 1 to 1000. Normal is 400, bold is 700.</summary>
    public int FontWeight { get; init; } = 400;

    /// <summary>Draws the text in italics.</summary>
    public bool Italic { get; init; }

    /// <summary>The text color. Its alpha fades the text together with its shadow and outline.</summary>
    public Color Color { get; init; } = Color.White;

    /// <summary>Where the text sits in the rectangle you draw it in, like <see cref="TextFlag.Center"/>.</summary>
    public TextFlag Alignment { get; init; } = TextFlag.LeftTop;

    /// <summary>The space between lines, as a multiple of the font's normal line height.</summary>
    public float LineHeight { get; init; } = 1;

    /// <summary>Extra space between letters, in pixels.</summary>
    public float LetterSpacing { get; init; }

    /// <summary>Extra space between words, in pixels.</summary>
    public float WordSpacing { get; init; }

    /// <summary>A drop shadow behind the text. Off unless you set it; <see cref="WithShadow(Color?, float, Vector2?)"/> is the easy way.</summary>
    public TextRendering.Shadow Shadow { get; init; }

    /// <summary>An outline around the text. Off unless you set it; <see cref="WithOutline"/> is the easy way.</summary>
    public TextRendering.Outline Outline { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(FontName))
        {
            throw new ArgumentException("A font name is required.", nameof(FontName));
        }

        if (!float.IsFinite(FontSize) || FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FontSize));
        }

        if (FontWeight < 1 || FontWeight > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(FontWeight));
        }

        if (!float.IsFinite(LineHeight) || LineHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LineHeight));
        }

        if (!float.IsFinite(LetterSpacing))
        {
            throw new ArgumentOutOfRangeException(nameof(LetterSpacing));
        }

        if (!float.IsFinite(WordSpacing))
        {
            throw new ArgumentOutOfRangeException(nameof(WordSpacing));
        }

        ValidateColor(Color, nameof(Color));

        if (Shadow.Enabled)
        {
            if (!float.IsFinite(Shadow.Size) || Shadow.Size < 0 || !Painter.IsFinite(Shadow.Offset))
            {
                throw new ArgumentOutOfRangeException(nameof(Shadow));
            }

            ValidateColor(Shadow.Color, nameof(Shadow));
        }

        if (Outline.Enabled)
        {
            if (!float.IsFinite(Outline.Size) || Outline.Size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Outline));
            }

            ValidateColor(Outline.Color, nameof(Outline));
        }
    }

    internal TextRendering.Scope CreateScope(string text, float scale)
    {
        var scope = new TextRendering.Scope(text, Color.WithAlpha(1), FontSize * scale, FontName, FontWeight)
        {
            FontItalic = Italic,
            LineHeight = LineHeight,
            LetterSpacing = LetterSpacing * scale,
            WordSpacing = WordSpacing * scale,
        };

        if (Shadow.Enabled)
        {
            scope.Shadow = Shadow with { Size = Shadow.Size * scale, Offset = Shadow.Offset * scale };
        }

        if (Outline.Enabled)
        {
            scope.Outline = Outline with { Size = Outline.Size * scale };
        }

        return scope;
    }

    private static void ValidateColor(Color color, string parameter)
    {
        if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B) || !float.IsFinite(color.A))
        {
            throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
