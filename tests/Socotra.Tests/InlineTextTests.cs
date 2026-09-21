using Microsoft.AspNetCore.Components;
using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class InlineTextTests
{
    public InlineTextTests() => Fonts.Load("Data/Fonts/Lato-Regular.ttf");

    private const string Styles = "rootpanel { flex-direction: column; align-items: flex-start; }";

    private static Panel Paragraph(Panel root, int width = 180)
    {
        var p = root.AddChild<Panel>();
        p.Style.Set($"display: block; width: {width}px; font-family: Lato; font-size: 20px;");
        p.AllowChildSelection = true;
        return p;
    }

    private static Label Text(Panel parent, string text)
    {
        var label = parent.AddChild<Label>();
        label.Style.Display = DisplayMode.Inline;
        label.Text = text;
        return label;
    }

    private static Panel Span(Panel parent, string style = "")
    {
        var span = parent.AddChild<Panel>();
        span.Style.Set("display: inline; " + style);
        return span;
    }

    [Fact]
    public void SpansShareOneParagraph()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        var prefix = Text(p, "Before ");
        var link = Span(p, "color: red;");
        var nested = Span(link, "font-weight: 700;");
        var text = Text(nested, "a long interactive link that wraps across several lines");
        Update(root);

        var context = p.LayoutTree.InlineContext!;
        Assert.Same(context, text.LayoutTree.InlineContext);
        Assert.True(p.LayoutTree.HasInlineContent);
        Assert.True(text.LayoutTree.IsInlineParticipant && link.LayoutTree.IsInlineParticipant);
        var fragments = link.LayoutTree.Node.InlineFragments;
        Assert.True(fragments.Select(f => f.Y).Distinct().Count() > 1);
        Assert.True(fragments[0].X > 0);
        Assert.Equal(fragments.Count, nested.LayoutTree.Node.InlineFragments.Count);
        Assert.True(prefix.LayoutTree.HasMeasure);
        Assert.True(link.IsInside(context.Origin + new Vector2(fragments[0].X + 1, fragments[0].Y + 1)));
        Assert.False(link.IsInside(context.Origin + new Vector2(1, fragments[0].Y + 1)));

        var height = p.Box.Rect.Height;
        p.Style.Width = 500;
        Update(root);
        Assert.True(p.Box.Rect.Height < height);

        text.Text = "short";
        Update(root);
        Assert.Equal("Before short", context.Text.Text);
        var width = text.Box.Rect.Width;
        nested.Style.FontSize = 40;
        Update(root);
        Assert.True(text.Box.Rect.Width > width);
    }

    [Fact]
    public void WhiteSpaceCollapsesAcrossSpansAndCopiesAsOneString()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        Text(p, "  hello \t");
        Text(Span(p), "\n  world");
        Text(p, " !  ");
        Update(root);

        Assert.Equal("hello world !", p.LayoutTree.InlineContext!.Text.Text);
        p.SelectAllInChildren();
        Assert.Equal("hello world !", p.GetClipboardValue(false));
        p.UnselectAllInChildren();
        Assert.Null(p.GetClipboardValue(false));
    }

    [Fact]
    public void SplitWordsGetNoSpaceAndEqualStylesShapeTogether()
    {
        var root = Root(Styles);
        var split = Paragraph(root, 400);
        Text(split, "inter");
        var last = Text(split, "active");
        var whole = Paragraph(root, 400);
        Text(whole, "interactive");
        Update(root);

        Assert.Equal("interactive", split.LayoutTree.InlineContext!.Text.Text);
        Assert.Equal(whole.LayoutTree.InlineContext!.Text.MeasuredSize.X, split.LayoutTree.InlineContext.Text.MeasuredSize.X, 0.001f);

        var hash = last.TextBlock!.InlineStyleHash;
        last.Style.FontColor = new Color(1, 0, 0);
        Update(root);
        Assert.NotEqual(hash, last.TextBlock.InlineStyleHash);
    }

    [Fact]
    public void DragSelectionCrossesSpansWithoutSeparators()
    {
        var root = Root(Styles);
        var p = Paragraph(root, 400);
        Text(p, "hello ");
        var last = Text(p, "world");
        Update(root);

        var origin = p.LayoutTree.InlineContext!.Origin;
        last.DispatchEventImmediate(new SelectionEvent("ondragselect", last) { StartPoint = origin, EndPoint = origin + new Vector2(350, 1) });
        Assert.Equal("hello world", last.GetClipboardValue(false));
    }

    [Fact]
    public void SourceRangesStayInTheOriginalText()
    {
        var root = Root(Styles);
        var p = Paragraph(root, 400);
        var text = Text(p, "  A\U0001F600B");
        Update(root);

        var fragments = text.LayoutTree.Node.InlineFragments;
        Assert.Equal(2, fragments.Min(f => f.TextStart));
        Assert.Equal(6, fragments.Max(f => f.TextStart + f.TextLength));
        p.SelectAllInChildren();
        Assert.Equal("A\U0001F600B", p.GetClipboardValue(false));
    }

    [Fact]
    public void EmptyParagraphHasNoFragments()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        var text = Text(p, " \t\n");
        Update(root);

        Assert.Equal("", p.LayoutTree.InlineContext!.Text.Text);
        Assert.Empty(text.LayoutTree.Node.InlineFragments);
        text.Text = "hello";
        Update(root);
        Assert.NotEmpty(text.LayoutTree.Node.InlineFragments);
    }

    [Fact]
    public void GeneratedTextAloneKeepsBlockLayout()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        new PanelRenderTreeBuilder(p).Build(builder => builder.AddMarkupContent(0, "legacy text"));
        Update(root);

        Assert.True(p.Children.OfType<Label>().Single().IsGeneratedText);
        Assert.Null(p.LayoutTree.InlineContext);

        var span = Text(p, " inline");
        Update(root);
        Assert.Equal("legacy text inline", p.LayoutTree.InlineContext!.Text.Text);

        span.Style.Display = DisplayMode.None;
        Update(root);
        Assert.Null(p.LayoutTree.InlineContext);
        Assert.All(p.Children, c => Assert.Null(c.LayoutTree.InlineContext));
    }

    [Fact]
    public void RazorSpansJoinTheParagraph()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        new PanelRenderTreeBuilder(p).Build(builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "style", "display: inline;");
            builder.AddContent(2, "one ");
            builder.CloseElement();
            builder.OpenElement(3, "span");
            builder.AddAttribute(4, "style", "display: inline;");
            builder.AddContent(5, " two");
            builder.CloseElement();
        });
        Update(root);

        Assert.Equal("one two", p.LayoutTree.InlineContext!.Text.Text);
        var label = p.Children[0].Children.OfType<Label>().Single();
        Assert.True(label.IsGeneratedText);
        Assert.Equal(DisplayMode.Flex, label.ComputedStyle!.Display);
    }

    [Theory]
    [InlineData("text-align: center;")]
    [InlineData("white-space: pre;")]
    [InlineData("text-transform: uppercase;")]
    [InlineData("padding-left: 10px;")]
    [InlineData("background-color: red;")]
    [InlineData("opacity: 0.5;")]
    [InlineData("transform: translateX(10px);")]
    [InlineData("text-shadow: 1px 1px 2px black;")]
    public void UnsupportedStylesKeepBlockLayout(string style)
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        var text = Text(p, "hello world");
        Update(root);
        Assert.NotNull(p.LayoutTree.InlineContext);

        text.Style.Set(style);
        Update(root);
        Assert.Null(p.LayoutTree.InlineContext);
        Assert.Null(text.LayoutTree.InlineContext);
        Assert.Empty(text.LayoutTree.Node.InlineFragments);
        Assert.True(text.Box.Rect.Width > 0);
    }

    [Fact]
    public void HidingOrChangingTheParagraphReleasesItsText()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        var text = Text(p, "hello world");
        Update(root);

        p.Style.Display = DisplayMode.None;
        Update(root);
        Assert.Null(p.LayoutTree.InlineContext);
        Assert.Null(text.LayoutTree.InlineContext);

        p.Style.Display = DisplayMode.Block;
        Update(root);
        Assert.Same(p.LayoutTree.InlineContext, text.LayoutTree.InlineContext);
        Assert.NotEmpty(text.LayoutTree.Node.InlineFragments);

        p.Style.Display = DisplayMode.Flex;
        Update(root);
        Assert.Null(p.LayoutTree.InlineContext);
        Assert.True(text.Box.Rect.Width > 0);
    }

    [Fact]
    public void SpansCanLeaveAndRejoinParagraphs()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        Text(p, "before ");
        var span = Span(p);
        var text = Text(span, "nested");
        Update(root);
        Assert.Same(p.LayoutTree, span.LayoutTree.InlineContext!.Root);

        span.Style.Display = DisplayMode.Block;
        Update(root);
        Assert.Null(p.LayoutTree.InlineContext);
        Assert.True(span.LayoutTree.HasInlineContent);
        span.SelectAllInChildren();
        Assert.Equal("nested", text.GetClipboardValue(false));

        span.Style.Display = DisplayMode.Inline;
        Update(root);
        Assert.True(span.LayoutTree.IsInlineParticipant);
        Assert.Null(span.LayoutTree.Node.InlineContent);
        p.SelectAllInChildren();
        Assert.Equal("before nested", text.GetClipboardValue(false));

        var destination = Paragraph(root);
        Text(destination, "new ");
        span.Parent = destination;
        Update(root);
        Assert.Equal("before", p.LayoutTree.InlineContext!.Text.Text);
        Assert.Same(destination.LayoutTree.InlineContext, text.LayoutTree.InlineContext);
        destination.SelectAllInChildren();
        Assert.Equal("new nested", text.GetClipboardValue(false));
    }

    [Fact]
    public void DeletedSpansLeaveTheParagraph()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        Text(p, "before ");
        var deleted = Text(p, "removed ");
        var tail = Text(p, "after");
        Update(root);

        deleted.Delete(true);
        Update(root);
        Assert.Equal("before after", p.LayoutTree.InlineContext!.Text.Text);
        Assert.Null(deleted.LayoutTree.InlineContext);
        Assert.NotEmpty(tail.LayoutTree.Node.InlineFragments);
        p.SelectAllInChildren();
        Assert.Equal("before after", p.GetClipboardValue(false));
    }

    [Fact]
    public void ControlsAndBlocksKeepBlockLayout()
    {
        var root = Root(Styles);
        foreach (var child in new Panel[] { new Image(), new TextEntry(), new Button() })
        {
            var p = Paragraph(root);
            var text = Text(p, "hello");
            child.Parent = p;
            child.Style.Display = DisplayMode.Inline;
            Update(root);
            Assert.Null(p.LayoutTree.InlineContext);
            Assert.Null(text.LayoutTree.InlineContext);
        }

        var mixed = Paragraph(root);
        Text(mixed, "inline");
        mixed.AddChild<Panel>().Style.Set("display: block; height: 20px;");
        Update(root);
        Assert.Null(mixed.LayoutTree.InlineContext);
    }

    [Fact]
    public void LayoutIsReusedUntilTheTextOrWidthChanges()
    {
        var root = Root(Styles);
        var p = Paragraph(root);
        var text = Text(p, "several words wrapping across multiple lines");
        Update(root);
        Update(root);

        var paragraph = p.LayoutTree.InlineContext!;
        var layout = paragraph.Layout(180);
        paragraph.Measure(float.NaN, false);
        paragraph.SetSelection(0, 7);
        Assert.Same(layout, paragraph.Layout(180));
        Assert.Equal("several", paragraph.SelectedText);

        text.Text += " more words";
        Update(root);
        var changed = paragraph.Layout(180);
        Assert.NotSame(layout, changed);
        Assert.False(paragraph.Text.ShouldDrawSelection);

        p.Style.Width = 400;
        Update(root);
        Assert.True(paragraph.Layout(400).Size.Height < changed.Size.Height);
    }

    [Fact]
    public void LargerFontsMoveTheBaseline()
    {
        var root = Root(Styles);
        var p = Paragraph(root, 400);
        Text(p, "hello");
        Update(root);
        var before = p.LayoutTree.InlineContext!.Layout(400);

        p.Style.FontSize = 40;
        Update(root);
        var after = p.LayoutTree.InlineContext.Layout(400);
        Assert.True(after.Baseline > before.Baseline);
        Assert.True(after.Size.Height > before.Size.Height);
    }
}
