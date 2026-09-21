using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class ScrollTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .item { width: 100px; height: 50px; flex-shrink: 0; }
        .list { width: 100px; height: 100px; flex-direction: column; overflow: scroll; flex-shrink: 0; }
        .outer { width: 100px; height: 200px; flex-direction: column; overflow: scroll; }
        .contain { overscroll-behavior: contain; }
        .bars { scrollbar-width: auto; }
        .stable { scrollbar-gutter: stable; }
        .wide { width: 100%; height: 10px; flex-shrink: 0; }
        .tail::after { content: 'end'; }
        """;

    private static void Update(RootPanel root, int frames = 1)
    {
        for (int i = 0; i < frames; i++)
        {
            root.Update(new Rect(0, 0, 1920, 1080), 0.016f);
        }
    }

    private static Panel List(Panel parent, int items, string classes = "list")
    {
        var list = parent.AddChild<Panel>(classes);
        for (int i = 0; i < items; i++)
        {
            list.AddChild<Panel>("item");
        }

        return list;
    }

    [Fact]
    public void ScrollToStaysWithinTheContent()
    {
        var root = Root(Styles);
        var list = List(root, 6);
        Update(root);

        list.ScrollTo(new Vector2(0, 1000));
        Assert.Equal(new Vector2(0, 200), list.ScrollOffset);

        list.ScrollTo(new Vector2(0, -50));
        Assert.Equal(Vector2.Zero, list.ScrollOffset);
    }

    [Fact]
    public void PreferScrollToBottomFollowsNewContent()
    {
        var root = Root(Styles);
        var list = List(root, 6);
        list.PreferScrollToBottom = true;
        Update(root, 3);
        Assert.Equal(200, list.ScrollOffset.Y);

        list.AddChild<Panel>("item");
        list.AddChild<Panel>("item");
        Update(root, 3);
        Assert.Equal(300, list.ScrollOffset.Y);
        Assert.True(list.IsScrollAtBottom);
    }

    [Fact]
    public void TheWheelMovesToTheOuterListAtTheEndOfTheInnerOne()
    {
        var root = Root(Styles);
        var outer = List(root, 0, "outer");
        var inner = List(outer, 3);
        for (int i = 0; i < 6; i++)
        {
            outer.AddChild<Panel>("item");
        }

        Update(root, 2);
        inner.ScrollTo(new Vector2(0, inner.ScrollSize.Y));

        Assert.False(inner.TryScroll(new Vector2(0, 1)));
        Assert.True(outer.TryScroll(new Vector2(0, 1)));
    }

    [Fact]
    public void OverscrollContainKeepsTheWheelInTheInnerList()
    {
        var root = Root(Styles);
        var outer = List(root, 0, "outer");
        var inner = List(outer, 3, "list contain");
        for (int i = 0; i < 6; i++)
        {
            outer.AddChild<Panel>("item");
        }

        Update(root, 2);
        inner.ScrollTo(new Vector2(0, inner.ScrollSize.Y));

        Assert.True(inner.TryScroll(new Vector2(0, 1)));
    }

    [Fact]
    public void ScrollbarsAppearOnlyWhenTheContentOverflows()
    {
        var root = Root(Styles);
        var fits = List(root, 1, "list bars");
        var overflows = List(root, 6, "list bars");
        var plain = List(root, 6);
        Update(root, 3);

        Assert.Empty(fits.ChildrenOfType<ScrollBar>());
        Assert.Empty(plain.ChildrenOfType<ScrollBar>());
        var bar = Assert.Single(overflows.ChildrenOfType<ScrollBar>());
        Assert.True(bar.IsVertical);
        Assert.Same(bar, overflows.Children[^1]);
        Assert.Equal(overflows.Box.ClipRect.Right, bar.Box.Rect.Right);
    }

    [Fact]
    public void AScrollbarStaysOutOfTheContentOrder()
    {
        var root = Root(Styles);
        var list = List(root, 6, "list bars tail");
        Update(root, 3);

        var bar = Assert.Single(list.ChildrenOfType<ScrollBar>());
        var after = list.Children[^2];
        Assert.Same(bar, list.Children[^1]);
        Assert.True(after.PseudoClass.HasFlag(PseudoClass.After));
        Assert.True(after.PseudoClass.HasFlag(PseudoClass.LastChild));
        Assert.False(bar.PseudoClass.HasFlag(PseudoClass.LastChild));
    }

    [Theory]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, true)]
    public void PositiveWheelValuesScrollRight(float x, float y, bool shift)
    {
        var root = Root(Styles);
        var row = root.AddChild<Panel>();
        row.Style.Set("width: 100px; height: 60px; flex-direction: row; overflow-x: scroll; flex-shrink: 0;");
        for (int i = 0; i < 6; i++)
        {
            row.AddChild<Panel>("item");
        }

        Update(root, 2);
        root.SetMousePosition(new Vector2(50, 30));
        root.AddMouseWheel(new Vector2(x, y), shift ? KeyboardModifiers.Shift : KeyboardModifiers.None);
        Update(root, 30);

        Assert.True(row.ScrollOffset.X > 0);
    }

    [Fact]
    public void WideContentScrollsClearOfTheGutter()
    {
        var root = Root(Styles);
        var plain = root.AddChild<Panel>("list bars");
        var stable = root.AddChild<Panel>("list bars stable");
        foreach (var list in (Panel[])[plain, stable])
        {
            list.AddChild<Panel>().Style.Set("width: 300px; height: 10px; flex-shrink: 0;");
        }

        Update(root, 3);
        plain.ScrollTo(new Vector2(1000, 0));
        stable.ScrollTo(new Vector2(1000, 0));

        var thickness = ScrollBar.Thickness(Length.Pixels(12), root.Scale);
        Assert.Equal(plain.ScrollOffset.X + thickness, stable.ScrollOffset.X, 0.5f);
    }

    [Fact]
    public void AStableGutterKeepsContentOutFromUnderTheScrollbar()
    {
        var root = Root(Styles);
        var plain = root.AddChild<Panel>("list bars");
        var stable = root.AddChild<Panel>("list bars stable");
        var plainChild = plain.AddChild<Panel>("wide");
        var stableChild = stable.AddChild<Panel>("wide");
        Update(root, 3);

        var thickness = ScrollBar.Thickness(Length.Pixels(12), root.Scale);
        Assert.Equal(plain.Box.Rect.Width, plainChild.Box.Rect.Width);
        Assert.Equal(stable.Box.Rect.Width - thickness, stableChild.Box.Rect.Width);
        Assert.Equal(stable.Box.Rect, stable.Box.ClipRect);
    }
}
