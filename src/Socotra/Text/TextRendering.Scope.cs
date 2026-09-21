using Topten.RichTextKit;

namespace Socotra;

public static partial class TextRendering
{
    /// <summary>
    /// An outline around text. Set <see cref="Enabled"/> to show it, or use <see cref="TextStyle.WithOutline"/>.
    /// </summary>
    public record struct Outline
    {
        /// <summary>Set to <see langword="true"/> to show the outline.</summary>
        public bool Enabled;

        /// <summary>How wide the outline is, in pixels.</summary>
        public float Size = 4;

        /// <summary>The outline's color.</summary>
        public Color Color = Color.White;

        /// <summary>A hidden, white, 4 pixel outline. Set <see cref="Enabled"/> to show it.</summary>
        public Outline()
        {
        }
    }

    /// <summary>
    /// A drop shadow behind text. Set <see cref="Enabled"/> to show it, or use <see cref="TextStyle.WithShadow(Color?, float, Vector2?)"/>.
    /// </summary>
    public record struct Shadow
    {
        /// <summary>Set to <see langword="true"/> to show the shadow.</summary>
        public bool Enabled;

        /// <summary>How blurry the shadow is, in pixels. Use 0 for a sharp shadow.</summary>
        public float Size = 4;

        /// <summary>The shadow's color.</summary>
        public Color Color = Color.White;

        /// <summary>How far the shadow sits from the text, in pixels. Positive values move it right and down.</summary>
        public Vector2 Offset = new(4);

        /// <summary>A hidden white shadow, blurred 4 pixels and moved 4 pixels right and down. Set <see cref="Enabled"/> to show it.</summary>
        public Shadow()
        {
        }
    }

    internal record struct Scope
    {
        public string Text;
        public Color TextColor;
        public string FontName;
        public float FontSize;
        public int FontWeight;
        public bool FontItalic;
        public float LineHeight;
        public float LetterSpacing;
        public float WordSpacing;
        public Shadow Shadow;
        public Outline Outline;

        public Scope(string text, Color color, float size, string font, int weight)
        {
            Text = text;
            TextColor = color;
            FontSize = size;
            FontName = font;
            FontWeight = weight;
            LineHeight = 1;
        }

        public readonly void ToStyle(Style style)
        {
            style.FontFamily = FontName;
            style.FontSize = FontSize;
            style.FontWeight = FontWeight;
            style.FontItalic = FontItalic;
            style.TextColor = TextColor.ToSkF();
            style.LetterSpacing = LetterSpacing;
            style.WordSpacing = WordSpacing;
            style.LineHeight = LineHeight;

            if (Shadow.Enabled)
            {
                style.AddEffect(TextEffect.DropShadow(Shadow.Color.ToSkF(), Shadow.Offset.X, Shadow.Offset.Y, Shadow.Size));
            }

            if (Outline.Enabled && Outline.Size > 0)
            {
                style.AddEffect(TextEffect.Outline(Outline.Color.ToSkF(), Outline.Size));
            }
        }
    }
}
