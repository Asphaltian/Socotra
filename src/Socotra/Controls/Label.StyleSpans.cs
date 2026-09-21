namespace Socotra;

public partial class Label
{
    private List<TextBlock.StyleSpan>? _styleSpans;

    /// <summary>
    /// Styles part of the text, from string index <paramref name="start"/> up to <paramref name="end"/> in the text as
    /// shown, after <c>white-space</c> and <c>text-transform</c>. These are string indexes, so a character like an emoji
    /// can take two, unlike <see cref="CaretPosition"/>. Font, color, background, spacing and text
    /// decoration settings are used. Add spans in order without overlapping them. They're cleared when <see cref="Text"/>
    /// changes, and don't apply to rich text or text inside an inline paragraph.
    /// </summary>
    /// <example>
    /// <code>
    /// // Show the first word in red
    /// label.Text = "Warning: low health";
    /// label.SetStyleSpan(0, 8, new Styles { FontColor = new Color(1, 0, 0) });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is negative or after <paramref name="end"/>.</exception>
    /// <exception cref="ArgumentException">The span starts before the end of the last one.</exception>
    public void SetStyleSpan(int start, int end, Styles style)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (start < 0 || end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (start == end)
        {
            return;
        }

        if (_styleSpans is { Count: > 0 } && start < _styleSpans[^1].End)
        {
            throw new ArgumentException("Style spans must be ordered and must not overlap.", nameof(start));
        }

        _styleSpans ??= [];
        _styleSpans.Add(new TextBlock.StyleSpan(start, end, style));
        TextBlock?.Dirty();
        SetNeedsPreLayout();
    }

    /// <summary>Removes every span added with <see cref="SetStyleSpan"/>. The text and selection stay as they are.</summary>
    public void ClearStyleSpans()
    {
        if (_styleSpans is not { Count: > 0 })
        {
            return;
        }

        _styleSpans.Clear();
        TextBlock?.Dirty();
        SetNeedsPreLayout();
    }
}
