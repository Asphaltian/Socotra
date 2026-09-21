using System.Globalization;
using System.Text;

namespace Socotra;

public partial class Label
{
    private bool _multiline = true;
    private Vector2 _caretScroll;
    private float? _desiredCaretX;
    private bool _movingLine;
    private Vector2 _scrolledSize;
    private int _caretIntoView;

    /// <summary>Whether the text wraps and can have more than one line. On by default.</summary>
    public bool Multiline
    {
        get => _multiline;
        set
        {
            if (_multiline == value)
            {
                return;
            }

            _multiline = value;
            LayoutTree.MarkDirty();
            SetNeedsPreLayout();
        }
    }

    /// <summary>Replaces the selected text with <paramref name="str"/> and puts the caret after it.</summary>
    public void ReplaceSelection(string str)
    {
        var start = Math.Min(SelectionStart, SelectionEnd);
        var end = Math.Max(SelectionStart, SelectionEnd);
        InsertTextAndMoveCaret(str, start, end);
    }

    /// <summary>Selects from <paramref name="start"/> to <paramref name="end"/>, in characters. Pass <paramref name="end"/> before <paramref name="start"/> for a selection that runs backwards.</summary>
    public void SetSelection(int start, int end)
    {
        start = Math.Clamp(start, 0, TextLength);
        end = Math.Clamp(end, 0, TextLength);
        if (start == end)
        {
            start = 0;
            end = 0;
        }

        SelectionStart = start;
        SelectionEnd = end;
    }

    /// <summary>Moves the caret to <paramref name="pos"/>. Pass <paramref name="select"/> to select the characters it passes over, like holding Shift.</summary>
    public void SetCaretPosition(int pos, bool select = false)
    {
        if (SelectionEnd == 0 && SelectionStart == 0 && select)
        {
            SelectionStart = Math.Clamp(CaretPosition, 0, TextLength);
        }

        CaretPosition = Math.Clamp(pos, 0, TextLength);
        if (select)
        {
            SelectionEnd = CaretPosition;
        }
        else
        {
            SelectionEnd = 0;
            SelectionStart = 0;
        }

        ScrollToCaret();
    }

    /// <summary>Scrolls the text, and any scrolling parent, so the caret can be seen.</summary>
    public void ScrollToCaret()
    {
        if (TextBlock is null)
        {
            return;
        }

        TextBlock.ScrollToCaret(CaretPosition, ref _caretScroll, Box.RectInner.Size);
        _caretIntoView = 3;
        SetNeedsFinalLayout();
    }

    /// <summary>Moves the caret to the start of the word on its left, like Ctrl+Left.</summary>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveToWordBoundaryLeft(bool select)
    {
        var left = GetWordBoundaryIndices().LastOrDefault(x => x < CaretPosition);
        MoveCaretPos(left - CaretPosition, select);
    }

    /// <summary>Moves the caret to the end of the word on its right, like Ctrl+Right.</summary>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveToWordBoundaryRight(bool select)
    {
        var right = GetWordBoundaryIndices().FirstOrDefault(x => x > CaretPosition, TextLength);
        MoveCaretPos(right - CaretPosition, select);
    }

    /// <summary>Moves the caret <paramref name="delta"/> characters to the right, or to the left when negative.</summary>
    /// <param name="delta">How many characters to move.</param>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveCaretPos(int delta, bool select = false) => SetCaretPosition(CaretPosition + delta, select);

    /// <summary>Inserts <paramref name="text"/> at character <paramref name="pos"/>. Pass <paramref name="endpos"/> to replace the characters from <paramref name="pos"/> up to it.</summary>
    public void InsertText(string text, int pos, int? endpos = null)
    {
        CaretSantity();
        pos = Math.Clamp(pos, 0, TextLength);
        if (endpos.HasValue)
        {
            endpos = Math.Clamp(endpos.Value, 0, TextLength);
        }

        var before = pos > 0 ? StringInfo.SubstringByTextElements(0, pos) : "";
        var after = "";
        if (endpos.HasValue)
        {
            if (endpos < TextLength)
            {
                after = StringInfo.SubstringByTextElements(endpos.Value);
            }
        }
        else if (pos < TextLength)
        {
            after = StringInfo.SubstringByTextElements(pos);
        }

        Text = $"{before}{text}{after}";
    }

    /// <summary>Removes <paramref name="count"/> characters starting at character <paramref name="start"/>.</summary>
    public virtual void RemoveText(int start, int count)
    {
        var before = start > 0 ? StringInfo.SubstringByTextElements(0, start) : "";
        var after = start + count < TextLength ? StringInfo.SubstringByTextElements(start + count) : "";
        Text = before + after;
    }

