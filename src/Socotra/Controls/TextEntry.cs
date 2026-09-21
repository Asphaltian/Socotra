using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Socotra;

/// <summary>
/// A box the user can type text into. It takes focus, handles the usual editing keys, mouse selection, the clipboard,
/// undo and redo, and sends <c>onchange</c> as the text changes, <c>onsubmit</c> when Enter is pressed and <c>oncancel</c> on Escape.
/// </summary>
/// <example>
/// <code>
/// // A single line name field that reports every edit
/// var name = new TextEntry { Parent = form, Placeholder = "Your name", MaxLength = 16 };
/// name.OnTextEdited = text => player.Name = text;
/// </code>
/// </example>
public partial class TextEntry : Panel
{
    private const float CaretSolidTime = 0.5f;
    private const float CaretBlinkRate = 1;

    private bool _readOnly;
    private bool _multiline;
    private bool _pressedOnSelection;
    private Vector2 _pressPosition;
    private int? _dragSelectionAnchor;
    private bool _selectingWords;
    private int _wordSelectionStart;
    private int _wordSelectionEnd;
    private char _pendingSurrogate;
    private TextState? _imeState;
    private int _lastCaretPosition;
    private string? _lastText;
    private double _caretMovedTime;

    /// <summary>Makes an empty text entry.</summary>
    public TextEntry()
    {
        AcceptsFocus = true;
        AddClass("textentry");
        CanDragScroll = false;
        Label = new Label("", "content-label") { Multiline = _multiline };
        Label.Style.WhiteSpace = WhiteSpace.Pre;
        AddChild(Label);
    }

    /// <summary>Called with the new text every time the user changes it.</summary>
    [Parameter]
    public Action<string>? OnTextEdited { get; set; }

