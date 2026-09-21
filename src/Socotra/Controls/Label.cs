using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Socotra.Html;
using Socotra.Layout;

namespace Socotra;

/// <summary>A panel that shows text. Set <see cref="IsRich"/> to show simple markup, or use a <see cref="TextEntry"/> to let the user edit it.</summary>
/// <example>
/// <code>
/// // A label with a class to style it by
/// var title = new Label("Settings", "title") { Parent = menu };
/// </code>
/// </example>
public partial class Label : Panel
{
    private string _text = "";
    private Rect _textRect;
    private int _layoutStateHash;
    private bool _sizeFinalized;
    private Vector2 _availableSpace;
    private int _caretPosition;
    private INode? _hoveredNode;
    private CursorOverride _linkCursor;
    private bool _isRich;
    private bool _drawSelection;
    private bool _clipsBackgroundToText;
    private int _selectionStart;
    private int _selectionEnd;
    private Color _selectionColor = Socotra.TextBlock.DefaultSelectionColor;

    /// <summary>Makes an empty label.</summary>
    public Label()
    {
        AddClass("label");
        LayoutTree.SetMeasure(MeasureText);
    }

    /// <summary>Makes a label showing <paramref name="text"/>, with the given classes.</summary>
    public Label(string? text, string? classname = null)
        : this()
    {
        Text = text;
        AddClass(classname);
    }

    /// <summary>The text to show.</summary>
    [Parameter]
    [AllowNull]
    public virtual string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (_text == value)
            {
                return;
            }