    /// <summary>Moves the caret to the start of its line.</summary>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveToLineStart(bool select = false)
    {
        if (!Multiline)
        {
            SetCaretPosition(0, select);
            return;
        }

        int lineStart = 0;
        int index = 0;
        var e = StringInfo.GetTextElementEnumerator(Text);
        while (e.MoveNext())
        {
            if (index >= CaretPosition)
            {
                break;
            }

            if (IsNewline(e.GetTextElement()))
            {
                lineStart = index + 1;
            }

            index++;
        }

        SetCaretPosition(lineStart, select);
    }

    /// <summary>Moves the caret to the end of its line.</summary>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveToLineEnd(bool select = false)
    {
        if (!Multiline)
        {
            SetCaretPosition(TextLength, select);
            return;
        }

        int index = 0;
        var e = StringInfo.GetTextElementEnumerator(Text);
        while (e.MoveNext())
        {
            var position = index++;
            if (position >= CaretPosition && IsNewline(e.GetTextElement()))
            {
                SetCaretPosition(position, select);
                return;
            }
        }

        SetCaretPosition(TextLength, select);
    }

    /// <summary>Moves the caret up or down by lines, keeping as close as it can to where it was across the line.</summary>
    /// <param name="offsetLine">How many lines to move. Negative values move up.</param>
    /// <param name="select">Selects the characters passed over, like holding Shift.</param>
    public void MoveCaretLine(int offsetLine, bool select)
    {
        if (!Multiline)
        {
            if (offsetLine < 0)
            {
                SetCaretPosition(0, select);
            }

            if (offsetLine > 0)
            {
                SetCaretPosition(TextLength, select);
            }

            return;
        }

        if (TextBlock is null)
        {
            return;
        }

        var caret = GetCaretRect(CaretPosition);
        _desiredCaretX ??= caret.Left - _textRect.Left + _caretScroll.X;
        var line = TextBlock.LineOf(CaretPosition) + offsetLine;
        if (line < 0)
        {
            SetCaretPosition(0, select);
            return;
        }

        if (line >= TextBlock.LineCount)
        {
            SetCaretPosition(TextLength, select);
            return;
        }

        var pos = TextBlock.GetLetterAtLine(line, _desiredCaretX.Value);
        if (pos < 0)
        {
            return;
        }

        _movingLine = true;
        try
        {
            SetCaretPosition(pos, select);
        }
        finally
        {
            _movingLine = false;
        }
    }

    /// <summary>Selects the word at character <paramref name="wordPos"/>, like a double click.</summary>
    public void SelectWord(int wordPos)
    {
        if (TextLength == 0)
        {
            return;
        }

        var boundaries = GetWordBoundaryIndices();
        wordPos = Math.Clamp(wordPos, 0, TextLength - 1);
        SelectionStart = boundaries.LastOrDefault(x => x <= wordPos);
        SelectionEnd = boundaries.FirstOrDefault(x => x > wordPos, TextLength);
        CaretPosition = SelectionEnd;
    }

    /// <summary>The character positions where words, runs of spaces and runs of symbols start and end, in order. Ctrl+Left and Ctrl+Right stop at these.</summary>
    public List<int> GetWordBoundaryIndices()
    {
        var result = new List<int> { 0 };
        var e = StringInfo.GetTextElementEnumerator(Text);
        var index = 0;
        var lastKind = -1;
        while (e.MoveNext())
        {
            var kind = ElementKind(e.GetTextElement());
            if (lastKind >= 0 && kind != lastKind)
            {
                result.Add(index);
            }

            lastKind = kind;
            index++;
        }

        if (result[^1] != TextLength)
        {
            result.Add(TextLength);
        }

        return result;
    }

    internal void InsertTextAndMoveCaret(string text, int pos, int? endpos = null)
    {
        pos = Math.Clamp(pos, 0, TextLength);
        var insertionEnd = (pos > 0 ? StringInfo.SubstringByTextElements(0, pos).Length : 0) + text.Length;
        InsertText(text, pos, endpos);
        var boundaries = StringInfo.ParseCombiningCharacters(Text);
        var index = Array.BinarySearch(boundaries, insertionEnd);
        SetCaretPosition(index >= 0 ? index : ~index);
    }

    internal void ClampScroll() => TextBlock?.ClampScroll(ref _caretScroll, Box.RectInner.Size);

    private static int ElementKind(string element)
    {
        if (!Rune.TryGetRuneAt(element, 0, out var rune))
        {
            return 2;
        }

        if (Rune.IsWhiteSpace(rune))
        {
            return 0;
        }

        return Rune.IsLetterOrDigit(rune) || rune.Value == '_' ? 1 : 2;
    }

    private static bool IsNewline(string str) => str is "\n" or "\r\n" or "\r";

    private void ScrollParentToCaret()
    {
        if (_caretIntoView <= 0)
        {
            return;
        }

        if (Parent is not { } parent || TextBlock is null)
        {
            _caretIntoView = 0;
            return;
        }

        _caretIntoView = parent.ScrollIntoView(GetCaretRect(CaretPosition)) ? _caretIntoView - 1 : 0;
    }
}
