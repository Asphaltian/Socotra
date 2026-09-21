using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class LabelTests
{
    public LabelTests() => Fonts.Load("Data/Fonts/Lato-Regular.ttf");

    private static RootPanel Root(string styles = "")
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("rootpanel { flex-direction: row; align-items: flex-start; font-family: Lato; font-size: 16px; } " + styles);
        return root;
    }

    private static Label Add(Panel parent, string text, string style = "")
    {
        var label = parent.AddChild<Label>();
        label.Text = text;
        label.Style.Set(style);
        return label;
    }

    [Fact]
    public void TextRoundTripsAndNullBecomesEmpty()
    {
        var label = new Label("Hi", "greeting");
        Assert.Equal("Hi", label.Text);
        Assert.True(label.HasClass("label") && label.HasClass("greeting"));

        label.Text = null;
        Assert.Equal("", label.Text);
    }

    [Fact]
    public void SetContentAndSetPropertyMatchText()
    {
        var content = new Label();
        content.SetContent("Hello");
        var property = new Label();
        property.SetProperty("text", "Hello");
        Assert.Equal("Hello", content.Text);
        Assert.Equal("Hello", property.Text);
    }

    [Fact]
    public void TextLengthCountsCharactersNotChars()
    {
        var label = new Label { Text = "a\U0001F44Db" };
        Assert.Equal(4, label.Text.Length);
        Assert.Equal(3, label.TextLength);
    }

    [Fact]
    public void LabelsAreMeasuredFromTheirText()
    {
        var root = Root();
        var shorter = Add(root, "Hello", "white-space: nowrap;");
        var longer = Add(root, "Hello Hello Hello", "white-space: nowrap;");
        Update(root);

        Assert.True(shorter.Box.Rect.Width > 0 && shorter.Box.Rect.Height > 0);
        Assert.True(longer.Box.Rect.Width > shorter.Box.Rect.Width * 2);
        Assert.Equal(shorter.Box.Rect.Height, longer.Box.Rect.Height);

        shorter.Text = "This is substantially longer";
        Update(root);
        Assert.True(shorter.Box.Rect.Width > longer.Box.Rect.Width);
    }

    [Fact]
    public void TextWrapsAtTheWidthUnlessWhiteSpaceSaysNot()
    {
        const string text = "aa bb cc dd ee ff gg hh";
        var root = Root();
        var wrapped = Add(root, text, "width: 100px;");
        var single = Add(root, text, "width: 100px; white-space: nowrap;");
        var pre = Add(root, text, "width: 100px; white-space: pre;");
        Update(root);

        Assert.Equal(100, wrapped.Box.Rect.Width);
        Assert.Equal(2, wrapped.TextBlock!.LineCount);
        Assert.True(wrapped.Box.Rect.Height > single.Box.Rect.Height * 1.5f);
        Assert.Equal(single.Box.Rect.Height, pre.Box.Rect.Height);
    }

    [Fact]
    public void WhiteSpaceCollapsesSpacesAndLineBreaksByMode()
    {
        var root = Root();
        var normal = Add(root, "a   b\nc", "white-space: normal;");
        var preLine = Add(root, "a   b\nc", "white-space: pre-line;");
        var pre = Add(root, "a   b\nc", "white-space: pre;");
        var reference = Add(root, "a b c", "white-space: nowrap;");
        Update(root);

        Assert.Equal(reference.Box.Rect.Size, normal.Box.Rect.Size);
        Assert.Equal(2, preLine.TextBlock!.LineCount);
        Assert.True(pre.Box.Rect.Width > preLine.Box.Rect.Width);
        Assert.Equal(pre.Box.Rect.Height, preLine.Box.Rect.Height);
    }

    [Fact]
    public void TrailingNewlineAddsALineOnlyWhenItIsKept()
    {
        var root = Root();
        var plain = Add(root, "Hello", "white-space: pre;");
        var kept = Add(root, "Hello\n", "white-space: pre;");
        var collapsed = Add(root, "Hello\n");
        Update(root);

        Assert.True(kept.Box.Rect.Height > plain.Box.Rect.Height * 1.5f);
        Assert.Equal(plain.Box.Rect.Height, collapsed.Box.Rect.Height);
    }

    [Fact]
    public void WordBreakAllSplitsLongWords()
    {
        var root = Root();
        var normal = Add(root, "WWWWWWWWWW", "width: 40px;");
        var breakAll = Add(root, "WWWWWWWWWW", "width: 40px; word-break: break-all;");
        Update(root);

        Assert.True(normal.TextBlock!.MeasureMinContent().X > breakAll.TextBlock!.MeasureMinContent().X * 3);
    }

    [Fact]
    public void TextTransformChangesWhatIsShaped()
    {
        var root = Root();
        var upper = Add(root, "small words", "text-transform: uppercase;");
        var reference = Add(root, "SMALL WORDS");
        var capitalized = Add(root, "small words", "text-transform: capitalize;");
        var capitalReference = Add(root, "Small Words");
        Update(root);

        Assert.Equal(reference.Box.Rect.Size, upper.Box.Rect.Size);
        Assert.Equal(capitalReference.Box.Rect.Size, capitalized.Box.Rect.Size);
        Assert.Equal("small words", upper.Text);
    }

    [Fact]
    public void EllipsisTruncatesTextThatDoesNotFit()
    {
        var root = Root();
        var clipped = Add(root, "a long line of text that cannot fit", "width: 80px; white-space: nowrap; text-overflow: ellipsis;");
        var fitting = Add(root, "fits", "width: 80px; white-space: nowrap; text-overflow: ellipsis;");
        Update(root);
        Update(root);

        Assert.True(clipped.TextBlock!.IsTruncated);
        Assert.InRange(clipped.TextBlock.BlockSize.X, 60, 81);
        Assert.True(clipped.TextBlock.Measure(float.NaN, float.NaN).X > 150);
        Assert.Equal(fitting.TextBlock!.Measure(float.NaN, float.NaN).X, fitting.TextBlock.BlockSize.X);
    }

    [Fact]
    public void LineHeightScalesEachLine()
    {
        var root = Root();
        var normal = Add(root, "one\ntwo", "white-space: pre;");
        var doubled = Add(root, "one\ntwo", "white-space: pre; line-height: 2;");
        var pixels = Add(root, "one\ntwo", "white-space: pre; line-height: 40px;");
        Update(root);

        Assert.InRange(doubled.Box.Rect.Height / normal.Box.Rect.Height, 1.8f, 2.2f);
        Assert.InRange(pixels.Box.Rect.Height / normal.Box.Rect.Height, 2.3f, 2.7f);
    }

    [Fact]
    public void LetterAndWordSpacingWidenTheText()
    {
        var root = Root();
        var plain = Add(root, "a b c d", "white-space: nowrap;");
        var letters = Add(root, "a b c d", "white-space: nowrap; letter-spacing: 4px;");
        var words = Add(root, "a b c d", "white-space: nowrap; word-spacing: 10px;");
        Update(root);

        Assert.InRange(letters.Box.Rect.Width - plain.Box.Rect.Width, 24, 32);
        Assert.InRange(words.Box.Rect.Width - plain.Box.Rect.Width, 25, 35);
    }

    [Fact]
    public void MinContentIsTheLongestWord()
    {
        var root = Root();
        var label = Add(root, "small extraordinary words");
        var word = Add(root, "extraordinary");
        Update(root);

        Assert.Equal(word.TextBlock!.Measure(float.NaN, float.NaN).X, label.TextBlock!.MeasureMinContent().X, 1f);
    }

    [Fact]
    public void ContentReplacesTheShownText()
    {
        var root = Root();
        var label = Add(root, "Visible text");
        Update(root);
        Assert.Equal("Visible text", label.TextBlock!.Text);

        label.Style.Content = "Other";
        Update(root);
        Assert.Equal("Other", label.TextBlock.Text);
        Assert.Equal("Visible text", label.Text);
    }

    [Fact]
    public void BeforeAndAfterElementsShowContent()
    {
        var root = Root(".tagged::before { content: '['; } .tagged::after { content: ']'; }");
        var panel = root.AddChild<Panel>("tagged");
        Add(panel, "x");
        Update(root);
        Update(root);

        var before = Assert.IsType<Label>(panel.Children[0]);
        var after = Assert.IsType<Label>(panel.Children[^1]);
        Assert.Equal("[", before.TextBlock!.Text);
        Assert.Equal("]", after.TextBlock!.Text);
        Assert.False(before.HasClass("label"));
    }

    [Fact]
    public void RichTextStylesItsElements()
    {
        var root = Root("b { font-size: 32px; }");
        var rich = Add(root, "plain <b>big</b>", "white-space: nowrap;");
        rich.IsRich = true;
        var plain = Add(root, "plain big", "white-space: nowrap;");
        Update(root);

        Assert.True(rich.Box.Rect.Height > plain.Box.Rect.Height * 1.5f);
        Assert.Equal(2, rich.TextBlock!.HtmlSpans!.Count);
    }

    [Fact]
    public void LoadingAFontRestylesText()
    {
        const string font = @"C:\Windows\Fonts\consola.ttf";
        if (!File.Exists(font))
        {
            return;
        }

        var root = Root();
        var label = Add(root, "text");
        Update(root);
        Assert.False(label.TextBlock!.UpdateStyles(label.ComputedStyle!));

        Fonts.Load(font);

        Assert.True(label.TextBlock.UpdateStyles(label.ComputedStyle!));
    }

    [Fact]
    public void SelectionSetBeforeTheFirstUpdateIsKept()
    {
        var root = Root();
        var label = Add(root, "hello");
        label.SelectionStart = 1;
        label.SelectionEnd = 4;
        label.SelectionColor = new Color(1, 0, 0);

        Update(root);

        Assert.Equal((1, 4), (label.SelectionStart, label.SelectionEnd));
        Assert.Equal(new Color(1, 0, 0), label.TextBlock!.SelectionColor);
    }

    [Fact]
    public void TurningRichTextOnLaysTheLabelOutAgain()
    {
        var root = Root("b { font-size: 32px; }");
        var label = Add(root, "plain <b>big</b>", "white-space: nowrap;");
        Update(root);
        var height = label.Box.Rect.Height;

        label.IsRich = true;
        Update(root);

        Assert.True(label.Box.Rect.Height > height * 1.5f);
    }

    [Fact]
    public void RichTextNeedsEveryClassInARule()
    {
        var root = Root(".a.b { font-size: 32px; }");
        var partial = Add(root, "plain <span class=\"a\">big</span>", "white-space: nowrap;");
        partial.IsRich = true;
        var both = Add(root, "plain <span class=\"a b\">big</span>", "white-space: nowrap;");
        both.IsRich = true;
        var plain = Add(root, "plain big", "white-space: nowrap;");
        Update(root);

        Assert.Equal(plain.Box.Rect.Height, partial.Box.Rect.Height);
        Assert.True(both.Box.Rect.Height > plain.Box.Rect.Height * 1.5f);
    }

    [Fact]
    public void StyleSpansStyleRangesOfText()
    {
        var root = Root();
        var label = Add(root, "small big", "white-space: nowrap;");
        var plain = Add(root, "small big", "white-space: nowrap;");
        Update(root);
        var height = plain.Box.Rect.Height;

        label.SetStyleSpan(6, 9, new Styles { FontSize = 32 });
        Update(root);
        Assert.True(label.Box.Rect.Height > height * 1.5f);

        Assert.Throws<ArgumentException>(() => label.SetStyleSpan(0, 2, new Styles()));
        label.Text = "other";
        Update(root);
        Assert.Equal(height, label.Box.Rect.Height);
    }

    [Fact]
    public void SelectedTextGoesToTheClipboard()
    {
        var root = Root();
        var label = Add(root, "Hello World", "white-space: nowrap;");
        Update(root);

        label.ShouldDrawSelection = true;
        label.SetSelection(6, 99);
        Assert.Equal(11, label.SelectionEnd);
        Assert.Equal("World", label.GetSelectedText());
        Assert.Equal("World", label.GetClipboardValue(false));

        label.Selectable = false;
        label.ShouldDrawSelection = true;
        Assert.Null(label.GetClipboardValue(false));
    }

    [Fact]
    public void LettersAreFoundByScreenPosition()
    {
        var root = Root();
        var label = Add(root, "Hello World", "white-space: nowrap;");
        Update(root);
        var box = label.Box.Rect;

        Assert.Equal(0, label.GetLetterAtScreenPosition(box.Position + new Vector2(1, box.Height / 2)));
        Assert.Equal(11, label.GetLetterAtScreenPosition(box.Position + new Vector2(box.Width - 1, box.Height / 2)));
        Assert.Equal(-1, label.GetCharacterAtScreenPosition(box.Position + new Vector2(box.Width + 50, box.Height / 2)));
        var caret = label.GetCaretRect(11);
        Assert.InRange(caret.Left, box.Right - 3, box.Right + 1);
    }

    [Fact]
    public void EditingMovesTheCaretByCharactersWordsAndLines()
    {
        var root = Root();
        var label = Add(root, "one two\nthree", "white-space: pre;");
        Update(root);

        label.InsertText("!", 3);
        Assert.Equal("one! two\nthree", label.Text);
        label.RemoveText(3, 1);
        Assert.Equal("one two\nthree", label.Text);
        Update(root);

        label.SetCaretPosition(0);
        label.MoveToWordBoundaryRight(false);
        Assert.Equal(3, label.CaretPosition);
        label.MoveToWordBoundaryRight(true);
        Assert.Equal(4, label.CaretPosition);
        Assert.Equal((3, 4), (label.SelectionStart, label.SelectionEnd));

        label.SetCaretPosition(2);
        label.MoveToLineEnd();
        Assert.Equal(7, label.CaretPosition);
        label.MoveCaretLine(1, false);
        Assert.Equal(1, label.TextBlock!.LineOf(label.CaretPosition));
        label.MoveToLineStart();
        Assert.Equal(8, label.CaretPosition);
        label.MoveCaretLine(-1, false);
        Assert.Equal(0, label.CaretPosition);

        label.SelectWord(5);
        Assert.Equal((4, 7), (label.SelectionStart, label.SelectionEnd));
        Assert.Equal(new[] { 0, 3, 4, 7, 8, 13 }, label.GetWordBoundaryIndices());
    }

    [Fact]
    public void ChildSelectionCopiesEveryLabel()
    {
        var root = Root();
        var column = root.AddChild<Panel>();
        column.Style.Set("flex-direction: column;");
        column.AllowChildSelection = true;
        Add(column, "first");
        Add(column, "second");
        var row = root.AddChild<Panel>();
        row.AllowChildSelection = true;
        Add(row, "left");
        Add(row, "right");
        Update(root);

        column.SelectAllInChildren();
        row.SelectAllInChildren();
        Assert.Equal("first\nsecond", column.GetClipboardValue(false));
        Assert.Equal("left right", row.GetClipboardValue(false));
        Assert.Equal("right", row.Children[1].GetClipboardValue(false));

        column.UnselectAllInChildren();
        Assert.Null(column.GetClipboardValue(false));
    }

    [Fact]
    public void DraggingAcrossLabelsSelectsTheirText()
    {
        var root = Root("rootpanel { pointer-events: all; }");
        var column = root.AddChild<Panel>();
        column.Style.Set("flex-direction: column;");
        column.AllowChildSelection = true;
        var first = Add(column, "first line");
        var second = Add(column, "second line");
        Update(root);

        root.SetMousePosition(first.Box.Rect.Position + new Vector2(1, 5));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        root.SetMousePosition(second.Box.Rect.Position + new Vector2(second.GetCaretRect(6).Left - second.Box.Rect.Left, 5));
        Update(root);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);

        Assert.Equal("first line\nsecond", column.GetClipboardValue(false));
    }

    [Fact]
    public void CaretClampsWhenTextShrinks()
    {
        var label = new Label { Text = "Hello World" };
        label.CaretPosition = 11;
        label.Text = "Hi";
        Assert.Equal(2, label.CaretPosition);
    }

    [Fact]
    public void RazorTextIsGeneratedLabels()
    {
        var root = Root();
        new PanelRenderTreeBuilder(root).Build(builder => builder.AddMarkupContent(0, "legacy text"));
        Update(root);

        var label = Assert.IsType<Label>(Assert.Single(root.Children));
        Assert.True(label.IsGeneratedText);
        Assert.Equal("legacy text", label.Text);
    }
}
