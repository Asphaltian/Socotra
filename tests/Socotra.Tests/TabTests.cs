using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class TabTests
{
    private static (RootPanel Root, TabPanel Pages) Build()
    {
        var root = Root();
        var pages = new TabPanel { Parent = root };
        return (root, pages);
    }

    private static void Frames(RootPanel root)
    {
        for (int i = 0; i < 3; i++)
        {
            Update(root);
        }
    }

    [Fact]
    public void TheFirstTabIsSelectedAndOnlyItsPageShows()
    {
        var (root, pages) = Build();
        var first = new Panel();
        var second = new Panel();
        var changes = new List<Tab?>();
        pages.TabBar.SelectionChanged += changes.Add;

        var a = pages.AddTab("A", first);
        var b = pages.AddTab("B", second);
        Frames(root);

        Assert.Same(a, pages.TabBar.SelectedTab);
        Assert.True(a.Selected);
        Assert.True(first.IsVisible);
        Assert.False(second.IsVisible);

        pages.TabBar.SelectTab(b);
        Frames(root);

        Assert.False(a.Selected);
        Assert.True(b.HasClass("selected"));
        Assert.False(first.IsVisible);
        Assert.True(second.IsVisible);
        Assert.Equal([a, b], changes);
        Assert.Throws<ArgumentException>(() => pages.TabBar.SelectTab(new Tab()));
    }

    [Fact]
    public void SwitchingAndReorderingKeepPageState()
    {
        var (root, pages) = Build();
        var entry = new TextEntry { Text = "unsaved" };
        var a = pages.AddTab("A", entry, canClose: true);
        var b = pages.AddTab("B", new Panel(), canClose: true);
        var moves = new List<(Tab, int)>();
        pages.TabBar.TabReordered += (tab, index) => moves.Add((tab, index));
        Frames(root);

        pages.TabBar.SelectTab(b);
        Frames(root);
        Assert.False(entry.IsVisible);

        pages.TabBar.MoveTab(a, 1);
        Assert.Same(b, pages.TabBar.SelectedTab);
        Assert.Equal([b, a], pages.TabBar.Tabs);
        Assert.Equal([(a, 1)], moves);

        pages.TabBar.SelectTab(a);
        Frames(root);
        Assert.True(entry.IsVisible);
        Assert.Equal("unsaved", entry.Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => pages.TabBar.MoveTab(a, 2));
    }

    [Fact]
    public void ClosingRespectsThePolicyAndSelectsTheNextTab()
    {
        var (_, pages) = Build();
        var locked = pages.AddTab("Locked", new Panel());
        var content = new Panel();
        var a = pages.AddTab("A", content, canClose: true);
        var b = pages.AddTab("B", new Panel(), canClose: true);
        var removed = new List<Tab>();
        pages.TabBar.TabRemoved += removed.Add;
        pages.TabBar.SelectTab(a);

        Assert.False(pages.TabBar.CloseTab(locked));
        pages.TabBar.CanCloseTab = _ => false;
        Assert.False(pages.TabBar.CloseTab(a));
        Assert.False(content.IsDeleted);

        pages.TabBar.CanCloseTab = null;
        Assert.True(pages.TabBar.CloseTab(a));
        Assert.True(content.IsDeleted);
        Assert.Same(b, pages.TabBar.SelectedTab);

        pages.TabBar.CloseTab(b);
        Assert.Same(locked, pages.TabBar.SelectedTab);
        pages.TabBar.RemoveTab(locked);
        Assert.Null(pages.TabBar.SelectedTab);
        Assert.Equal(0, pages.Body.ChildrenCount);
        Assert.Equal([a, b, locked], removed);
    }

    [Fact]
    public void TheCloseButtonClosesWithoutSelecting()
    {
        var (root, pages) = Build();
        var a = pages.AddTab("A", new Panel());
        var b = pages.AddTab("B", new Panel(), canClose: true);
        var changes = 0;
        pages.TabBar.SelectionChanged += _ => changes++;

        b.Children.OfType<Button>().Single().Click();
        Frames(root);

        Assert.Same(a, pages.TabBar.SelectedTab);
        Assert.Equal(0, changes);
        Assert.True(b.IsDeleted);
    }

    [Fact]
    public void ClickingAndMiddleClickingATab()
    {
        var (root, pages) = Build();
        var a = pages.AddTab("A", new Panel());
        var b = pages.AddTab("B", new Panel(), canClose: true);
        Frames(root);

        Click(root, b);
        Assert.Same(b, pages.TabBar.SelectedTab);

        Mouse(root, a, "onmiddleclick");
        Assert.False(a.IsDeleted);
        Mouse(root, b, "onmiddleclick");
        Assert.True(b.IsDeleted);
    }

    [Fact]
    public void ArrowKeysHomeAndEndMoveTheSelection()
    {
        var (root, pages) = Build();
        var a = pages.AddTab("A", new Panel());
        var b = pages.AddTab("B", new Panel());
        var c = pages.AddTab("C", new Panel());
        Frames(root);
        a.Focus();
        Update(root);

        Key(root, "right");
        Assert.Same(b, pages.TabBar.SelectedTab);
        Assert.Same(b, root.Focused);
        Key(root, "end");
        Assert.Same(c, pages.TabBar.SelectedTab);
        Key(root, "right");
        Assert.Same(a, pages.TabBar.SelectedTab);
        Key(root, "left");
        Assert.Same(c, pages.TabBar.SelectedTab);
        Key(root, "home");
        Assert.Same(a, pages.TabBar.SelectedTab);
    }

    [Fact]
    public void BadgesShowCountsAndNullHidesThem()
    {
        var (root, pages) = Build();
        var tab = pages.AddTab("Messages", new Panel(), "mail", count: 0);
        var badge = tab.Children.OfType<Label>().Single(label => label.HasClass("tab-count"));
        Frames(root);
        Assert.True(badge.IsVisible);
        Assert.Equal("0", badge.Text);

        tab.Count = 1234;
        Assert.Equal("1234", badge.Text);

        tab.Count = null;
        Frames(root);
        Assert.False(badge.IsVisible);
    }

    [Fact]
    public void NarrowTabsWithIconsHideTheirText()
    {
        var (root, pages) = Build();
        var bar = pages.TabBar;
        var a = pages.AddTab("Messages", new Panel(), "mail", count: 12);
        var b = pages.AddTab("History", new Panel(), "history");
        var plain = pages.AddTab("No icon", new Panel());
        bar.Style.Width = 240;
        Frames(root);

        Assert.True(a.IsIconOnly);
        Assert.True(b.IsIconOnly);
        Assert.False(plain.IsIconOnly);
        Assert.Equal("Messages", a.Tooltip);

        bar.Style.Width = 600;
        Frames(root);
        Assert.False(a.IsIconOnly);

        bar.Style.Width = 240;
        bar.AutoHideText = false;
        Frames(root);
        Assert.False(a.IsIconOnly);
    }

    [Fact]
    public void DraggingReordersOnlyWhenAllowed()
    {
        var (root, pages) = Build();
        var bar = pages.TabBar;
        var a = pages.AddTab("First tab", new Panel());
        var b = pages.AddTab("Second tab", new Panel());
        Frames(root);
        var start = a.Box.Rect.Center;
        var end = new Vector2(b.Box.Rect.Right - 1, b.Box.Rect.Center.Y);

        Drag(root, start, end);
        Assert.Same(a, bar.Tabs[0]);

        bar.AllowReorder = true;
        Assert.True(a.WantsDrag);
        root.SetMousePosition(start);
        root.SetMouseButton(MouseButtons.Left, true);
        Frames(root);
        root.SetMousePosition(end);
        Frames(root);
        Assert.True(a.HasClass("dragging"));
        Assert.True(b.HasClass("drop-after"));
        Key(root, "escape");
        root.SetMouseButton(MouseButtons.Left, false);
        Frames(root);
        Assert.Same(a, bar.Tabs[0]);

        Drag(root, start, end);
        Assert.Same(b, bar.Tabs[0]);
        Assert.Same(a, bar.SelectedTab);
        Assert.False(a.HasClass("dragging"));
    }

    private static void Drag(RootPanel root, Vector2 from, Vector2 to)
    {
        root.SetMousePosition(from);
        root.SetMouseButton(MouseButtons.Left, true);
        Frames(root);
        root.SetMousePosition(to);
        Frames(root);
        root.SetMouseButton(MouseButtons.Left, false);
        Frames(root);
    }

    [Fact]
    public void RightClickingOpensTheTabMenu()
    {
        var (root, pages) = Build();
        var tab = pages.AddTab("Scene", new Panel(), canClose: true);
        var duplicated = 0;
        tab.BuildContextMenu = menu => menu.AddOption("Duplicate", () => duplicated++);
        Frames(root);

        Mouse(root, tab, "onrightclick");
        var list = root.Children.OfType<Popup>().Single();
        var options = list.Children.OfType<Menu>().ToList();
        Assert.Equal(["Duplicate", "Close"], options.Select(option => option.Text));

        Click(root, options[0]);
        Assert.Equal(1, duplicated);
        Assert.True(list.IsDeleted);

        Mouse(root, tab, "onrightclick");
        Click(root, root.Children.OfType<Popup>().Single().Children.OfType<Menu>().Last());
        Assert.True(tab.IsDeleted);
    }

    [Fact]
    public void AddingContentThatCannotBeAPageThrows()
    {
        var (_, pages) = Build();

        Assert.Throws<ArgumentException>(() => pages.AddTab("Root", new RootPanel()));
        Assert.Throws<ArgumentException>(() => pages.AddTab("Self", pages));
        var inside = new Panel();
        pages.AddTab("Inside", inside);
        Assert.Throws<ArgumentException>(() => pages.AddTab("Again", inside));
    }
}
