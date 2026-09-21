using System.Text;
using SkiaSharp;
using Socotra.Layout;
using Topten.RichTextKit;

namespace Socotra;

internal sealed partial class TextBlock
{
    private float? _inlineWidth;
    private float? _inlineLayoutWidth;
    private InlineContentLayout? _inlineLayout;

    public bool IsInlineParagraph { get; private set; }

    public float InlineFinalWidth { get; private set; }

    public Style InlineStyle => _style.Copy();

    public int? InlineStyleHash => _fontHash;

    public int InlineCaretCount => _block.CaretIndicies.Count - 1;

    public void FinalizeInlineWidth(float width)
    {
        if (InlineFinalWidth != width)
        {
            Invalidate();
        }

        InlineFinalWidth = width;
    }

    public void InvalidateInlineSelection() => Invalidate();

    public string GetInlineSelectedText(int start, int end)
    {
        var from = _block.CodePointToCharacterIndex(CaretToCodePointIndex(start));
        var to = _block.CodePointToCharacterIndex(CaretToCodePointIndex(end));
        return Text[from..to];
    }

    public void SetInlineRuns(Styles style, IReadOnlyList<InlineFormattingContext.Run> runs)
    {
        _inlineLayout = null;
        IsInlineParagraph = true;
        SetText(string.Concat(runs.Select(r => r.Text)));
        Dirty();
        UpdateStyles(style);
        _block.Clear();
        _block.Alignment = TextAlignment.Left;
        _block.Overflow = Topten.RichTextKit.TextOverflow.None;
        _textOverflow = TextOverflow.None;
        _block.NoWrap = style.WhiteSpace == WhiteSpace.NoWrap;
        for (int i = 0; i < runs.Count;)
        {
            var first = runs[i++];
            var text = new StringBuilder(first.Text);
            while (i < runs.Count && InlineStyleKey(runs[i].Style).Equals(InlineStyleKey(first.Style)))
            {
                text.Append(runs[i++].Text);
            }

            _block.AddText(text.ToString(), first.Style);
        }
    }

    public LayoutSize MeasureInline(float width)
    {
        _inlineWidth = float.IsFinite(width) ? MathF.Max(0, width) : null;
        _block.MaxWidth = _inlineWidth;
        _block.MaxHeight = null;
        return new LayoutSize(_block.MeasuredWidth, _block.MeasuredHeight);
    }

    public InlineContentLayout LayoutInline(float width, IReadOnlyList<InlineFormattingContext.Run> runs)
    {
        var size = MeasureInline(width);
        if (_inlineLayout is not null && _inlineLayoutWidth == _inlineWidth)
        {
            return _inlineLayout;
        }

        var fragments = new List<InlineFragment>();
        foreach (var line in _block.Lines)
        {
            foreach (var fontRun in line.Runs)
            {
                if (fontRun.RunKind == FontRunKind.TrailingWhitespace)
                {
                    continue;
                }

                foreach (var run in runs)
                {
                    var start = Math.Max(fontRun.Start, run.Start);
                    var end = Math.Min(fontRun.End, run.Start + run.Sources.Count);
                    if (end <= start)
                    {
                        continue;
                    }

                    var a = fontRun.GetXCoordOfCodePointIndex(start);
                    var b = fontRun.GetXCoordOfCodePointIndex(end);
                    var sourceStart = run.Sources[start - run.Start].Start;
                    var sourceEnd = run.Sources[end - run.Start - 1].End;
                    fragments.Add(new InlineFragment(run.Owner.LayoutTree.Node, sourceStart, sourceEnd - sourceStart,
                        MathF.Min(a, b), line.YCoord, MathF.Abs(b - a), line.Height));
                }
            }
        }

        _inlineLayoutWidth = _inlineWidth;
        return _inlineLayout = new InlineContentLayout(size, _block.Lines.Count == 0 ? 0 : _block.Lines[0].BaseLine, fragments);
    }

    private static (string, float, int, bool, FontVariantNumeric, SKColorF, UnderlineStyle, SKColorF?, StrikeThroughStyle, float, float, float, bool, float?, UnderlineType, float, float, float)
        InlineStyleKey(Style style) =>
        (style.FontFamily, style.FontSize, style.FontWeight, style.FontItalic, style.FontVariantNumeric,
        style.TextColor, style.Underline, style.UnderlineColor, style.StrikeThrough, style.LineHeight,
        style.LetterSpacing, style.WordSpacing, style.StrokeInkSkip, style.StrokeThickness, style.UnderlineStrokeType,
        style.UnderlineOffset, style.OverlineOffset, style.StrikeThroughOffset);
}
