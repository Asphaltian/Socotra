using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Socotra.Html;
using Topten.RichTextKit;

namespace Socotra;

internal sealed partial class TextBlock
{
    private const float CaretWidth = 2;

    private readonly Topten.RichTextKit.TextBlock _block = new() { FontMapper = FontManager.Instance };
    private readonly Style _style = new();
    private readonly Dictionary<int, Vector2> _sizeCache = [];
    private readonly List<TextInstance> _instances = [];
    private Node? _htmlNode;
    private Vector2? _minContentSize;
    private int? _fontHash;
    private float _fontSize;
    private TextOverflow _textOverflow;
    private TextTransform? _textTransform;
    private Length? _lineHeight;
    private WhiteSpace? _whiteSpace;
    private FontSmooth _smooth;
    private bool _hasGradient;
    private GradientInfo _gradient;
    private bool _endsWithNewline;
    private int _lastSizeHash;
    private bool _needsLayout = true;
    private Margin _effectMargin;
    private Margin _textureMargin;
    private Vector2 _textureSize;
    private Texture? _texture;
    private bool _textureDirty = true;

    public string Text { get; private set; } = "";

    public bool ShouldDrawSelection { get; set; }

    public int SelectionStart { get; set; }

    public int SelectionEnd { get; set; }

    internal static readonly Color DefaultSelectionColor = new(0, 1, 1, 0.39f);

    public Color SelectionColor { get; set; } = DefaultSelectionColor;

    public bool IsTruncated { get; private set; }

    public bool NoWrap { get; set; }

    public bool IsHtml { get; private set; }

    public Func<INode, Styles?>? LookupStyles { get; set; }

    public Vector2 BlockSize { get; private set; }

    public Vector2 MeasuredSize => new(_block.MeasuredWidth, _block.MeasuredHeight);

    public List<HtmlSpan>? HtmlSpans { get; private set; }

    public int LineCount => _block.Lines.Count + (_endsWithNewline ? 1 : 0);

    public void SetText(string text)
    {
        Text = text;
        IsHtml = false;
    }

    public void SetHtml(string text)
    {
        if (IsHtml && Text == text)
        {
            return;
        }

        Text = text;
        IsHtml = true;
        try
        {
            _htmlNode = Node.Parse(Text);
        }
        catch (Exception e)
        {
            _htmlNode = null;
            Log.Warning(e.Message);
        }
    }

    public void Dirty() => _fontHash = null;

    public Vector2 MeasureMinContent(float? width = null)
    {
        if (width is null && _minContentSize is { } cached)
        {
            return cached;
        }

        var size = _block.MeasureMinContent(width, _whiteSpace == WhiteSpace.BreakSpaces, _endsWithNewline);
        var result = new Vector2(MathF.Ceiling(size.Width), MathF.Ceiling(size.Height));
        if (width is null)
        {
            _minContentSize = result;
        }

        return result;
    }

    public Vector2 Measure(float width, float height)
    {
        if (!float.IsNaN(width))
        {
            width = MathF.Ceiling(width);
        }

        if (!float.IsNaN(height))
        {
            height = MathF.Ceiling(height);
        }

        var hash = HashCode.Combine((int)width, _textOverflow != TextOverflow.None ? (int)height : 0);
        if (_sizeCache.TryGetValue(hash, out var size))
        {
            return size;
        }

        _block.MaxWidth = float.IsNaN(width) ? null : width + 1;
        if (_textOverflow != TextOverflow.None)
        {
            _block.MaxHeight = float.IsNaN(height) ? null : height + 1;
        }

        var measuredHeight = _block.MeasuredHeight;
        if (_endsWithNewline && _block.Lines.Count > 0)
        {
            measuredHeight += _block.Lines[^1].Height;
        }

        size = new Vector2(MathF.Ceiling(_block.MeasuredWidth), MathF.Ceiling(measuredHeight));
        _sizeCache[hash] = size;
        return size;
    }

    public int LineOf(int caretPosition)
    {
        var codepoint = CaretToCodePointIndex(caretPosition);
        if (_endsWithNewline && codepoint > 0 && codepoint == _block.Length)
        {
            return _block.Lines.Count;
        }

        return _block.GetCaretInfo(new CaretPosition { CodePointIndex = codepoint }).LineIndex;
    }

    public int GetLetterAtLine(int line, float x)
    {
        if (line >= _block.Lines.Count)
        {
            return _block.LookupCaretIndex(_block.Length);
        }

        return _block.LookupCaretIndex(_block.HitTestLine(line, x).ClosestCodePointIndex);
    }

