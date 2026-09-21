using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class StyleTests
{
    [Fact]
    public void RulesMatchClassesElementsAndIds()
    {
        var root = Root("""
            .menu { width: 400px; }
            panel.menu { height: 300px; }
            #title { opacity: 0.5; }
            """);
        var menu = root.AddChild<Panel>("menu");
        var title = menu.AddChild<Panel>();
        title.Id = "title";

        Update(root);

        Assert.Equal(Length.Pixels(400), menu.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(300), menu.ComputedStyle.Height);
        Assert.Equal(0.5f, title.ComputedStyle!.Opacity);
    }

    [Fact]
    public void SpecificityThenSourceOrderDecides()
    {
        var root = Root("""
            .a.b { width: 10px; }
            .a { width: 20px; height: 20px; }
            .a { height: 30px; }
            """);
        var panel = root.AddChild<Panel>("a b");

        Update(root);

        Assert.Equal(Length.Pixels(10), panel.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(30), panel.ComputedStyle.Height);
    }

    [Fact]
    public void ScssNestingVariablesAndMixinsExpand()
    {
        var root = Root("""
            $accent: #00ff00;
            $color-strong: blue;
            @mixin boxed($size, $color: red) {
                width: $size;
                border-color: $color;
                outline-color: $color-strong;
                &:hover { height: $size; }
                @content;
            }

            .list {
                // line comment
                .item { @include boxed(12px) { opacity: 0.25; } }
                > .direct { color: $accent; }
                &.wide { width: 500px; }
            }
            """);
        var list = root.AddChild<Panel>("list wide");
        var item = list.AddChild<Panel>().AddChild<Panel>("item");
        var direct = list.AddChild<Panel>("direct");
        var indirect = list.AddChild<Panel>().AddChild<Panel>("direct");

        Update(root);
        item.Switch(PseudoClass.Hover, true);
        Update(root);

        Assert.Equal(Length.Pixels(500), list.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(12), item.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(12), item.ComputedStyle.Height);
        Assert.Equal(Color.Parse("red"), item.ComputedStyle.BorderLeftColor);
        Assert.Equal(Color.Parse("blue"), item.ComputedStyle.OutlineColor);
        Assert.Equal(0.25f, item.ComputedStyle.Opacity);
        Assert.Equal(Color.Parse("#00ff00"), direct.ComputedStyle!.FontColor);
        Assert.Equal(Color.Black, indirect.ComputedStyle!.FontColor);
    }

    [Fact]
    public void PseudoClassesAndStructuralSelectorsMatch()
    {
        var root = Root("""
            .row:first-child { width: 1px; }
            .row:last-child { width: 2px; }
            .row:nth-child(2) { width: 3px; }
            .row:not(.skip):hover { height: 4px; }
            .list:has(.selected) { opacity: 0.5; }
            .row + .row { min-width: 5px; }
            """);
        var list = root.AddChild<Panel>("list");
        var first = list.AddChild<Panel>("row");
        var second = list.AddChild<Panel>("row selected");
        var third = list.AddChild<Panel>("row skip");

        second.Switch(PseudoClass.Hover, true);
        third.Switch(PseudoClass.Hover, true);
        Update(root);

        Assert.Equal(Length.Pixels(1), first.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(3), second.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(2), third.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(4), second.ComputedStyle.Height);
        Assert.NotEqual(Length.Pixels(4), third.ComputedStyle.Height);
        Assert.Equal(0.5f, list.ComputedStyle!.Opacity);
        Assert.Equal(Length.Pixels(0), first.ComputedStyle.MinWidth);
        Assert.Equal(Length.Pixels(5), second.ComputedStyle.MinWidth);
    }

    [Fact]
    public void OrderMovesChildrenInTheLayout()
    {
        var root = Root("""
            .row { flex-direction: row; }
            .item { width: 10px; height: 10px; flex-shrink: 0; }
            .first { order: -1; }
            """);
        var row = root.AddChild<Panel>("row");
        var a = row.AddChild<Panel>("item");
        var b = row.AddChild<Panel>("item first");
        Update(root);
        Assert.True(b.Box.Rect.Left < a.Box.Rect.Left);

        var c = row.AddChild<Panel>("item");
        Update(root);
        Assert.True(b.Box.Rect.Left < a.Box.Rect.Left && a.Box.Rect.Left < c.Box.Rect.Left);

        b.RemoveClass("first");
        Update(root);
        Assert.True(a.Box.Rect.Left < b.Box.Rect.Left && b.Box.Rect.Left < c.Box.Rect.Left);
    }

    [Fact]
    public void HasKeepsTheCombinatorsInsideIt()
    {
        var root = Root("""
            .list:has(.row > .cell) { opacity: 0.5; }
            .list:has(> .row .cell) { width: 3px; }
            """);
        var direct = root.AddChild<Panel>("list");
        direct.AddChild<Panel>("row").AddChild<Panel>("cell");
        var nested = root.AddChild<Panel>("list");
        nested.AddChild<Panel>("row").AddChild<Panel>().AddChild<Panel>("cell");
        var deeper = root.AddChild<Panel>("list");
        deeper.AddChild<Panel>().AddChild<Panel>("row").AddChild<Panel>("cell");
        Update(root);

        Assert.Equal(0.5f, direct.ComputedStyle!.Opacity);
        Assert.Equal(1, nested.ComputedStyle!.Opacity);
        Assert.Equal(0.5f, deeper.ComputedStyle!.Opacity);
        Assert.Equal(Length.Pixels(3), direct.ComputedStyle.Width);
        Assert.Equal(Length.Pixels(3), nested.ComputedStyle.Width);
        Assert.NotEqual(Length.Pixels(3), deeper.ComputedStyle.Width);
    }

    [Fact]
    public void ParsingKeepsTheSheetsYouAdded()
    {
        var root = new RootPanel();
        root.StyleSheet.Add(StyleSheet.FromString(".a { width: 5px; }"));
        root.StyleSheet.Parse(".a { height: 5px; }");
        var panel = root.AddChild<Panel>("a");
        Update(root);

        Assert.Equal(Length.Pixels(5), panel.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(5), panel.ComputedStyle.Height);
    }

    [Fact]
    public void EverySelectorFeatureMatches()
    {
        var root = Root("""
            * { min-height: 1px; }
            #Main { width: 10px; }
            .item:nth-child(odd) { height: 1px; }
            .item:nth-child(even) { height: 2px; }
            .item:only-child { height: 3px; }
            .box:empty { opacity: 0.5; }
            .box:has(> .flag) { opacity: 0.25; }
            .item:has(+ .flag) { max-width: 7px; }
            .item ~ .flag { max-height: 8px; }
            .item:focus-visible { margin-left: 1px; }
            .item:disabled { margin-top: 2px; }
            .box > .item:first-child:not(:last-child) { padding-left: 3px; }
            panel.item:hover:active:focus { padding-top: 4px; }
            """);
        var box = root.AddChild<Panel>("box");
        box.Id = "main";
        var first = box.AddChild<Panel>("item");
        var second = box.AddChild<Panel>("item");
        var flag = box.AddChild<Panel>("flag");
        var empty = root.AddChild<Panel>("box");
        var lonely = empty.AddChild<Panel>().AddChild<Panel>("item");
        first.Switch(PseudoClass.FocusVisible, true);
        second.Disabled = true;
        second.Switch(PseudoClass.Hover | PseudoClass.Active | PseudoClass.Focus, true);
        Update(root);

        Assert.Equal(Length.Pixels(10), box.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(1), flag.ComputedStyle!.MinHeight);
        Assert.Equal(Length.Pixels(1), first.ComputedStyle!.Height);
        Assert.Equal(Length.Pixels(2), second.ComputedStyle!.Height);
        Assert.Equal(Length.Pixels(3), lonely.ComputedStyle!.Height);
        Assert.Equal(0.25f, box.ComputedStyle.Opacity);
        Assert.Equal(1, empty.ComputedStyle!.Opacity);
        Assert.Equal(Length.Pixels(7), second.ComputedStyle.MaxWidth);
        Assert.Equal(Length.Undefined, first.ComputedStyle.MaxWidth);
        Assert.Equal(Length.Pixels(8), flag.ComputedStyle.MaxHeight);
        Assert.Equal(Length.Pixels(1), first.ComputedStyle.MarginLeft);
        Assert.Equal(Length.Pixels(2), second.ComputedStyle.MarginTop);
        Assert.Equal(Length.Pixels(3), first.ComputedStyle.PaddingLeft);
        Assert.Equal(Length.Pixels(0), lonely.ComputedStyle.PaddingLeft);
        Assert.Equal(Length.Pixels(4), second.ComputedStyle.PaddingTop);
    }

    [Fact]
    public void ParsingAgainReplacesTheLastParsedSheet()
    {
        var root = Root(".a { width: 1px; }");
        root.StyleSheet.Parse(".a { height: 2px; }");
        var panel = root.AddChild<Panel>("a");

        Update(root);

        Assert.Equal(Length.Undefined, panel.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(2), panel.ComputedStyle.Height);
    }

    [Fact]
    public void ChildSheetsCanUseTheirAncestorsVariables()
    {
        var root = Root("$size: 12px;");
        var panel = root.AddChild<Panel>("a");
        panel.StyleSheet.Parse(".a { width: $size; }");

        Update(root);

        Assert.Equal(Length.Pixels(12), panel.ComputedStyle!.Width);
        Assert.Equal("12px", root.StyleSheet.Sheets[0].GetVariable("$size"));
    }

    [Fact]
    public void InlineStylesheetsLoadForTheirPanelType()
    {
        var root = new RootPanel();
        var panel = root.AddChild<InlineStyled>();

        Update(root);

        Assert.Equal(Length.Pixels(33), panel.ComputedStyle!.Width);
    }

    [StyleSheet.Inline("style-tests-inline", "inlinestyled { width: 33px; }")]
    private sealed class InlineStyled : Panel
    {
    }

    [Fact]
    public void InheritedPropertiesCascadeAndCurrentColorResolves()
    {
        var root = Root("""
            .parent { color: blue; font-size: 20px; border-width: 1px; }
            .child { border-color: currentColor; }
            """);
        var parent = root.AddChild<Panel>("parent");
        var child = parent.AddChild<Panel>("child");

        Update(root);

        Assert.Equal(Color.Parse("blue"), child.ComputedStyle!.FontColor);
        Assert.Equal(Length.Pixels(20), child.ComputedStyle.FontSize);
        Assert.Equal(Color.Parse("blue"), child.ComputedStyle.BorderTopColor);
        Assert.Equal(Color.White, parent.ComputedStyle!.BorderTopColor);
    }

    [Fact]
    public void MalformedRulesAreSkippedWithoutLosingTheRest()
    {
        var root = Root("""
            .broken:nonsense { width: 1px; }
            .fine { width: 2px; }
            """);
        var panel = root.AddChild<Panel>("fine");

        Update(root);

        Assert.Equal(Length.Pixels(2), panel.ComputedStyle!.Width);
    }

    [Fact]
    public void FlexLayoutPlacesChildrenAtTheRootScale()
    {
        var root = Root("""
            .bar { flex-direction: row; width: 300px; height: 100px; padding: 10px; gap: 20px; }
            .cell { flex-grow: 1; }
            """);
        var bar = root.AddChild<Panel>("bar");
        var a = bar.AddChild<Panel>("cell");
        var b = bar.AddChild<Panel>("cell");

        root.Update(new Rect(0, 0, 3840, 2160), 0);

        Assert.Equal(2, root.Scale);
        Assert.Equal(new Rect(0, 0, 600, 200), bar.Box.Rect);
        Assert.Equal(new Rect(20, 20, 260, 160), a.Box.Rect);
        Assert.Equal(new Rect(320, 20, 260, 160), b.Box.Rect);
    }

    [Fact]
    public void TransitionsInterpolateOverTime()
    {
        var root = Root("""
            .fade { opacity: 1; transition: opacity 1s linear; }
            .fade.hidden { opacity: 0; }
            """);
        var panel = root.AddChild<Panel>("fade");
        Update(root, 0);
        Update(root, 0);

        panel.AddClass("hidden");
        Update(root, 0);
        Update(root, 0.25f);

        Assert.Equal(0.75f, panel.ComputedStyle!.Opacity!.Value, 0.01f);

        Update(root, 1);

        Assert.Equal(0, panel.ComputedStyle.Opacity!.Value, 0.001f);
        Assert.False(panel.HasActiveTransitions);
    }

    [Fact]
    public void DeletedPanelsWaitForTheirOutro()
    {
        var root = Root(".item { opacity: 1; transition: opacity 0.5s linear; } .item:outro { opacity: 0; }");
        var panel = root.AddChild<Panel>("item");
        Update(root, 0);
        Update(root, 0);

        panel.Delete();
        Update(root, 0.1f);

        Assert.False(panel.IsDeleted);
        Assert.True(panel.HasOutro);

        Update(root, 1);
        Update(root, 0);

        Assert.True(panel.IsDeleted);
        Assert.Empty(root.Children);
    }
}
