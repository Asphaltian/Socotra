using Topten.RichTextKit;

namespace Socotra;

internal sealed partial class TextBlock
{
    private Dictionary<Styles, Style>? _spanStyles;

    public List<StyleSpan>? StyleSpans { get; set; }

    public float StyleSpanScale { get; set; } = 1;

    private void AddStyledText(string text)
    {
        if (StyleSpans is not { Count: > 0 })
        {
            _block.AddText(text, _style);
            return;
        }

        _spanStyles ??= new(ReferenceEqualityComparer.Instance);
        _spanStyles.Clear();
        int position = 0;
        foreach (var span in StyleSpans)
        {
            int start = Math.Min(span.Start, text.Length);
            int end = Math.Min(span.End, text.Length);
            if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
            {
                start--;
            }

            if (end > 0 && end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1]))
            {
                end--;
            }

            _block.AddText(text.AsSpan(position, start - position), _style);
            if (end > start)
            {
                if (!_spanStyles.TryGetValue(span.Style, out var resolved))
                {
                    resolved = ResolveSpanStyle(span.Style, StyleSpanScale);
                    _spanStyles.Add(span.Style, resolved);
                }

                _block.AddText(text.AsSpan(start, end - start), resolved);
            }

            position = end;
        }

        _block.AddText(text.AsSpan(position), _style);
        _spanStyles.Clear();
    }

    private Style ResolveSpanStyle(Styles source, float scale)
    {
        var result = _style.Copy();
        if (source.FontSize is { } size)
        {
            result.FontSize = MathF.Round(size.GetPixels(100) * scale * 32) / 32;
        }

        result.FontFamily = source.FontFamily ?? result.FontFamily;
        result.FontWeight = source.FontWeight ?? result.FontWeight;
        if (source.FontStyle is { } italic)
        {
            result.FontItalic = italic == FontStyle.Italic;
        }

        result.FontVariantNumeric = source.FontVariantNumeric ?? result.FontVariantNumeric;
        result.TextColor = source.FontColor?.ToSkF() ?? result.TextColor;
        result.BackgroundColor = source.BackgroundColor?.ToSkF() ?? result.BackgroundColor;
        if (source.LetterSpacing is { } letters)
        {
            result.LetterSpacing = letters.GetPixels(1000) * scale;
        }

        if (source.WordSpacing is { } words)
        {
            result.WordSpacing = words.GetPixels(1000) * scale;
        }

        if (source.TextDecorationLine is { } decoration)
        {
            result.Underline = decoration.HasFlag(TextDecoration.Underline) ? UnderlineStyle.Solid : UnderlineStyle.None;
            result.StrikeThrough = decoration.HasFlag(TextDecoration.LineThrough) ? StrikeThroughStyle.Solid : StrikeThroughStyle.None;
        }

        result.UnderlineColor = source.TextDecorationColor?.ToSkF() ?? result.TextColor;
        return result;
    }

    internal readonly record struct StyleSpan(int Start, int End, Styles Style);
}