    public void Draw(Painter painter, BlendMode blendMode, Styles? currentStyle, Rect textRect, float opacity)
    {
        if (BlockSize == Vector2.Zero || Text.Length == 0 || opacity <= 0)
        {
            return;
        }

        var options = GetOptions();
        options.Opacity = opacity;

        _instances.Clear();
        var origin = GetAlignedRect(currentStyle, textRect).Floor().Position - new Vector2(_block.MeasuredPadding.Left, 0);
        GpuFontText.Build(_block, origin, options, _instances);
        painter.Glyphs(_instances, blendMode, _hasGradient ? _gradient : default);
    }

    internal bool GetMask(Styles? currentStyle, Rect textRect, [NotNullWhen(true)] out Texture? texture, out Rect rect)
    {
        texture = null;
        rect = default;
        if (BlockSize == Vector2.Zero || Text.Length == 0)
        {
            return false;
        }

        if (_textureDirty)
        {
            _texture = RebuildTexture();
            _textureDirty = false;
        }

        if (_texture is null)
        {
            return false;
        }

        texture = _texture;
        rect = GetTextureRect(currentStyle, textRect);
        return true;
    }

    private GpuFontText.Options GetOptions()
    {
        var options = GpuFontText.Options.Default;
        options.Aliased = _smooth == FontSmooth.Never;
        options.SelectionColor = SelectionColor;
        options.HasGradient = _hasGradient;
        if (ShouldDrawSelection && (SelectionStart > 0 || SelectionEnd > 0))
        {
            options.SelectionStart = CaretToCodePointIndex(SelectionStart);
            options.SelectionEnd = CaretToCodePointIndex(SelectionEnd);
        }

        return options;
    }

    private Rect GetTextureRect(Styles? currentStyle, Rect textRect)
    {
        var position = GetAlignedRect(currentStyle, textRect).Position - new Vector2(_textureMargin.Left, _textureMargin.Top);
        return new Rect(position, _textureSize).Floor();
    }

    private Texture? RebuildTexture()
    {
        int width = (int)_textureSize.X, height = (int)_textureSize.Y;
        if (width < 2 || height < 2)
        {
            return null;
        }

        var options = GetOptions();
        options.HasGradient = false;

        var origin = new Vector2(_textureMargin.Left - _block.MeasuredPadding.Left, _textureMargin.Top);
        return GpuFontText.Render(_block, origin, width, height, options, _texture);
    }

    public Rect CaretRect(int caretPosition)
    {
        var codepoint = CaretToCodePointIndex(caretPosition);
        var info = _block.GetCaretInfo(new CaretPosition { AltPosition = false, CodePointIndex = codepoint });
        var x = info.CaretRectangle.Left;
        var y = info.CaretRectangle.Top;
        if (codepoint > 0 && codepoint == _block.Length && _endsWithNewline)
        {
            x = 0;
            y += _block.Lines[info.LineIndex].Height;
        }

        return new Rect(x, y, info.CaretRectangle.Width, info.CaretRectangle.Height);
    }

    public int GetLetterAt(Vector2 pos) => _block.LookupCaretIndex(_block.HitTest(pos.X, pos.Y).ClosestCodePointIndex);

    public int GetCharacterAt(Vector2 pos)
    {
        var index = _block.HitTest(pos.X, pos.Y).OverCodePointIndex;
        return index < 0 ? -1 : _block.LookupCaretIndex(index);
    }

    public HtmlSpan? GetSpanAt(Vector2 pos)
    {
        if (HtmlSpans is null)
        {
            return null;
        }

        var index = _block.HitTest(pos.X, pos.Y).OverCodePointIndex;
        return HtmlSpans.FirstOrDefault(span => span.From <= index && span.To > index);
    }