    /// <summary>Lets the user select and copy the text but not change it, and adds the <c>readonly</c> class. Unlike <see cref="Panel.Disabled"/>, the entry can still be focused.</summary>
    [Parameter]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            SetClass("readonly", value);
        }
    }

    /// <summary>The text in the entry.</summary>
    [Parameter]
    [AllowNull]
    public string Text
    {
        get => Label.Text;
        set
        {
            _imeState = null;
            Label.Text = value;
        }
    }

    /// <summary>
    /// The text in the entry, for binding to. Setting it does nothing while the entry has focus, so it doesn't fight the
    /// user's typing, and it clears the undo history. A <see cref="Numeric"/> entry tidies the number up.
    /// </summary>
    [Parameter]
    [AllowNull]
    public string Value
    {
        get => Label.Text;
        set
        {
            if (HasFocus || Label.Text == value)
            {
                return;
            }

            _imeState = null;
            Label.Text = value;
            if (Numeric)
            {
                Label.Text = FixNumeric();
            }

            ClearUndoHistory();
        }
    }

    /// <inheritdoc cref="Label.TextLength"/>
    public int TextLength => Label.TextLength;

    /// <inheritdoc cref="Label.CaretPosition"/>
    public int CaretPosition
    {
        get => Label.CaretPosition;
        set => Label.CaretPosition = value;
    }

    /// <summary>Replaces codes like <c>:smile:</c> with the emoji they name as the user types or pastes. See <see cref="Emoji"/>.</summary>
    public bool AllowEmojiReplace { get; set; }

    /// <inheritdoc/>
    public override bool AcceptsImeInput => true;

    /// <summary>How a <see cref="Numeric"/> entry formats its number, as a .NET format string like <c>0.00</c>.</summary>
    public string? NumberFormat { get; set; }

    /// <summary>Lets the user type several lines. Enter adds a new line instead of submitting. Adds the <c>is-multiline</c> class.</summary>
    [Parameter]
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
            Label.Multiline = value;
            SetClass("is-multiline", value);
        }
    }

    /// <summary>The lowest number a <see cref="Numeric"/> entry accepts.</summary>
    public float? MinValue { get; set; }

    /// <summary>The highest number a <see cref="Numeric"/> entry accepts.</summary>
    public float? MaxValue { get; set; }

    /// <summary>Text shown while the entry is empty, like a short hint of what goes in it. The label showing it gets the <c>placeholder</c> class.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>The label showing <see cref="Prefix"/>. Null when there's no prefix.</summary>
    public Label? PrefixLabel { get; protected set; }

    /// <summary>Text shown before the typed text, like a unit or a label. Adds the <c>has-prefix</c> class.</summary>
    public string? Prefix
    {
        get => PrefixLabel?.Text;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                PrefixLabel?.Delete();
                PrefixLabel = null;
                SetClass("has-prefix", false);
                return;
            }

            PrefixLabel ??= Add.Label(value, "prefix-label");
            PrefixLabel.Text = value;
            SetClass("has-prefix", true);
        }
    }

    /// <summary>The label showing <see cref="Suffix"/>. Null when there's no suffix.</summary>
    public Label? SuffixLabel { get; protected set; }

    /// <summary>Text shown after the typed text, like a unit. Adds the <c>has-suffix</c> class.</summary>
    public string? Suffix
    {
        get => SuffixLabel?.Text;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SuffixLabel?.Delete();
                SuffixLabel = null;
                SetClass("has-suffix", false);
                return;
            }

            SuffixLabel ??= Add.Label(value, "suffix-label");
            SuffixLabel.Text = value;
            SetClass("has-suffix", true);
        }
    }

    /// <summary>The color selected text is highlighted with. Translucent cyan by default.</summary>
    [Parameter]
    public Color SelectionColor
    {
        get => Label.SelectionColor;
        set => Label.SelectionColor = value;
    }

    /// <inheritdoc/>
    public override Rect ImeCaretRect => Label.TextBlock is not null ? Label.GetCaretRect(CaretPosition) : Box.Rect;

    /// <summary>The label holding the typed text.</summary>
    protected Label Label { get; }

    /// <summary>Whether the user can change the text right now: the entry is neither <see cref="ReadOnly"/> nor <see cref="Panel.Disabled"/>.</summary>
    protected bool CanEdit => !ReadOnly && !Disabled;

    /// <inheritdoc/>
    public override void OnPaste(string text)
    {
        if (!CanEdit)
        {
            return;
        }

        SetImePreview("");
        var start = Label.HasSelection() ? Math.Min(Label.SelectionStart, Label.SelectionEnd) : CaretPosition;
        var end = Label.HasSelection() ? Math.Max(Label.SelectionStart, Label.SelectionEnd) : start;
        PasteText(text, start, end);
    }

    /// <inheritdoc/>
    public override string? GetClipboardValue(bool cut)
    {
        var value = Label.GetClipboardValue(cut);
        if (cut && CanEdit && Label.HasSelection())
        {
            RecordEdit(EditKind.Single);
            Label.ReplaceSelection("");
            OnValueChanged();
        }

        return value;
    }

    /// <inheritdoc/>
    public override void OnButtonEvent(ButtonEvent e) => e.StopPropagation = true;

    /// <inheritdoc/>
    public override void OnButtonTyped(ButtonEvent e)
    {
        e.StopPropagation = true;
        var button = e.Button;
        if (Label.HasSelection() && button is ("delete" or "backspace") && CanEdit)
        {
            RecordEdit(EditKind.Single);
            Label.ReplaceSelection("");
            OnValueChanged();
            return;
        }

        switch (button)
        {
            case "delete":
                if (CaretPosition < TextLength && CanEdit)
                {
                    RecordEdit(EditKind.Deleting);
                    if (e.HasCtrl)
                    {
                        Label.MoveToWordBoundaryRight(true);
                        Label.ReplaceSelection("");
                    }
                    else
                    {
                        Label.RemoveText(CaretPosition, 1);
                    }

                    OnValueChanged();
                }

                return;

            case "backspace":
                if (CaretPosition > 0 && CanEdit)
                {
                    RecordEdit(EditKind.Deleting);
                    if (e.HasCtrl)
                    {
                        Label.MoveToWordBoundaryLeft(true);
                        Label.ReplaceSelection("");
                    }
                    else
                    {
                        Label.MoveCaretPos(-1);
                        Label.RemoveText(CaretPosition, 1);
                    }

                    OnValueChanged();
                }

                return;

            case "z" when e.HasCtrl:
                if (e.HasShift)
                {
                    Redo();
                }
                else
                {
                    Undo();
                }

                return;

            case "y" when e.HasCtrl:
                Redo();
                return;

            case "a" when e.HasCtrl:
                Label.SetSelection(0, TextLength);
                CaretPosition = TextLength;
                return;

            case "home":
                if (e.HasCtrl)
                {
                    Label.SetCaretPosition(0, e.HasShift);
                }
                else
                {
                    Label.MoveToLineStart(e.HasShift);
                }

                return;

            case "end":
                if (e.HasCtrl)
                {
                    Label.SetCaretPosition(TextLength, e.HasShift);
                }
                else
                {
                    Label.MoveToLineEnd(e.HasShift);
                }

                return;

            case "left":
                if (e.HasCtrl)
                {
                    Label.MoveToWordBoundaryLeft(e.HasShift);
                }
                else if (Label.HasSelection() && !e.HasShift)
                {
                    Label.SetCaretPosition(Math.Min(Label.SelectionStart, Label.SelectionEnd));
                }
                else
                {
                    Label.MoveCaretPos(-1, e.HasShift);
                }

                return;

            case "right":
                if (e.HasCtrl)
                {
                    Label.MoveToWordBoundaryRight(e.HasShift);
                }
                else if (Label.HasSelection() && !e.HasShift)
                {
                    Label.SetCaretPosition(Math.Max(Label.SelectionStart, Label.SelectionEnd));
                }
                else
                {
                    Label.MoveCaretPos(1, e.HasShift);
                }

                return;

            case "up" or "down":
                if (MoveAutoCompleteSelection(button == "up" ? -1 : 1))
                {
                    return;
                }

                if (string.IsNullOrEmpty(Text) && _history.Count > 0)
                {
                    UpdateAutoComplete([.. _history]);
                    MoveAutoCompleteSelection(-1);
                    return;
                }

                Label.MoveCaretLine(button == "up" ? -1 : 1, e.HasShift);
                return;

            case "enter" or "pad_enter":
                if (CommitAutoComplete())
                {
                    return;
                }

                if (Multiline)
                {
                    OnKeyTyped('\n');
                    return;
                }

                Blur();
                CreateEvent("onsubmit", Text);
                return;

            case "tab":
                if (MoveAutoCompleteSelection(e.HasShift ? -1 : 1))
                {
                    return;
                }

                break;
        }

        base.OnButtonTyped(e);
    }

    /// <inheritdoc/>
    public override void OnKeyTyped(char k)
    {
        if (char.IsHighSurrogate(k))
        {
            _pendingSurrogate = k;
            return;
        }

        if (char.IsLowSurrogate(k))
        {
            if (_pendingSurrogate == default)
            {
                return;
            }

            var pair = $"{_pendingSurrogate}{k}";
            _pendingSurrogate = default;
            if (CanEnterPair(pair))
            {
                InsertTyped(pair);
            }

            return;
        }

        _pendingSurrogate = default;
        if (CanEnterCharacter(k))
        {
            InsertTyped(k.ToString());
        }
    }

    /// <inheritdoc/>
    public override void OnDraw(Painter painter)
    {
        base.OnDraw(painter);
        var drawingDropCaret = DropCaretPosition >= 0 && CanEdit;
        if (!HasFocus && !drawingDropCaret)
        {
            return;
        }

        if (!drawingDropCaret && Label.HasSelection())
        {
            return;
        }

        var caret = Label.GetCaretRect(drawingDropCaret ? DropCaretPosition : CaretPosition);
        caret.Left = MathF.Floor(caret.Left);
        caret.Width = 1;
        var visible = Box.RectInner;
        caret.Left = MathF.Max(caret.Left, visible.Left);
        caret.Top = MathF.Max(caret.Top, visible.Top);
        caret.Right = MathF.Min(caret.Right, visible.Right);
        caret.Bottom = MathF.Min(caret.Bottom, visible.Bottom);
        if (caret.Width <= 0 || caret.Height <= 0)
        {
            return;
        }

        var sinceMoved = (float)(TimeNow - _caretMovedTime);
        var solid = drawingDropCaret || sinceMoved < CaretSolidTime;
        var blink = (sinceMoved - CaretSolidTime) * CaretBlinkRate % 1 < 0.5f;
        var color = ComputedStyle?.CaretColor ?? ComputedStyle?.FontColor ?? Color.Black;
        using var scope = painter.Scope();
        painter.Fill = solid || blink ? color : color.WithAlpha(0);
        painter.Stroke = Stroke.None;
        painter.Rect(caret - Box.Rect.Position);
    }

    /// <summary>Called after the user changes the text. It updates the <see cref="AutoComplete"/> suggestions, checks the text, then sends <c>onchange</c> and calls <see cref="OnTextEdited"/>.</summary>
    public virtual void OnValueChanged()
    {
        UpdateAutoComplete();
        UpdateValidation();
        var text = Numeric ? FixNumeric() : Text;
        CreateEvent("onchange");
        CreateValueEvent("value", text);
        OnTextEdited?.Invoke(text);
        EmptyStateChanged();
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        bool isPlaceholder = string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder);
        Label.SetClass("placeholder", isPlaceholder);
        Label.Style.Content = isPlaceholder ? Placeholder : null;
        Label.Selectable = !isPlaceholder;
        Label.ShouldDrawSelection = HasFocus;
        if (_lastCaretPosition != CaretPosition || _lastText != Text)
        {
            if (_lastText == Text)
            {
                BreakEditRun();
            }

            _lastCaretPosition = CaretPosition;
            _lastText = Text;
            _caretMovedTime = TimeNow;
        }

        if (!HasFocus)
        {
            _caretMovedTime = TimeNow;
        }
    }

    /// <summary>Sets <c>placeholder</c>, <c>numeric</c>, <c>format</c> and <c>value</c> from markup.</summary>
    public override void SetProperty(string name, string? value)
    {
        base.SetProperty(name, value);
        switch (name)
        {
            case "placeholder":
                Placeholder = value;
                break;

            case "numeric":
                Numeric = Translation.ToBool(value);
                break;

            case "format":
                NumberFormat = value;
                break;

            case "value" when !HasFocus:
                if (!Numeric)
                {
                    Text = value;
                }
                else if (Translation.TryParseFloat(value, out var number))
                {
                    Text = number.ToString(NumberFormat, CultureInfo.InvariantCulture);
                }

                break;
        }
    }

    /// <summary>The text as a tidy number: commas read as decimal points, anything unreadable as 0, kept between <see cref="MinValue"/> and <see cref="MaxValue"/> and formatted with <see cref="NumberFormat"/>.</summary>
    public virtual string FixNumeric()
    {
        if (!Translation.TryParseTypedNumber(Text, out var number))
        {
            number = 0;
        }

        number = Math.Clamp(number, MinValue ?? number, MaxValue ?? number);
        return WholeNumbers
            ? MathF.Round(number).ToString("0", CultureInfo.InvariantCulture)
            : number.ToString(NumberFormat, CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    protected override void OnEscape(PanelEvent e)
    {
        if (_localSelectionDrag is not null)
        {
            CancelTextDrag();
            _pressedOnSelection = false;
        }
        else
        {
            Cancel();
        }

        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        if (e.Button != "mouseleft" || ScrollBar.Owns(e.Target))
        {
            return;
        }

        e.StopPropagation();
        CancelTextDrag();
        _suppressDragSelection = false;
        _pressedOnSelection = false;
        _dragSelectionAnchor = null;
        _selectingWords = false;
        if (e.ClickCount == 2)
        {
            SelectWordOnPress();
            return;
        }

        if (e.ClickCount == 3)
        {
            SelectLineOnPress();
            return;
        }

        if (e.HasShift && !string.IsNullOrEmpty(Text))
        {
            var to = Label.GetLetterAtScreenPosition(ScreenMousePosition);
            if (to < 0)
            {
                return;
            }

            var anchor = Label.HasSelection() ? Label.SelectionStart : CaretPosition;
            _dragSelectionAnchor = anchor;
            Label.SelectionStart = anchor;
            Label.SelectionEnd = to;
            Label.CaretPosition = to;
            Label.ScrollToCaret();
            return;
        }

        _pressedOnSelection = IsPressOnSelection();
        _pressPosition = ScreenMousePosition;
        if (_pressedOnSelection || string.IsNullOrEmpty(Text))
        {
            return;
        }

        var pos = Label.GetLetterAtScreenPosition(ScreenMousePosition);
        Label.SelectionStart = 0;
        Label.SelectionEnd = 0;
        if (pos >= 0)
        {
            Label.SetCaretPosition(pos);
        }

        Label.ScrollToCaret();
    }

    /// <inheritdoc/>
    protected override void OnMouseUp(MousePanelEvent e)
    {
        if (e.Button != "mouseleft" || ScrollBar.Owns(e.Target))
        {
            return;
        }

        if (FinishTextDrag(e.HasCtrl))
        {
            e.StopPropagation();
            return;
        }

        if (_pressedOnSelection)
        {
            _pressedOnSelection = false;
            var letter = Label.GetLetterAtScreenPosition(ScreenMousePosition);
            Label.SelectionStart = 0;
            Label.SelectionEnd = 0;
            if (letter >= 0)
            {
                Label.SetCaretPosition(letter);
            }

            Label.ScrollToCaret();
            e.StopPropagation();
            return;
        }

        if (!Label.HasSelection())
        {
            var pos = Label.GetLetterAtScreenPosition(ScreenMousePosition);
            if (pos >= 0)
            {
                Label.SetCaretPosition(pos);
            }
        }

        Label.ScrollToCaret();
        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        if (ScrollBar.Owns(e.Target))
        {
            return;
        }

        e.StopPropagation();
        if (_pressedOnSelection && (_localSelectionDrag is not null || (ScreenMousePosition - _pressPosition).Length() > 5))
        {
            UpdateTextDrag();
        }
    }

    /// <inheritdoc/>
    protected override void OnFocus(PanelEvent e)
    {
        UpdateAutoComplete();
        _caretMovedTime = TimeNow;
    }

    /// <inheritdoc/>
    protected override void OnBlur(PanelEvent e)
    {
        SetImePreview("");
        CancelTextDrag();
        _pressedOnSelection = false;
        if (Numeric)
        {
            Text = FixNumeric();
        }
    }

    /// <inheritdoc/>
    protected override void OnTripleClick(MousePanelEvent e)
    {
        if (e.Button == "mouseleft" && !string.IsNullOrEmpty(Text))
        {
            e.StopPropagation();
        }
    }

    /// <inheritdoc/>
    protected override void OnDragSelect(SelectionEvent e)
    {
        if (string.IsNullOrEmpty(Text) || ScrollBar.Owns(e.Target) || _pressedOnSelection || _suppressDragSelection)
        {
            return;
        }

        Label.ShouldDrawSelection = true;
        var anchor = _dragSelectionAnchor ?? Label.GetLetterAtScreenPosition(e.StartPoint);
        var focus = Label.GetLetterAtScreenPosition(e.EndPoint);
        if (_selectingWords)
        {
            var boundaries = Label.GetWordBoundaryIndices();
            if (focus < _wordSelectionStart)
            {
                anchor = _wordSelectionEnd;
                focus = boundaries.LastOrDefault(x => x <= focus);
            }
            else
            {
                anchor = _wordSelectionStart;
                focus = Math.Max(_wordSelectionEnd, boundaries.FirstOrDefault(x => x >= focus, Label.TextLength));
            }
        }

        Label.SelectionStart = anchor;
        Label.SelectionEnd = focus;
        Label.CaretPosition = focus;
        Label.ScrollToCaret();
    }

    /// <inheritdoc/>
    protected override void OnEvent(PanelEvent e)
    {
        if (e.Name == "onimestart" && CanEdit)
        {
            SetImePreview("");
        }

        if (e.Name == "onime" && CanEdit)
        {
            SetImePreview(e.Value as string ?? "");
        }

        if (e.Name == "onimeend")
        {
            SetImePreview("");
        }

        base.OnEvent(e);
    }

    /// <summary>Whether the entry counts as empty for <c>:empty</c>: when there's no text.</summary>
    protected override bool IsPanelEmpty() => TextLength == 0;

    private void PasteText(string text, int start, int end, bool select = false)
    {
        if (!CanEdit)
        {
            return;
        }

        var current = new StringInfo(Text);
        var before = start > 0 ? current.SubstringByTextElements(0, start) : "";
        var after = end < current.LengthInTextElements ? current.SubstringByTextElements(end) : "";
        var context = before + after;
        var accepted = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var character = rune.ToString();
            if (character.Length == 1 ? !CanEnterCharacter(character[0]) : !CanEnterPair(character))
            {
                continue;
            }

            if (Numeric && !CanEnterNumericCharacter(character[0], context))
            {
                continue;
            }

            accepted.Append(character);
            if (Numeric && !char.IsDigit(character[0]))
            {
                context += character;
            }
        }

        var insertion = ReplaceEmojisInText(accepted.ToString());
        if (MaxLength.HasValue)
        {
            var elements = StringInfo.ParseCombiningCharacters(insertion);
            var count = elements.Length;
            while (count > 0)
            {
                var excess = new StringInfo(before + insertion + after).LengthInTextElements - MaxLength.Value;
                if (excess <= 0)
                {
                    break;
                }

                count = Math.Max(0, count - excess);
                insertion = insertion[..elements[count]];
            }
        }

        if (start == end && insertion.Length == 0)
        {
            return;
        }

        var changed = Text != before + insertion + after;
        if (changed)
        {
            RecordEdit(EditKind.Single);
        }

        Label.InsertTextAndMoveCaret(insertion, start, end);
        if (select)
        {
            Label.SetSelection(start, CaretPosition);
        }

        if (changed)
        {
            OnValueChanged();
        }
    }

    private void Cancel()
    {
        if (AutoCompletePanel is { IsDeleted: false })
        {
            AutoCompleteCancel();
            return;
        }

        Blur();
        CreateEvent("oncancel");
    }

    private bool IsPressOnSelection()
    {
        if (!Label.HasSelection())
        {
            return false;
        }

        var letter = Label.GetLetterAtScreenPosition(ScreenMousePosition);
        if (letter < 0)
        {
            return false;
        }

        var start = Math.Min(Label.SelectionStart, Label.SelectionEnd);
        var end = Math.Max(Label.SelectionStart, Label.SelectionEnd);
        return letter >= start && letter < end;
    }

    private void SelectLineOnPress()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var letter = Label.GetLetterAtScreenPosition(ScreenMousePosition);
        if (letter >= 0)
        {
            Label.CaretPosition = letter;
        }

        _pressedOnSelection = false;
        Label.MoveToLineStart();
        Label.MoveToLineEnd(true);
        _selectingWords = false;
    }

    private void SelectWordOnPress()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        _pressedOnSelection = false;
        Label.ShouldDrawSelection = true;
        Label.SelectWord(Label.GetLetterAtScreenPosition(ScreenMousePosition));
        _wordSelectionStart = Label.SelectionStart;
        _wordSelectionEnd = Label.SelectionEnd;
        _selectingWords = true;
    }

    private bool CanEnterPair(string pair) => !Numeric && (CharacterRegex is null || Regex.IsMatch(pair, CharacterRegex));

    private void InsertTyped(string text)
    {
        if (!CanEdit)
        {
            return;
        }

        SetImePreview("");
        if (MaxLength.HasValue && TextLength >= MaxLength && !Label.HasSelection())
        {
            return;
        }

        if (Label.HasSelection())
        {
            BreakEditRun();
        }

        RecordEdit(text.Length > 0 && char.IsWhiteSpace(text[0]) ? EditKind.Single : EditKind.Typing);
        if (Label.HasSelection())
        {
            Label.ReplaceSelection(text);
        }
        else
        {
            Label.InsertTextAndMoveCaret(text, CaretPosition);
        }

        if (text == ":")
        {
            RealtimeEmojiReplace();
        }

        OnValueChanged();
    }

    private void RealtimeEmojiReplace()
    {
        if (!AllowEmojiReplace || CaretPosition == 0)
        {
            return;
        }

        var text = Text;
        var caretChar = StringInfo.ParseCombiningCharacters(text)[CaretPosition - 1] + 1;
        string? lookup = null;
        var start = 0;
        for (int i = caretChar - 3; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return;
            }

            if (text[i] == ':')
            {
                start = i;
                lookup = text[i..caretChar];
                break;
            }
        }

        if (lookup is null || Emoji.FindEmoji(lookup) is not { } replace)
        {
            return;
        }

        var caret = CaretPosition - new StringInfo(lookup).LengthInTextElements + new StringInfo(replace).LengthInTextElements;
        Text = string.Concat(text.AsSpan(0, start), replace, text.AsSpan(caretChar));
        CaretPosition = caret;
    }

    private string ReplaceEmojisInText(string text)
    {
        if (!AllowEmojiReplace || string.IsNullOrEmpty(text))
        {
            return text;
        }

        return EmojiCodeRegex().Replace(text, match => Emoji.FindEmoji(match.Value) ?? match.Value);
    }

    private void SetImePreview(string text)
    {
        if (_imeState is { } original)
        {
            ApplyState(original);
        }

        _imeState = null;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _imeState = CurrentState();
        if (Label.HasSelection())
        {
            Label.ReplaceSelection(text);
        }
        else
        {
            Label.InsertTextAndMoveCaret(text, CaretPosition);
        }
    }

    [GeneratedRegex(":\\w+:")]
    private static partial Regex EmojiCodeRegex();
}