            _text = value;
            ClearStyleSpans();
            StringInfo.String = value;
            CaretSantity();
            LayoutTree.MarkDirty();
            SetNeedsPreLayout();
        }
    }

    /// <summary>Shows <see cref="Text"/> as markup, so tags like <c>&lt;b&gt;</c>, <c>&lt;br&gt;</c> and <c>&lt;a href&gt;</c> can be styled with the label's stylesheets.</summary>
    [Parameter]
    public bool IsRich
    {
        get => _isRich;
        set
        {
            if (_isRich == value)
            {
                return;
            }

            _isRich = value;
            LayoutTree.MarkDirty();
            SetNeedsPreLayout();
        }
    }

    /// <summary>Called with the element clicked inside rich text. Without it, clicking a link opens its <c>http</c> or <c>https</c> address in the browser.</summary>
    [Parameter]
    public Action<INode>? OnNodeClicked { get; set; }

    /// <summary>Whether the user can select the text.</summary>
    public bool Selectable { get; set; } = true;

    /// <summary>Whether the selection is shown. Turning it on does nothing unless <see cref="Selectable"/> is set.</summary>
    public bool ShouldDrawSelection
    {
        get => TextBlock?.ShouldDrawSelection ?? _drawSelection;
        set
        {
            _drawSelection = Selectable && value;
            if (TextBlock is not null)
            {
                TextBlock.ShouldDrawSelection = _drawSelection;
            }
        }
    }

    /// <summary>Where the selection starts, in characters. It can be after <see cref="SelectionEnd"/> when the selection runs backwards.</summary>
    public int SelectionStart
    {
        get => TextBlock?.SelectionStart ?? _selectionStart;
        set
        {
            _selectionStart = value;
            if (TextBlock is not null)
            {
                TextBlock.SelectionStart = value;
            }
        }
    }

    /// <summary>Where the selection ends, in characters.</summary>
    public int SelectionEnd
    {
        get => TextBlock?.SelectionEnd ?? _selectionEnd;
        set
        {
            _selectionEnd = value;
            if (TextBlock is not null)
            {
                TextBlock.SelectionEnd = value;
            }
        }
    }

    /// <summary>The color selected text is highlighted with. Translucent cyan by default.</summary>
    public Color SelectionColor
    {
        get => TextBlock?.SelectionColor ?? _selectionColor;
        set
        {
            _selectionColor = value;
            if (TextBlock is not null)
            {
                TextBlock.SelectionColor = value;
            }
        }
    }

    /// <summary>Where typed characters go, counted in characters from the start. Setting it keeps it inside the text and scrolls it into view.</summary>
    public int CaretPosition
    {
        get => _caretPosition;
        set
        {
            value = Math.Clamp(value, 0, TextLength);
            if (_caretPosition == value)
            {
                return;
            }

            _caretPosition = value;
            if (!_movingLine)
            {
                _desiredCaretX = null;
            }

            ScrollToCaret();
        }
    }

    /// <summary>How many characters the text has. An emoji or a letter with an accent counts as one.</summary>
    public int TextLength => StringInfo.LengthInTextElements;

    /// <summary>The text, split into the characters <see cref="CaretPosition"/> and the selection count in.</summary>
    protected StringInfo StringInfo { get; } = new();

    internal TextBlock? TextBlock { get; private set; }

    internal bool IsGeneratedText { get; set; }

    private Rect TextLayoutRect => new(Box.RectInner.Position - _caretScroll, Box.RectInner.Size);

    /// <inheritdoc/>
    public override void SetProperty(string name, string? value)
    {
        if (name == "text")
        {
            Text = value;
            return;
        }

        base.SetProperty(name, value);
    }

    /// <inheritdoc/>
    public override void SetContent(string? value) => Text = value;

    /// <summary>The selected text, or an empty string when nothing is selected.</summary>
    public string GetSelectedText()
    {
        if (TextLength == 0 || !HasSelection())
        {
            return "";
        }

        CaretSantity();
        var start = Math.Min(SelectionStart, SelectionEnd);
        var end = Math.Max(SelectionStart, SelectionEnd);
        return StringInfo.SubstringByTextElements(start, end - start);
    }

    /// <inheritdoc/>
    public override string? GetClipboardValue(bool cut)
    {
        if (LayoutTree.IsInlineParticipant)
        {
            return LayoutTree.SelectedInlineText;
        }

        return HasSelection() ? GetSelectedText() : null;
    }

    /// <summary>Where the caret is drawn when it's before character <paramref name="i"/>, on screen.</summary>
    public Rect GetCaretRect(int i)
    {
        var rect = TextBlock?.CaretRect(i) ?? default;
        rect.Position += _textRect.Position - _caretScroll;
        rect.Width = 2;
        return rect;
    }

    /// <summary>The caret position nearest <paramref name="pos"/>, measured from the text's top left corner. Returns -1 before the text is laid out.</summary>
    public int GetLetterAt(Vector2 pos) => TextBlock?.GetLetterAt(pos) ?? -1;

    /// <summary>The caret position nearest a point on screen.</summary>
    public int GetLetterAtScreenPosition(Vector2 pos) => GetLetterAt(ScreenPositionToTextRectPosition(pos));

    /// <summary>The character under a point on screen, or -1 if the point isn't over any. Both halves of a character give the same answer.</summary>
    public int GetCharacterAtScreenPosition(Vector2 pos) => TextBlock?.GetCharacterAt(ScreenPositionToTextRectPosition(pos)) ?? -1;

    /// <summary>Whether some text is selected and the selection is shown.</summary>
    public bool HasSelection() => ShouldDrawSelection && SelectionStart != SelectionEnd;

    /// <inheritdoc/>
    public override void OnDeleted()
    {
        base.OnDeleted();
        TextBlock = null;
    }

    /// <inheritdoc/>
    public override void OnDraw(Painter painter)
    {
        if (LayoutTree.IsInlineParticipant || TextBlock is null)
        {
            return;
        }

        if (TextBlock.BlockSize == Vector2.Zero && !string.IsNullOrEmpty(TextBlock.Text))
        {
            TextBlock.SizeFinalized(Box.RectInner.Width, Box.RectInner.Height);
        }

        if (_clipsBackgroundToText)
        {
            return;
        }

        TextBlock.Draw(painter, painter.InheritedBlendMode, ComputedStyle, TextLayoutRect, painter.InheritedOpacity);
    }

    internal bool GetTextMask([NotNullWhen(true)] out Texture? texture, out Rect rect)
    {
        texture = null;
        rect = default;
        if (!_clipsBackgroundToText || TextBlock is null || ComputedStyle is null)
        {
            return false;
        }

        return TextBlock.GetMask(ComputedStyle, TextLayoutRect, out texture, out rect);
    }

    internal override void PreLayout(LayoutCascade cascade)
    {
        base.PreLayout(cascade);
        if (ComputedStyle is not { } style)
        {
            return;
        }

        var text = style.Content ?? Text;
        if (TextBlock is null)
        {
            TextBlock = new TextBlock
            {
                LookupStyles = HtmlStyleLookup,
                ShouldDrawSelection = _drawSelection,
                SelectionStart = _selectionStart,
                SelectionEnd = _selectionEnd,
                SelectionColor = _selectionColor,
            };
        }

        TextBlock.NoWrap = !Multiline;
        _clipsBackgroundToText = (!IsFixed && cascade.ClipBackgroundToText) || style.BackgroundClip == BackgroundClip.Text;
        if (IsRich)
        {
            TextBlock.SetHtml(text);
            TextBlock.NoWrap = false;
        }
        else
        {
            TextBlock.SetText(text);
        }

        var stateHash = HashCode.Combine((int)(_availableSpace.X * 100), ScaleToScreen, TextBlock.IsTruncated, _hoveredNode);
        if (stateHash != _layoutStateHash)
        {
            _layoutStateHash = stateHash;
            _sizeFinalized = false;
        }

        TextBlock.StyleSpans = _styleSpans;
        TextBlock.StyleSpanScale = ScaleToScreen;
        if (TextBlock.UpdateStyles(style))
        {
            LayoutTree.MarkDirty();
            _sizeFinalized = false;
        }
    }

    internal override void FinalLayout(Vector2 offset)
    {
        base.FinalLayout(offset);
        if (LayoutTree.IsInlineParticipant || !IsVisible || ComputedStyle is not { } style || TextBlock is null)
        {
            return;
        }

        var content = Box.RectInner;
        TextBlock.SizeFinalized(content.Width, content.Height);
        if (!_sizeFinalized)
        {
            _sizeFinalized = true;
            LayoutTree.MarkDirty();
        }

        _textRect = TextBlock.GetAlignedRect(style, content);
        _textRect.Size = TextBlock.BlockSize;
        if (_scrolledSize != content.Size)
        {
            _scrolledSize = content.Size;
            ScrollToCaret();
        }

        ScrollParentToCaret();
    }

    /// <summary>Keeps <see cref="CaretPosition"/> and the selection inside the text. Call it after changing the text some other way than through the label's methods.</summary>
    protected void CaretSantity()
    {
        if (CaretPosition == 0 && SelectionStart == 0 && SelectionEnd == 0)
        {
            ClampScroll();
            return;
        }

        if (CaretPosition > TextLength)
        {
            CaretPosition = TextLength;
            ScrollToCaret();
        }

        if (SelectionStart > TextLength)
        {
            SelectionStart = TextLength;
            ScrollToCaret();
        }

        if (SelectionEnd > TextLength)
        {
            SelectionEnd = TextLength;
            ScrollToCaret();
        }

        ClampScroll();
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        if (TextBlock is null || !IsRich)
        {
            _hoveredNode = null;
            return;
        }

        var hovered = TextBlock.GetSpanAt(e.LocalPosition)?.Node;
        if (hovered == _hoveredNode)
        {
            return;
        }

        _hoveredNode?.SetPseudoClass(PseudoClass.None);
        _hoveredNode = hovered;
        _hoveredNode?.SetPseudoClass(PseudoClass.Hover);
        _linkCursor.Set(this, _hoveredNode?.Name == "a" ? "pointer" : null);
        TextBlock.Dirty();
        SetNeedsPreLayout();
    }

    /// <inheritdoc/>
    protected override void OnClick(MousePanelEvent e)
    {
        base.OnClick(e);
        if (_hoveredNode is null)
        {
            return;
        }

        if (OnNodeClicked is not null)
        {
            OnNodeClicked(_hoveredNode);
            return;
        }

        if (_hoveredNode.GetAttribute("href", "") is not { Length: > 0 } url)
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            Log.Warning($"Blocked URL: {url}");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true, Verb = "open" });
    }

    private Vector2 MeasureText(float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        if (TextBlock is null)
        {
            return new Vector2(2, 10);
        }

        if (widthMode == MeasureMode.MinContent)
        {
            return TextBlock.MeasureMinContent();
        }

        if (heightMode == MeasureMode.MinContent)
        {
            return TextBlock.MeasureMinContent(widthMode == MeasureMode.Undefined ? float.NaN : width);
        }

        _availableSpace = new Vector2(width, height);
        return _sizeFinalized && TextBlock.IsTruncated ? TextBlock.BlockSize : TextBlock.Measure(width, height);
    }

    private Vector2 ScreenPositionToTextRectPosition(Vector2 pos)
    {
        var local = ScreenPositionToPanelPosition(pos) + Box.Rect.Position;
        return local - _textRect.Position + _caretScroll;
    }

    private Styles HtmlStyleLookup(INode node)
    {
        var styles = new Styles();
        styles.Add(ComputedStyle!);
        var local = new Styles();
        var rules = AllStyleSheets.SelectMany(sheet => sheet.Blocks).Select(block => block.Test(node)).OfType<StyleSelector>().ToList();
        rules.Sort(StyleSelector.ByScore);
        foreach (var rule in rules)
        {
            local.Add(rule.Block!.Styles);
        }

        if (node.GetAttribute("style", "") is { Length: > 0 } inline)
        {
            local.Set(inline);
        }

        local.ApplyScale(ScaleToScreen);
        styles.Add(local);
        return styles;
    }
}

/// <summary>Adds labels through <see cref="Panel.Add"/>.</summary>
public static class LabelConstructor
{
    /// <summary>Adds a label showing <paramref name="text"/>, with the classes in <paramref name="classname"/>.</summary>
    public static Label Label(this PanelCreator self, string? text = null, string? classname = null)
    {
        var control = self.panel.AddChild<Label>(classname);
        if (text is not null)
        {
            control.Text = text;
        }

        return control;
    }
}