    public bool UpdateStyles(Styles style)
    {
        var fontFamily = style.FontFamily ?? "Arial";
        var fontColor = style.FontColor ?? Color.Black;
        _fontSize = MathF.Round((style.FontSize ?? Length.Pixels(Length.InitialFontSize)).GetPixels(100) * 32) / 32;
        _textOverflow = style.TextOverflow!.Value;
        _textTransform = style.TextTransform;
        _lineHeight = style.LineHeight;
        _whiteSpace = style.WhiteSpace;
        _smooth = style.FontSmooth!.Value;
        var textAlign = style.TextAlign!.Value;
        var decoration = style.TextDecorationLine!.Value;
        var fontStyle = style.FontStyle!.Value;

        var hash = HashCode.Combine(_fontSize, fontColor, fontFamily, style.FontWeight, textAlign, _whiteSpace, decoration, fontStyle);
        hash = HashCode.Combine(hash, style.LetterSpacing, _textTransform, Text, style.TextShadow, style.TextStrokeWidth, style.TextStrokeColor, style.TextDecorationColor);
        hash = HashCode.Combine(hash, style.TextDecorationThickness, style.TextDecorationSkipInk, style.TextDecorationStyle, style.TextUnderlineOffset, style.TextOverlineOffset, style.TextLineThroughOffset, style.TextGradient);
        hash = HashCode.Combine(hash, _textOverflow, style.WordBreak, _lineHeight, style.WordSpacing, _smooth, style.FontVariantNumeric, NoWrap);
        hash = HashCode.Combine(hash, IsHtml, FontManager.Instance.Generation);
        if (_fontHash == hash)
        {
            return false;
        }

        _fontHash = hash;
        _style.FontFamily = fontFamily;
        _style.FontSize = _fontSize;
        _style.FontWeight = style.FontWeight ?? 400;
        _style.FontItalic = fontStyle != FontStyle.None;
        _style.FontVariantNumeric = style.FontVariantNumeric ?? FontVariantNumeric.Normal;
        _style.TextColor = fontColor.ToSkF();
        _style.StrokeInkSkip = style.TextDecorationSkipInk == TextSkipInk.All;
        _style.UnderlineOffset = style.TextUnderlineOffset!.Value.GetPixels(100);
        _style.OverlineOffset = style.TextOverlineOffset!.Value.GetPixels(100);
        _style.StrikeThroughOffset = style.TextLineThroughOffset!.Value.GetPixels(100);
        _style.UnderlineStrokeType = style.TextDecorationStyle switch
        {
            TextDecorationStyle.Double => UnderlineType.Double,
            TextDecorationStyle.Dotted => UnderlineType.Dotted,
            TextDecorationStyle.Dashed => UnderlineType.Dashed,
            TextDecorationStyle.Wavy => UnderlineType.Wavy,
            _ => UnderlineType.Solid,
        };
        _style.UnderlineColor = (style.TextDecorationColor ?? fontColor).ToSkF();
        _style.StrokeThickness = style.TextDecorationThickness?.GetPixels(100);
        _style.Underline = (decoration.HasFlag(TextDecoration.Underline) ? UnderlineStyle.Gapped : UnderlineStyle.None)
            | (decoration.HasFlag(TextDecoration.Overline) ? UnderlineStyle.Overline : UnderlineStyle.None);
        _style.StrikeThrough = decoration.HasFlag(TextDecoration.LineThrough) ? StrikeThroughStyle.Solid : StrikeThroughStyle.None;
        _style.LetterSpacing = style.LetterSpacing!.Value.GetPixels(1000);
        _style.WordSpacing = style.WordSpacing!.Value.GetPixels(1000);
        _style.LineHeight = GetLineHeightMultiplier();
        _style.ClearEffects();

        _hasGradient = !style.TextGradient.IsEmpty && style.TextGradient.Type != GradientType.Conic;
        _gradient = _hasGradient ? ToGradient(style.TextGradient) : default;

        var effectMargin = default(Margin);
        foreach (var shadow in style.TextShadow ?? [])
        {
            var effect = TextEffect.DropShadow(shadow.Color.ToSkF(), shadow.OffsetX, shadow.OffsetY, shadow.Blur);
            effect.BlurSize = MathF.Max(effect.BlurSize, 0.01f);
            _style.AddEffect(effect);

            var shadowSize = shadow.Blur * 2;
            effectMargin = new Margin(
                MathF.Ceiling(MathF.Max(effectMargin.Left, shadowSize - shadow.OffsetX)),
                MathF.Ceiling(MathF.Max(effectMargin.Top, shadowSize - shadow.OffsetY)),
                MathF.Ceiling(MathF.Max(effectMargin.Right, shadowSize + shadow.OffsetX)),
                MathF.Ceiling(MathF.Max(effectMargin.Bottom, shadowSize + shadow.OffsetY)));
        }

        if (style.TextStrokeWidth!.Value.Value > 0)
        {
            var color = style.TextStrokeColor ?? fontColor;
            var size = style.TextStrokeWidth.Value.GetPixels(1);
            _style.AddEffect(TextEffect.Outline(color.ToSkF(), size));

            effectMargin = new Margin(
                MathF.Ceiling(MathF.Max(effectMargin.Left, size)),
                MathF.Ceiling(MathF.Max(effectMargin.Top, size)),
                MathF.Ceiling(MathF.Max(effectMargin.Right, size)),
                MathF.Ceiling(MathF.Max(effectMargin.Bottom, size)));
        }

        _effectMargin = effectMargin;

        _block.Clear();
        _endsWithNewline = false;
        _block.Alignment = (TextAlignment)textAlign;
        _block.Overflow = (Topten.RichTextKit.TextOverflow)_textOverflow;
        _block.WordBreak = (WordBreakMode)style.WordBreak!.Value;
        _block.NoWrap = NoWrap || _whiteSpace is WhiteSpace.NoWrap or WhiteSpace.Pre;

        if (IsHtml && !string.IsNullOrWhiteSpace(Text))
        {
            BuildFromHtml();
        }
        else if (!IsInlineParagraph)
        {
            var text = FixedText(Text);
            AddStyledText(text);
            _endsWithNewline = EndsWithLineBreak(text);
        }

        _sizeCache.Clear();
        _minContentSize = null;
        Invalidate();
        return true;
    }

