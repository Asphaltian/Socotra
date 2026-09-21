namespace Socotra.Tests;

public class DockHostTests
{
    private readonly RootPanel _root = new UnscaledRoot();
    private readonly DockHost _host;

    public DockHostTests()
    {
        Fonts.Load("Data/Fonts/Lato-Regular.ttf");
        _root.StyleSheet.Parse(".dockhost { font-family: Lato; } .tab { padding: 4px 8px; }");
        _host = new DockHost { Parent = _root };
    }

    private void Frame()
    {
        for (int i = 0; i < 3; i++)
        {
            _root.Update(new Rect(0, 0, 800, 600), 0.016f);
        }
    }

    private void MoveTo(Vector2 position)
    {
        _root.SetMousePosition(position);
        Frame();
    }

    private void MouseButton(bool down)
    {
        _root.SetMouseButton(MouseButtons.Left, down);
        Frame();
    }

    private static Panel Tab(DockHost owner, string title) => owner.Descendants.OfType<Label>()
        .Single(x => x.HasClass("dock-tab-title") && x.Text == title).Parent!;

    private static Vector2 TitleCenter(Panel tab) => tab.Children.OfType<Label>().Single(x => x.HasClass("dock-tab-title")).Box.Rect.Center;

    private Panel Single(string className) => _host.Descendants.Single(x => x.HasClass(className));

    private void Key(string button)
    {
        _root.AddButtonEvent(new ButtonEvent(button, true));
        _root.AddButtonEvent(new ButtonEvent(button, false));
        Frame();
    }

    private void BeginDrag(Panel tab, Vector2 destination)
    {
        Assert.True(tab.Children.OfType<Label>().Single(x => x.HasClass("dock-tab-title")).Box.Rect.Width > 0);
        MoveTo(TitleCenter(tab));
        Assert.Same(tab, _root.Hovered);
        MouseButton(true);
        Assert.Same(tab, _root.Focused);
        MoveTo(destination);
        Assert.True(tab.HasClass("dragging"));
        Assert.True(Single("dock-targets").IsVisible);
    }

    private void AssertKept(DockItem[] items, Panel[] tabs)
    {
        for (int i = 0; i < items.Length; i++)
        {
            Assert.Same(items[i], _host.Find(items[i].Id));
            Assert.Contains(_host, items[i].Content!.Ancestors);
            Assert.Equal(0, ((RecordingPanel)items[i].Content!).DeletedCount);
            Assert.Same(tabs[i], Tab(_host, items[i].Title));
        }
    }

    private DockItem[] OpenThree()
    {
        var items = ((string[])["a", "b", "c"]).Select(id => _host.Register(id, id, new RecordingPanel())).ToArray();
        foreach (var item in items)
        {
            _host.Dock(item.Id);
        }

        Frame();
        return items;
    }

    [Fact]
    public void FactoriesOnlyMakeContentWhenOpened()
    {
        int calls = 0;
        var item = _host.Register("lazy", "Lazy", () =>
        {
            calls++;
            return new Panel();
        });
        var closed = _host.State;
        Frame();
        Assert.Equal(0, calls);
        Assert.Null(item.Content);
        Assert.True(_host.RestoreState(closed));
        Assert.Equal(0, calls);

        _host.Dock("lazy");
        var content = item.Content;
        var opened = _host.State;
        _host.Close("lazy");
        Assert.True(_host.RestoreState(opened));
        Assert.Equal(1, calls);
        Assert.Same(content, item.Content);
    }

    [Fact]
    public void AFailingFactoryLeavesTheLayoutAlone()
    {
        _host.Register("bad", "Bad", () => throw new InvalidOperationException("Factory failure"));
        var before = _host.State;
        Assert.Throws<InvalidOperationException>(() => _host.Dock("bad"));
        Assert.Equal(before, _host.State);
    }

