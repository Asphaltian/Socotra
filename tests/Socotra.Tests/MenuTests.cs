using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public sealed class MenuTests : IDisposable
{
    private readonly (float Open, float Close, float Reopen) _delays = (Menu.SubmenuOpenDelay, Menu.SubmenuCloseDelay, Menu.ReopenGuard);

    public MenuTests()
    {
        Menu.SubmenuOpenDelay = 0;
        Menu.SubmenuCloseDelay = 0;
        Menu.ReopenGuard = 0;
    }

    public void Dispose() => (Menu.SubmenuOpenDelay, Menu.SubmenuCloseDelay, Menu.ReopenGuard) = _delays;

    private static (RootPanel Root, Panel Source) Build()
    {
        var root = Root();
        var source = Placed(root, 100, 100, 80, 20);
        Update(root);
        return (root, source);
    }

    private static void MenuKey(Menu menu, string button)
    {
        var list = menu.ListPanel!;
        var root = list.FindRootPanel()!;
        list.Focus();
        Update(root);
        Key(root, button);
    }

    [Fact]
    public void AddOptionKeepsOrderAndParent()
    {
        var menu = new Menu("File");
        var a = menu.AddOption("New", "note_add");
        var b = menu.AddOption("Open");
        var separator = menu.AddSeparator();
        var c = menu.AddMenu("Recent");

        Assert.Equal([a, b, separator, c], menu.Options);
        Assert.Same(menu, a.ParentMenu);
        Assert.Same(menu, c.RootMenu);
        Assert.True(separator.IsSeparator);
        Assert.Equal("note_add", a.Icon);
        Assert.True(menu.HasOptions);
        Assert.False(a.HasOptions);
        Assert.Same(b, menu.FindOption("Open"));
        Assert.Null(menu.FindOption("Nope"));
    }

    [Fact]
    public void RowPropertiesSetTheirClasses()
    {
        var option = new Menu("Save") { Shortcut = "Ctrl+S", Enabled = false };
        var toggle = new Menu().AddOption("Show Grid", _ => { });

        Assert.True(option.HasClass("has-shortcut"));
        Assert.True(option.Disabled);
        option.Shortcut = "";
        Assert.False(option.HasClass("has-shortcut"));
        Assert.True(toggle.Checkable);
        Assert.True(toggle.StaysOpen);
        Assert.False(toggle.Checked);
    }

    [Fact]
    public void OpenShowsTheOptionsInTheRootAndCloseKeepsThem()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        var option = menu.AddOption("New");
        var shown = 0;
        var closed = 0;
        menu.AboutToShow += _ => shown++;
        menu.Closed += _ => closed++;

        menu.Open(source, Popup.PositionMode.BelowLeft);

        Assert.True(menu.IsOpen);
        Assert.Equal(1, shown);
        Assert.Same(root, menu.ListPanel!.Parent);
        Assert.Same(menu.ListPanel, option.Parent);
        Assert.True(menu.HasClass("open"));

        menu.Close();

        Assert.False(menu.IsOpen);
        Assert.Equal(1, closed);
        Assert.Null(option.Parent);
        Assert.Equal([option], menu.Options);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.Same(menu.ListPanel, option.Parent);
    }

    [Fact]
    public void AnEmptyMenuDoesNotOpenButAboutToShowCanFillIt()
    {
        var (_, source) = Build();
        var menu = new Menu("Recent");
        var files = new[] { "a.txt", "b.txt" };

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.False(menu.IsOpen);

        menu.AboutToShow += m =>
        {
            m.Clear();
            foreach (var file in files)
            {
                m.AddOption(file);
            }
        };

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.Equal(2, menu.Options.Count);
        menu.Close();

        files = ["c.txt"];
        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.Equal("c.txt", Assert.Single(menu.Options).Text);
    }

    [Fact]
    public void ClosingAllPopupsClosesTheMenu()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        menu.AddOption("New");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        root.ClosePopups();

        Assert.False(menu.IsOpen);
        Assert.False(menu.HasClass("open"));
    }

    [Fact]
    public void ClickingAnOptionRunsItAndClosesTheWholeMenu()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        var clicks = 0;
        var inner = recent.AddOption("a.txt", () => clicks++);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Hover(root, recent);
        Assert.True(recent.IsOpen);

        Click(root, inner);

        Assert.Equal(1, clicks);
        Assert.False(recent.IsOpen);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void CheckableOptionsToggleAndStayOpen()
    {
        var (root, source) = Build();
        var menu = new Menu("View");
        bool? toggled = null;
        var grid = menu.AddOption("Show Grid", on => toggled = on);
        var command = menu.AddOption("Refresh", () => { });
        command.StaysOpen = true;
        var disabled = menu.AddOption("Save", () => toggled = null);
        disabled.Enabled = false;

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Click(root, grid);
        Assert.True(grid.Checked);
        Assert.True(toggled);
        Assert.True(menu.IsOpen);

        Click(root, grid);
        Assert.False(toggled);
        Click(root, command);
        Click(root, disabled);
        Assert.False(toggled);
        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void HoveringASubmenuRowOpensItAndHoveringAnotherClosesIt()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        var inner = recent.AddOption("a.txt");
        var save = menu.AddOption("Save");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Hover(root, recent);

        Assert.True(recent.IsOpen);
        Assert.Same(recent, recent.ListPanel!.PopupSource);
        Assert.Same(root, recent.ListPanel.Parent);
        Assert.Same(recent.ListPanel, inner.Parent);

        Hover(root, save);
        Assert.False(recent.IsOpen);
        Assert.True(menu.IsOpen);
    }

    [Fact]
    public void PressingInASubmenuKeepsItsParentsOpen()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        var inner = recent.AddOption("a.txt");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Hover(root, recent);
        root.ClosePopups(inner);

        Assert.True(menu.IsOpen);
        Assert.True(recent.IsOpen);

        menu.Close();
        Assert.False(recent.IsOpen);
    }

    [Fact]
    public void AMenuOpenedFromAPopupKeepsThatPopupOpen()
    {
        var (root, source) = Build();
        var popup = new Popup(source, Popup.PositionMode.BelowLeft, 0);
        var inside = new Panel { Parent = popup };
        var menu = new Menu("Layout");
        menu.AddOption("Square");

        menu.Open(inside, Popup.PositionMode.BelowLeft);
        root.ClosePopups(menu.ListPanel);
        Assert.True(menu.IsOpen);
        Assert.False(popup.IsDeleting);

        root.ClosePopups(inside);
        Assert.False(menu.IsOpen);
        Assert.False(popup.IsDeleting);
    }

    [Fact]
    public void TheMouseClicksOptionsAndPressingElsewhereCloses()
    {
        var (root, source) = Build();
        var menu = new Menu("File");
        var clicks = 0;
        var option = menu.AddOption("New", () => clicks++);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Update(root);
        Update(root);
        Press(root, option.Box.Rect.Center);
        Assert.Equal(1, clicks);
        Assert.False(menu.IsOpen);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Update(root);
        Press(root, new Vector2(1500, 900));
        Assert.False(menu.IsOpen);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ArrowsSkipSeparatorsAndDisabledRowsAndWrap()
    {
        var (_, source) = Build();
        var menu = new Menu("File");
        var a = menu.AddOption("A");
        menu.AddSeparator();
        menu.AddOption("Off").Enabled = false;
        var b = menu.AddOption("B");
        menu.AddWidget(new TextEntry());

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.Null(menu.Highlighted);

        MenuKey(menu, "down");
        Assert.Same(a, menu.Highlighted);
        Assert.True(a.HasClass("active"));
        MenuKey(menu, "down");
        Assert.Same(b, menu.Highlighted);
        Assert.False(a.HasClass("active"));
        MenuKey(menu, "down");
        Assert.Same(a, menu.Highlighted);
        MenuKey(menu, "up");
        Assert.Same(b, menu.Highlighted);
    }

    [Fact]
    public void EnterRunsTheHighlightedOptionAndLettersJumpToOptions()
    {
        var (_, source) = Build();
        var menu = new Menu("File");
        var clicks = 0;
        menu.AddOption("New", () => clicks++);
        var save = menu.AddOption("Save");
        var saveAs = menu.AddOption("Save As");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        MenuKey(menu, "s");
        Assert.Same(save, menu.Highlighted);
        MenuKey(menu, "s");
        Assert.Same(saveAs, menu.Highlighted);
        MenuKey(menu, "n");
        MenuKey(menu, "enter");

        Assert.Equal(1, clicks);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void RightOpensASubmenuAndLeftAndEscapeCloseOneLevel()
    {
        var (_, source) = Build();
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        var inner = recent.AddOption("a.txt");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        MenuKey(menu, "down");
        MenuKey(menu, "right");
        Assert.True(recent.IsOpen);
        Assert.Same(inner, recent.Highlighted);

        MenuKey(recent, "left");
        Assert.False(recent.IsOpen);
        Assert.True(menu.IsOpen);
        Assert.Same(recent, menu.Highlighted);

        MenuKey(menu, "right");
        Escape(recent.ListPanel!);
        Assert.False(recent.IsOpen);
        Escape(menu.ListPanel!);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void AnOpenMenuTakesTheKeyboard()
    {
        var (root, source) = Build();
        var below = Placed(root, 100, 300, 80, 20);
        below.AcceptsFocus = true;
        var menu = new Menu("File");
        var clicks = 0;
        var a = menu.AddOption("A");
        menu.AddOption("B", () => clicks++);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Update(root);
        Assert.Same(menu.ListPanel, root.Focused);

        Key(root, "down");
        Assert.Same(a, menu.Highlighted);
        Assert.Same(menu.ListPanel, root.Focused);
        Key(root, "down");
        Key(root, "enter");

        Assert.Equal(1, clicks);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void UnusedKeysGoOnToWhatOpenedTheMenu()
    {
        var (_, source) = Build();
        var menu = new Menu("File");
        menu.AddOption("A");
        menu.Open(source, Popup.PositionMode.BelowLeft);

        var e = new ButtonEvent("f5", true);
        menu.ListPanel!.OnButtonTyped(e);
        Assert.False(e.StopPropagation);

        e = new ButtonEvent("down", true);
        menu.ListPanel.OnButtonTyped(e);
        Assert.True(e.StopPropagation);
    }

    [Fact]
    public void WidgetsAndChildPanelsAreRowsButNotOptions()
    {
        var (_, source) = Build();
        var menu = new Menu("View");
        var entry = menu.AddWidget(new TextEntry());
        var child = new Menu("Child");
        var plain = new Panel();
        menu.AddChild(child);
        menu.AddChild(plain);

        Assert.Equal([entry, child, plain], menu.Rows);
        Assert.Equal([child], menu.Options);

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Assert.Same(menu.ListPanel, entry.Parent);
        Assert.Same(menu.ListPanel, plain.Parent);

        menu.Remove(plain);
        Assert.Null(plain.Parent);
        Assert.False(plain.IsDeleted);
        menu.Close();
        Assert.Null(entry.Parent);
        Assert.False(entry.IsDeleted);
    }

    [Fact]
    public void LeavingARowTakesItsHighlightUnlessItsSubmenuIsOpen()
    {
        Menu.SubmenuOpenDelay = 1;
        var (root, source) = Build();
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        recent.AddOption("a.txt");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Hover(root, recent);
        Assert.Same(recent, menu.Highlighted);
        Assert.False(recent.IsOpen);

        Mouse(root, recent, "onmouseout");
        Assert.Null(menu.Highlighted);

        Menu.SubmenuOpenDelay = 0;
        Hover(root, recent);
        Assert.True(recent.IsOpen);
        Mouse(root, recent, "onmouseout");
        Assert.Same(recent, menu.Highlighted);
    }

    [Fact]
    public void SubmenusFlipToTheLeftAtTheEdgeOfTheScreen()
    {
        var root = Root();
        var source = Placed(root, 1800, 100, 50, 20);
        Update(root);
        var menu = new Menu("File");
        var recent = menu.AddMenu("Recent");
        recent.AddOption("a.txt");

        menu.Open(source, Popup.PositionMode.BelowLeft);
        Update(root);
        Update(root);
        recent.Open();
        Update(root);
        Update(root);

        var row = recent.Box.Rect;
        var list = recent.ListPanel!.Box.Rect;
        Assert.True(list.Right <= 1920, $"on screen, right edge at {list.Right}");
        Assert.True(list.Right <= row.Left + 4, $"flipped to the left of the row: list {list} row {row}");
    }

    [Fact]
    public void MenuBarClicksHoversAndArrowsBetweenMenus()
    {
        var root = Root();
        var bar = new MenuBar { Parent = root };
        var file = bar.AddMenu("File");
        file.AddOption("New");
        bar.AddChild(new TextEntry());
        var edit = bar.AddMenu("Edit");
        edit.AddOption("Undo");
        var help = bar.AddMenu("Help");

        Assert.Equal([file, edit, help], bar.Menus);

        Hover(root, edit);
        Assert.False(edit.IsOpen);

        Click(root, file);
        Assert.True(file.IsOpen);
        Assert.Same(file, bar.OpenMenu);

        Hover(root, edit);
        Assert.False(file.IsOpen);
        Assert.True(edit.IsOpen);

        MenuKey(edit, "left");
        Assert.True(file.IsOpen);
        MenuKey(file, "right");
        Assert.True(edit.IsOpen);

        Click(root, edit);
        Assert.False(edit.IsOpen);
        Assert.Null(bar.OpenMenu);

        Click(root, help);
        Assert.Null(bar.OpenMenu);
    }

    [Fact]
    public void MenuListsAreStyledUnderWhatOpenedThem()
    {
        var root = Root();
        var bar = new MenuBar { Parent = root };
        bar.StyleSheet.Parse(".menubar { word-spacing: 3px; } .menubar .menulist { width: 222px; }");
        var file = bar.AddMenu("File");
        file.AddOption("New");

        Click(root, file);
        Update(root);

        var list = file.ListPanel!;
        Assert.Equal(Length.Pixels(222), list.ComputedStyle!.Width);
        Assert.Equal(Length.Pixels(3), list.ComputedStyle.WordSpacing);
    }

    [Fact]
    public void MenuBarEscapeAndRemovingTheHeadingCloseTheMenu()
    {
        var root = Root();
        var bar = new MenuBar { Parent = root };
        var file = bar.AddMenu("File");
        file.AddOption("New");

        Click(root, file);
        Escape(file.ListPanel!);
        Assert.False(file.IsOpen);
        Assert.Null(bar.OpenMenu);

        Click(root, file);
        file.Parent = null;
        Assert.False(file.IsOpen);
        Assert.Null(bar.OpenMenu);
    }

    [Fact]
    public void TheClickThatDismissedAMenuDoesNotReopenIt()
    {
        Menu.ReopenGuard = _delays.Reopen;
        var root = Root();
        var bar = new MenuBar { Parent = root };
        var file = bar.AddMenu("File");
        file.AddOption("New");

        Click(root, file);
        Assert.True(file.IsOpen);

        root.ClosePopups();
        Click(root, file);
        Assert.False(file.IsOpen);

        Click(root, file);
        Assert.True(file.IsOpen);

        file.Close();
        Click(root, file);
        Assert.True(file.IsOpen);
    }

    [Fact]
    public void DeletingAMenuClosesIt()
    {
        var (_, source) = Build();
        var menu = new Menu("File");
        menu.AddOption("New");
        menu.Open(source, Popup.PositionMode.BelowLeft);
        var list = menu.ListPanel!;

        menu.Delete(true);

        Assert.True(list.IsDeleted);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void MarkupOptionsFollowTheirItemsWhileOpenAndClosed()
    {
        var root = Root();
        var host = root.AddChild<Razor.FileMenu>();
        Update(root);
        var menu = host.Menu!;
        void Rebuild()
        {
            Update(root);
            Update(root);
        }

        void AssertOptions(params string[] texts)
        {
            Assert.Equal(texts, menu.Options.Select(option => option.Text));
            Assert.Equal(menu.Options, menu.Rows);
            Assert.All(menu.Options, option => Assert.False(option.IsDeleting || option.IsDeleted));
            Assert.All(menu.Options, option => Assert.Equal(menu.ListPanel, option.Parent));
        }

        menu.Open(menu, Popup.PositionMode.BelowLeft);
        AssertOptions("Recent", "New", "Open");
        Update(root);
        Hover(root, host.Recent!);
        Assert.True(host.Recent!.IsOpen);
        var recentClosed = 0;
        host.Recent.Closed += _ => recentClosed++;

        host.Items.Add("Save");
        Rebuild();
        AssertOptions("Recent", "New", "Open", "Save");
        Assert.Equal(0, recentClosed);

        host.Items.RemoveAt(0);
        Rebuild();
        AssertOptions("Recent", "Open", "Save");

        menu.Close();
        host.Items.Add("Quit");
        Rebuild();
        menu.Open(menu, Popup.PositionMode.BelowLeft);
        AssertOptions("Recent", "Open", "Save", "Quit");

        Update(root);
        Click(root, menu.Options[^1]);
        Assert.Equal("Quit", host.Picked);
        Assert.False(menu.IsOpen);
    }
}