    public void SizeFinalized(float width, float height)
    {
        width = MathF.Ceiling(width);
        height = MathF.Ceiling(height);
        var sizeHash = new Vector2(width, height).GetHashCode();
        if (_lastSizeHash != sizeHash)
        {
            Invalidate();
            _lastSizeHash = sizeHash;
            if (Text.Length == 0)
            {
                BlockSize = new Vector2(ClampSize(_block.MeasuredWidth), ClampSize(_block.MeasuredHeight));
            }
        }

        if (Text.Length == 0 || !_needsLayout)
        {
            return;
        }

        Relayout(width, height);
        _needsLayout = false;
    }

    public void ScrollToCaret(int caretPosition, ref Vector2 scroll, Vector2 visibleBounds)
    {
        if (visibleBounds.X <= 0 || visibleBounds.Y <= 0)
        {
            return;
        }

        var caret = CaretRect(caretPosition);
        if (caret.Left < scroll.X)
        {
            scroll.X = caret.Left;
        }
        else if (caret.Left + CaretWidth > scroll.X + visibleBounds.X)
        {
            scroll.X = caret.Left + CaretWidth - visibleBounds.X;
        }

        if (caret.Top < scroll.Y)
        {
            scroll.Y = caret.Top;
        }
        else if (caret.Bottom > scroll.Y + visibleBounds.Y)
        {
            scroll.Y = caret.Bottom - visibleBounds.Y;
        }

        ClampScroll(ref scroll, visibleBounds);
    }

    public void ClampScroll(ref Vector2 scroll, Vector2 visibleBounds)
    {
        if (visibleBounds.X <= 0 || visibleBounds.Y <= 0)
        {
            return;
        }

        scroll.X = Math.Clamp(scroll.X, 0, MathF.Max(0, _block.MeasuredWidth + CaretWidth - visibleBounds.X));
        scroll.Y = Math.Clamp(scroll.Y, 0, MathF.Max(0, _block.MeasuredHeight - visibleBounds.Y));
    }

    private static bool EndsWithLineBreak(string text) => text is { Length: > 0 } && text[^1] is '\n' or '\u2029';

    internal static int ClampSize(float size) => Math.Clamp((int)MathF.Ceiling(size), 2, 4096);

    private static GradientInfo ToGradient(in TextGradientInfo text)
    {
        var stops = new GradientStops();
        foreach (var stop in text.Stops.AsSpan()[..Math.Min(text.Stops.Length, GradientInfo.MaxStops)])
        {
            stops = stops.Add(stop);
        }

        return new GradientInfo
        {
            Angle = text.Angle,
            CenterX = text.CenterX,
            CenterY = text.CenterY,
            Size = text.Size,
            Type = text.Type,
            Stops = stops,
        };
    }

    private static string CollapseWhiteSpace(string text) => WhiteSpaceRegex().Replace(text, " ").Trim();

    private static string CollapseSpacesAndPreserveLines(string text) =>
        SpaceAroundLineBreakRegex().Replace(SpacesAndTabsRegex().Replace(text, " "), "").Trim();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhiteSpaceRegex();

    [GeneratedRegex("[ \t]+")]
    private static partial Regex SpacesAndTabsRegex();

    [GeneratedRegex("(?<=\\n|\\u2029)[ \\t]+|[ \\t]+(?=\\n|\\u2029)")]
    private static partial Regex SpaceAroundLineBreakRegex();