    [Fact]
    public void LayoutEditsKeepTheSameContent()
    {
        var items = OpenThree();
        var containers = items.Select(x => x.Content!.Parent).ToArray();
        var entry = new TextEntry { Parent = items[0].Content, Text = "unsaved edit" };
        Frame();

        Assert.False(items[0].Content!.IsVisible);
        Assert.True(items[2].Content!.IsVisible);
        Assert.True(_host.Activate("a"));
        Frame();
        Assert.True(items[0].Content!.IsVisible);
        Assert.False(items[2].Content!.IsVisible);

        _host.Dock("b", "a", DockPosition.Right, 0.3f);
        _host.Dock("c", "b", DockPosition.Bottom, 0.4f);
        Frame();
        var saved = _host.State;
        Assert.Equal(3, _host.Descendants.Count(x => x.HasClass("dock-group")));
        Assert.All(items, x => Assert.True(x.Content!.IsVisible));
        Assert.All(_host.Descendants.Where(x => x.HasClass("dock-group")), x => Assert.True(x.HasClass("single-tab")));

        foreach (var item in items)
        {
            Assert.True(_host.Close(item.Id));
        }

        Frame();
        Assert.Equal(0, _host.Descendants.Count(x => x.HasClass("dock-group")));
        Assert.All(items, x => Assert.False(x.Content!.IsVisible || _host.IsOpen(x.Id)));
        Assert.True(_host.Descendants.Single(x => x.HasClass("dock-empty")).IsVisible);

        _host.Dock("a");
        _host.Dock("b", "a");
        var twoTabs = _host.State;
        Frame();
        Assert.True(_host.RestoreState(saved));
        Frame();
        Assert.Equal(saved, _host.State);
        Assert.All(items, x => Assert.True(x.Content!.IsVisible));

        Assert.True(_host.RestoreState(twoTabs));
        Frame();
        Assert.False(_host.IsOpen("c"));
        Assert.False(items[2].Content!.IsVisible);
        Assert.Equal(containers, items.Select(x => x.Content!.Parent));
        Assert.All(items, x => Assert.Equal(0, ((RecordingPanel)x.Content!).DeletedCount));
        Assert.Same(items[0].Content, entry.Parent);
        Assert.Equal("unsaved edit", entry.Text);
    }

    [Fact]
    public void DeletingTheHostDeletesAllContentOnce()
    {
        var items = OpenThree();
        var neverOpened = new RecordingPanel();
        _host.Register("never", "never", neverOpened);
        var child = new RecordingPanel { Parent = items[0].Content };
        _host.Close("a");
        _host.Close("b");
        Frame();

        _host.Delete(true);
        Frame();

        Assert.Throws<InvalidOperationException>(() => _host.Dock("a"));
        foreach (var content in items.Select(x => (RecordingPanel)x.Content!).Append(neverOpened).Append(child))
        {
            Assert.True(content.IsDeleted);
            Assert.Equal(1, content.DeletedCount);
        }
    }

    [Fact]
    public void RegisteringRefusesDuplicatesAndCycles()
    {
        var content = new RecordingPanel();
        var item = _host.Register("a", "a", content);
        var spare = new RecordingPanel { Parent = _root };
        var parent = content.Parent;
        var other = new DockHost { Parent = _root };

        Assert.Throws<ArgumentException>(() => _host.Register("a", "duplicate", spare));
        Assert.Throws<ArgumentException>(() => _host.Register("alias", "alias", content));
        Assert.Throws<ArgumentException>(() => other.Register("alias", "alias", content));
        Assert.Throws<ArgumentException>(() => _host.Register("self", "self", _host));
        Assert.Throws<ArgumentException>(() => _host.Register("root", "root", _root));
        Assert.Throws<ArgumentException>(() => _host.Register(" ", "blank", new Panel()));
        Assert.Same(item, _host.Find("a"));
        Assert.Same(parent, content.Parent);
        Assert.Same(_root, spare.Parent);
        Assert.Single(_host.Items);
        Assert.Empty(other.Items);
        Assert.False(_host.IsOpen("a"));
    }

    [Fact]
    public void UnknownIdsAndBadStatesChangeNothing()
    {
        OpenThree();
        var before = _host.State;
        int changes = 0;
        _host.LayoutChanged += () => changes++;

        Assert.Null(_host.Find("missing"));
        Assert.Null(_host.Find(null));
        Assert.False(_host.IsOpen("missing"));
        Assert.False(_host.Close("missing"));
        Assert.False(_host.Activate("missing"));
        Assert.Throws<ArgumentException>(() => _host.Dock("missing"));
        Assert.Throws<ArgumentException>(() => _host.Dock("a", "missing", DockPosition.Left));
        Assert.Throws<ArgumentOutOfRangeException>(() => _host.Dock("a", "b", DockPosition.Left, 0.99f));
        foreach (var json in (string[])[
            "not json",
            """{"version":1,"root":{"type":"group","tabs":["a","missing"],"activeId":"a"}}""",
            """{"version":1,"root":{"type":"group","tabs":["a","a"],"activeId":"a"}}""",
            """{"version":2,"root":null}""",
        ])
        {
            Assert.False(_host.RestoreState(json));
            Assert.Equal(before, _host.State);
        }

        Assert.Equal(0, changes);
    }

    [Fact]
    public void PanelsThatCantCloseStayOpen()
    {
        var pinned = _host.Register("pinned", "pinned", new RecordingPanel(), canClose: false);
        _host.Register("optional", "optional", new RecordingPanel());
        _host.Dock("optional");
        var withoutPinned = _host.State;
        Assert.True(_host.RestoreState(withoutPinned));
        Assert.False(_host.IsOpen("pinned"));

        _host.Dock("pinned");
        Frame();
        var before = _host.State;
        Assert.False(_host.Close("pinned"));
        Assert.False(_host.RestoreState(withoutPinned));
        Assert.Equal(before, _host.State);
        Assert.True(_host.Close("optional"));

        _host.State = """{"version":1,"root":{"type":"group","tabs":["pinned"],"activeId":"pinned"}}""";
        Frame();
        Assert.True(pinned.Content!.IsVisible);
        Assert.False(pinned.CanClose);
    }

    [Fact]
    public void StateSavesTabOrderSelectionAndSplits()
    {
        OpenThree();
        _host.Dock("c", "a", DockPosition.Left, 0.25f);
        _host.Dock("b", "a", DockPosition.Center, tabIndex: 0);
        Assert.Contains("\"type\":\"split\"", _host.State);
        Assert.Contains("\"fraction\":0.25", _host.State);
        Assert.Contains("\"tabs\":[\"b\",\"a\"]", _host.State);
        Assert.Contains("\"activeId\":\"b\"", _host.State);

        var other = new DockHost { Parent = _root };
        foreach (var id in (string[])["a", "b", "c"])
        {
            other.Register(id, id, new Panel());
        }

        Assert.True(other.RestoreState(_host.State));
        Assert.Equal(_host.State, other.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplitterDragsKeepTheMinimumSizes(bool vertical)
    {
        OpenThree();
        var edge = vertical ? DockPosition.Bottom : DockPosition.Right;
        _host.Dock("b", "a", edge);
        _host.Dock("c", "a", edge);
        Frame();
        var split = _host.Descendants.First(x => x.HasClass("dock-split"));
        var handle = split.Children.Single(x => x.HasClass("dock-splitter"));
        var branches = split.Children.Where(x => x.HasClass("dock-branch")).ToArray();
        var bounds = split.Box.Rect;
        var minimum = vertical ? 80 : 120;

        MoveTo(handle.Box.Rect.Center);
        Assert.Same(handle, _root.Hovered);
        MouseButton(true);
        MoveTo(vertical ? new Vector2(bounds.Center.X, bounds.Top + 1) : new Vector2(bounds.Left + 1, bounds.Center.Y));
        Assert.True(split.HasClass("resizing"));
        Assert.Equal((minimum * 2) + 5, Size(branches[0]), 1.0f);
        Assert.True(Size(branches[1]) >= minimum - 1);

        MoveTo(vertical ? new Vector2(bounds.Center.X, bounds.Bottom - 1) : new Vector2(bounds.Right - 1, bounds.Center.Y));
        Assert.Equal(minimum, Size(branches[1]), 1.0f);
        Assert.Equal(Size(split) - 5, Size(branches[0]) + Size(branches[1]), 1.0f);
        MouseButton(false);
        Assert.False(split.HasClass("resizing"));

        var saved = _host.State;
        MoveTo(bounds.Center);
        Assert.Equal(saved, _host.State);

        float Size(Panel panel) => vertical ? panel.Box.Rect.Height : panel.Box.Rect.Width;
    }

    [Fact]
    public void EscapePutsTheSplitterBack()
    {
        OpenThree();
        _host.Dock("b", "a", DockPosition.Right, 0.35f);
        Frame();
        var split = _host.Descendants.Single(x => x.HasClass("dock-split"));
        var handle = split.Children.Single(x => x.HasClass("dock-splitter"));
        var before = _host.State;
        var start = handle.Box.Rect.Center;

        MoveTo(start);
        MouseButton(true);
        MoveTo(start - new Vector2(100, 0));
        Assert.NotEqual(before, _host.State);

        _root.AddButtonEvent(new ButtonEvent("escape", true));
        _root.AddButtonEvent(new ButtonEvent("escape", false));
        Frame();
        Assert.False(split.HasClass("resizing"));
        Assert.Equal(before, _host.State);
        Assert.Equal(start.X, handle.Box.Rect.Center.X, 1.0f);

        MouseButton(false);
        MoveTo(start - new Vector2(150, 0));
        Assert.Equal(before, _host.State);
    }

    [Fact]
    public void DeletedContentIsForgotten()
    {
        var items = OpenThree();
        items[1].Content!.Delete(true);
        Frame();

        Assert.Null(_host.Find("b"));
        Assert.False(_host.IsOpen("b"));
        Assert.Equal(2, _host.Items.Count);
    }

    [Fact]
    public void ClickingATabShowsItsPanelAndTheCloseButtonClosesIt()
    {
        var items = OpenThree();
        var tabs = items.Select(x => Tab(_host, x.Id)).ToArray();
        Assert.Single(_host.Descendants, x => x.HasClass("dock-tabs"));
        Assert.True(tabs[2].HasClass("selected"));
        Assert.True(items[2].Content!.IsVisible);

        MoveTo(TitleCenter(tabs[0]));
        MouseButton(true);
        MouseButton(false);
        Assert.True(tabs[0].HasClass("selected"));
        Assert.False(tabs[2].HasClass("selected"));
        Assert.True(items[0].Content!.IsVisible);
        Assert.False(items[2].Content!.IsVisible);
        Assert.Same(tabs[0], ((TabBar)tabs[0].Parent!).SelectedTab);

        var close = tabs[1].Children.Single(x => x.HasClass("dock-tab-action"));
        Assert.Equal("Close panel", close.Tooltip);
        MoveTo(close.Box.Rect.Center);
        MouseButton(true);
        MouseButton(false);
        Assert.False(_host.IsOpen("b"));
        Assert.False(tabs[1].IsVisible);
        Assert.Equal(0, ((RecordingPanel)items[1].Content!).DeletedCount);

        _host.Dock("b");
        Frame();
        Assert.Same(tabs[1], Tab(_host, "b"));
        Assert.True(tabs[1].HasClass("selected"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("folder", true)]
    [InlineData("tune", false)]
    public void TabsShowTheIconAndACloseButtonOnlyWhenClosable(string? icon, bool canClose)
    {
        var item = _host.Register("a", "Pane", new RecordingPanel(), canClose, icon);
        _host.Dock("a");
        Frame();
        var tab = Tab(_host, "Pane");
        var icons = tab.Children.OfType<IconPanel>().Where(x => x.HasClass("dock-tab-icon")).ToArray();
        Assert.Equal(icon, item.Icon);
        Assert.Equal(icon is null ? 0 : 1, icons.Length);
        if (icon is not null)
        {
            Assert.Equal(icon, icons[0].Text);
            MoveTo(icons[0].Box.Rect.Center);
            Assert.Same(tab, _root.Hovered);
        }

        var actions = tab.Children.Where(x => x.HasClass("dock-tab-action")).ToArray();
        Assert.Equal(canClose ? 1 : 0, actions.Length);
    }

    [Fact]
    public void DraggingATabAlongTheStripReordersIt()
    {
        var items = OpenThree();
        var tabs = items.Select(x => Tab(_host, x.Id)).ToArray();
        var rect = tabs[2].Box.Rect;
        BeginDrag(tabs[0], new Vector2(rect.Right + 8, rect.Center.Y));
        Assert.True(Single("dock-preview").IsVisible);
        MouseButton(false);

        Assert.Equal([tabs[1], tabs[2], tabs[0]], tabs[0].Parent!.Children.Where(x => x.HasClass("dock-tab")));
        Assert.Single(_host.Descendants, x => x.HasClass("dock-group"));
        Assert.True(tabs[0].HasClass("selected"));
        Assert.False(tabs[0].HasClass("dragging"));
        Assert.True(items[0].Content!.IsVisible);
        AssertKept(items, tabs);
    }

    [Theory]
    [InlineData(DockPosition.Left)]
    [InlineData(DockPosition.Right)]
    [InlineData(DockPosition.Top)]
    [InlineData(DockPosition.Bottom)]
    public void DroppingOnARootGuideSplitsTheWorkspace(DockPosition position)
    {
        var items = OpenThree();
        var tabs = items.Select(x => Tab(_host, x.Id)).ToArray();
        BeginDrag(tabs[0], _host.Box.Rect.Position + (_host.Box.Rect.Size * 0.25f));
        var guide = Single($"dock-root-guide-{position.ToString().ToLowerInvariant()}");
        Assert.True(guide.IsVisible);
        MoveTo(guide.Box.Rect.Center);
        Assert.True(guide.HasClass("hovered"));
        Assert.True(Single("dock-preview").IsVisible);
        Assert.Equal(new DockDropTarget(null, position), _host.UpdateDockTargets(guide.Box.Rect.Center, "a"));
        var expected = new DockLayout();
        Assert.True(expected.Restore(_host.State, items.Select(x => x.Id)));
        expected.Dock("a", position: position);
        MouseButton(false);

        Assert.Equal(expected.Save(), _host.State);
        Assert.Equal(2, _host.Descendants.Count(x => x.HasClass("dock-group")));
        Assert.NotSame(tabs[0].Parent, tabs[1].Parent);
        Assert.Same(tabs[1].Parent, tabs[2].Parent);
        Assert.True(items[0].Content!.IsVisible);
        AssertKept(items, tabs);
    }

    [Theory]
    [InlineData(DockPosition.Left)]
    [InlineData(DockPosition.Right)]
    [InlineData(DockPosition.Top)]
    [InlineData(DockPosition.Bottom)]
    [InlineData(DockPosition.Center)]
    public void DroppingOnAnAreasGuideDocksBesideOrInsideIt(DockPosition position)
    {
        var items = OpenThree();
        _host.Dock("b", "a", DockPosition.Right);
        _host.Dock("c", "b");
        Frame();
        var tabs = items.Select(x => Tab(_host, x.Id)).ToArray();
        var group = tabs[1].Parent!.Parent!;
        BeginDrag(tabs[0], group.Box.Rect.Position + (group.Box.Rect.Size * 0.25f));
        Assert.False(Single("dock-preview").IsVisible);
        var guide = Single($"dock-group-guide-{position.ToString().ToLowerInvariant()}");
        Assert.True(guide.IsVisible);
        MoveTo(guide.Box.Rect.Center);
        Assert.True(guide.HasClass("hovered"));
        Assert.True(Single("dock-preview").IsVisible);
        Assert.Equal(new DockDropTarget("c", position), _host.UpdateDockTargets(guide.Box.Rect.Center, "a"));
        var expected = new DockLayout();
        Assert.True(expected.Restore(_host.State, items.Select(x => x.Id)));
        expected.Dock("a", "c", position);
        MouseButton(false);

        Assert.Equal(expected.Save(), _host.State);
        Assert.False(tabs[0].HasClass("dragging"));
        Assert.False(Single("dock-targets").IsVisible);
        AssertKept(items, tabs);
    }

    [Theory]
    [InlineData(0.25f, 0.25f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(0.1f, 0.5f)]
    [InlineData(0.5f, 0.9f)]
    public void DroppingAwayFromTheGuidesChangesNothing(float x, float y)
    {
        var items = OpenThree();
        var tabs = items.Select(item => Tab(_host, item.Id)).ToArray();
        var point = _host.Box.Rect.Position + (_host.Box.Rect.Size * new Vector2(x, y));
        BeginDrag(tabs[0], point);
        var before = _host.State;
        Assert.Null(_host.UpdateDockTargets(point, "a"));
        Frame();
        Assert.False(Single("dock-preview").IsVisible);
        Assert.DoesNotContain(_host.Descendants, panel => panel.HasClass("dock-guide") && panel.HasClass("hovered"));
        MouseButton(false);
        Assert.Equal(before, _host.State);
        AssertKept(items, tabs);
    }

    [Theory]
    [InlineData("external", 3)]
    [InlineData("a", 2)]
    public void EveryAreaThePanelCanGoToIsOutlined(string draggedId, int sectionCount)
    {
        OpenThree();
        _host.Dock("b", "a", DockPosition.Right);
        _host.Dock("c", "b", DockPosition.Bottom);
        Frame();
        var groups = _host.Descendants.Where(x => x.HasClass("dock-group")).ToArray();
        Assert.Null(_host.UpdateDockTargets(null, draggedId));
        Frame();
        var sections = _host.Descendants.Where(x => x.HasClass("dock-section") && x.IsVisible).ToArray();
        Assert.Equal(sectionCount, sections.Length);
        foreach (var group in groups)
        {
            var eligible = draggedId != "a" || group != Tab(_host, "a").Parent!.Parent;
            Assert.Equal(eligible, sections.Any(x => x.Box.Rect == group.Box.Rect));
            if (!eligible)
            {
                continue;
            }

            Assert.NotNull(_host.UpdateDockTargets(group.Box.Rect.Center, draggedId));
            Frame();
            Assert.Equal(group.Box.Rect, sections.Single(x => x.HasClass("hovered")).Box.Rect);
            Assert.Equal(5, _host.Descendants.Count(x => x.IsVisible && x.Classes.Contains("dock-group-guide-", StringComparison.Ordinal)));
            Assert.True(Single("dock-preview").IsVisible);
        }

        _host.CancelDrag();
        Frame();
        Assert.False(Single("dock-targets").IsVisible);
        Assert.All(sections, x => Assert.False(x.IsVisible));
    }

    [Fact]
    public void AnEmptyWorkspaceOffersOnlyItsCenterGuide()
    {
        Frame();
        Assert.Null(_host.UpdateDockTargets(null, "external"));
        Frame();
        Assert.Single(_host.Descendants, x => x.HasClass("dock-section") && x.IsVisible);
        Assert.DoesNotContain(_host.Descendants, x => x.HasClass("dock-guide") && x.IsVisible);
        Assert.Null(_host.UpdateDockTargets(_host.Box.Rect.Position + (_host.Box.Rect.Size * 0.25f), "external"));
        Frame();
        var guide = _host.Descendants.Single(x => x.HasClass("dock-guide") && x.IsVisible);
        Assert.True(guide.HasClass("dock-group-guide-center"));
        Assert.Equal(new DockDropTarget(null, DockPosition.Center), _host.UpdateDockTargets(guide.Box.Rect.Center, "external"));
        Frame();
        Assert.True(Single("dock-preview").IsVisible);
    }

    [Fact]
    public void TheGuidesDontBlockTheMouse()
    {
        var items = OpenThree();
        var center = _host.Box.Rect.Center;
        Assert.NotNull(_host.UpdateDockTargets(center, "external"));
        Frame();
        MoveTo(center);
        Assert.Same(items[2].Content, _root.Hovered);
        var tab = Tab(_host, "a");
        MoveTo(TitleCenter(tab));
        Assert.Same(tab, _root.Hovered);
        MouseButton(true);
        MouseButton(false);
        Assert.True(tab.HasClass("selected"));
        Assert.True(items[0].Content!.IsVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapeOrLeavingTheWindowCancelsTheDrag(bool leave)
    {
        OpenThree();
        var tab = Tab(_host, "a");
        BeginDrag(tab, new Vector2(_host.Box.Rect.Right - 5, _host.Box.Rect.Center.Y));
        MoveTo(Single("dock-root-guide-right").Box.Rect.Center);
        Assert.True(Single("dock-preview").IsVisible);
        var before = _host.State;
        if (leave)
        {
            _root.SetMousePosition(null);
            Frame();
        }
        else
        {
            Key("escape");
        }

        Assert.False(tab.HasClass("dragging"));
        Assert.False(Single("dock-preview").IsVisible);
        Assert.False(Single("dock-targets").IsVisible);
        MoveTo(Single("dock-root-guide-right").Box.Rect.Center);
        MouseButton(false);
        Assert.Equal(before, _host.State);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HiddenPartsOfTheHostArentTargets(bool clipped)
    {
        OpenThree();
        var clip = new Panel { Parent = _root };
        clip.Style.Position = PositionMode.Absolute;
        clip.Style.Width = 200;
        clip.Style.Height = 80;
        if (clipped)
        {
            clip.Style.Overflow = OverflowMode.Hidden;
        }

        _host.Parent = clip;
        _host.Style.Width = 200;
        _host.Style.Height = 400;
        _host.Style.FlexShrink = 0;
        Frame();
        Assert.True(_host.Box.Rect.Center.Y > clip.Box.Rect.Bottom);
        Assert.Equal(!clipped, _host.UpdateDockTargets(_host.Box.Rect.Center, "external").HasValue);
        Assert.NotNull(_host.UpdateDockTargets(Single("dock-tabs").Box.Rect.Center, "external"));
    }

    [Fact]
    public void MarkupMakesADockHost() => Assert.IsType<DockHost>(Panel.CreateElement("dockhost"));

    private sealed class RecordingPanel : Panel
    {
        public int DeletedCount { get; private set; }

        public override void OnDeleted()
        {
            DeletedCount++;
            base.OnDeleted();
        }
    }
}