    internal Rect GetAlignedRect(Styles? currentStyle, Rect textRect)
    {
        if (currentStyle?.TextAlign == TextAlign.Center)
        {
            textRect.Left += (textRect.Width - BlockSize.X) * 0.5f;
        }
        else if (currentStyle?.TextAlign == TextAlign.Right)
        {
            textRect.Left = textRect.Right - BlockSize.X;
        }

        if (currentStyle?.AlignItems == Align.Center)
        {
            textRect.Top += (textRect.Height - BlockSize.Y) * 0.5f;
        }
        else if (currentStyle?.AlignItems == Align.FlexEnd)
        {
            textRect.Top = textRect.Bottom - BlockSize.Y;
        }

        return textRect;
    }

    private void BuildFromHtml()
    {
        HtmlSpans = [];
        try
        {
            if (_htmlNode is not null)
            {
                BuildBlockFromHtml(_htmlNode);
            }

            if (LookupStyles is null)
            {
                return;
            }

            foreach (var span in HtmlSpans)
            {
                if (LookupStyles(span.Node) is { } styles)
                {
                    _block.ApplyStyle(span.From, span.To - span.From, ResolveSpanStyle(styles, 1));
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning(e.Message);
        }
    }

    private void BuildBlockFromHtml(Node node)
    {
        if (node.IsComment)
        {
            return;
        }

        if (node.IsText)
        {
            var start = _block.Length;
            _block.AddText(node.InnerHtml, _style);
            _endsWithNewline = EndsWithLineBreak(node.InnerHtml);
            HtmlSpans!.Add(new HtmlSpan(node.ParentNode, start, _block.Length));
        }

        if (node.Name == "br")
        {
            _block.AddText("\n", _style);
            _endsWithNewline = true;
            return;
        }

        foreach (var child in node.ChildNodes)
        {
            BuildBlockFromHtml(child);
        }
    }

    private void Invalidate()
    {
        _needsLayout = true;
        _textureDirty = true;
    }

    private void Relayout(float maxWidth, float maxHeight)
    {
        if (_textOverflow != TextOverflow.None)
        {
            _block.MaxWidth = maxWidth;
            _block.MaxHeight = maxHeight;
        }
        else
        {
            _block.MaxWidth = IsInlineParagraph ? _inlineWidth : _whiteSpace is WhiteSpace.NoWrap or WhiteSpace.Pre ? null : MathF.Ceiling(maxWidth) + 1;
        }

        int width = ClampSize(_block.MeasuredWidth);
        int height = ClampSize(_block.MeasuredHeight);
        if (_style.LetterSpacing < 0)
        {
            width += Math.Abs((int)MathF.Floor(_style.LetterSpacing));
        }

        BlockSize = new Vector2(width, height);
        IsTruncated = _block.Truncated;

        var overhang = _block.MeasuredOverhang;
        _textureMargin = _effectMargin + new Margin(MathF.Ceiling(overhang.Left), MathF.Ceiling(overhang.Top), MathF.Ceiling(overhang.Right), MathF.Ceiling(overhang.Bottom));

        var marginEdge = _textureMargin.EdgeSize;
        _textureSize = new Vector2(width + MathF.Ceiling(marginEdge.X), height + MathF.Ceiling(marginEdge.Y));
    }

    private int CaretToCodePointIndex(int caretPosition)
    {
        var carets = _block.CaretIndicies;
        return caretPosition < 0 || caretPosition > carets.Count - 1 ? caretPosition : carets[caretPosition];
    }

    private string FixedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ".";
        }

        text = text.Replace("\r\n", "\u2029").Replace('\n', '\u2029');
        text = _textTransform switch
        {
            TextTransform.Uppercase => text.ToUpperInvariant(),
            TextTransform.Lowercase => text.ToLowerInvariant(),
            TextTransform.Capitalize => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text),
            _ => text,
        };

        return _whiteSpace switch
        {
            WhiteSpace.Normal or WhiteSpace.NoWrap => CollapseWhiteSpace(text),
            WhiteSpace.PreLine => CollapseSpacesAndPreserveLines(text),
            _ => text,
        };
    }

    private float GetLineHeightMultiplier() => _lineHeight switch
    {
        { Unit: LengthUnit.Percentage } height => height.Value / 100,
        { Unit: LengthUnit.Pixels } height => height.Value / MathF.Max(_fontSize, 1),
        _ => 1,
    };

    internal sealed record HtmlSpan(INode Node, int From, int To);
}
