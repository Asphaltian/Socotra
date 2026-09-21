namespace Socotra.Tests;

public class TreeViewTests
{
    private static RootPanel CreateRoot() => new UnscaledRoot();

    private static void Layout(RootPanel root, float deltaTime = 0.016f) => root.Update(new Rect(0, 0, 1000, 1000), deltaTime);

    private static List<Node> MakeTree(int roots = 10, int children = 10, int grandchildren = 10)
    {
        var list = new List<Node>();
        for (int r = 0; r < roots; r++)
        {
            var root = new Node($"r{r}");
            list.Add(root);
            for (int c = 0; c < children; c++)
            {
                var child = root.Add($"r{r}c{c}");
                for (int g = 0; g < grandchildren; g++)
                {
                    child.Add($"r{r}c{c}g{g}");
                }
            }
        }

        return list;
    }

    private static TreeView<Node> CreateTree(RootPanel root, List<Node> data, float height = 400)
    {
        var tree = new TreeView<Node> { Parent = root };
        tree.Style.Set($"width: 300px; height: {height}px;");
        tree.RowHeight = 40;
        tree.Roots = data;
        tree.GetChildren = n => n.Children;
        tree.OnRow = (row, n) => row.Text = n.Name;
        return tree;
    }

    private static void Press(BaseTreeView tree, string button, bool shift = false)
    {
        var root = tree.FindRootPanel()!;
        var modifiers = shift ? KeyboardModifiers.Shift : KeyboardModifiers.None;
        tree.Focus();
        root.AddButtonEvent(new ButtonEvent(button, true, modifiers));
        root.AddButtonEvent(new ButtonEvent(button, false, modifiers));
        Layout(root);
    }

    private static void Click(TreeRow row, KeyboardModifiers modifiers = KeyboardModifiers.None) =>
        row.DispatchEventImmediate(new MousePanelEvent("onclick", row, "mouseleft") { KeyboardModifiers = modifiers });

    [Fact]
    public void OnlyVisibleRowsGetPanels()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(100));
        Layout(root);
        Layout(root);

        Assert.Equal(100, tree.RowCount);
        Assert.InRange(tree.ActiveRowCount, 10, 12);
        Assert.Equal(tree.ActiveRowCount, tree.PooledRowCount);

        var row3 = tree.GetRowPanel(3)!;
        Assert.Equal("r3", row3.Text);
        Assert.Equal(Length.Pixels(120), row3.Style.Top);
        Assert.Equal(Length.Pixels(40), row3.Style.Height);
    }

    [Fact]
    public void RowsShowTheItemsTooltip()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(5));
        tree.GetTooltip = n => n.Name == "r2" ? null : $"Node {n.Name}";
        tree.Style.PointerEvents = PointerEvents.All;
        Layout(root);
        Layout(root);

        Assert.Equal("Node r1", tree.GetRowPanel(1)!.Tooltip);
        Assert.Null(tree.GetRowPanel(2)!.Tooltip);

        root.SetMousePosition(tree.GetRowPanel(3)!.Box.Rect.Center);
        Layout(root);
        Assert.Equal("Node r3", (root.Tooltips.Current?.Children.FirstOrDefault() as Label)?.Text);
    }

    [Fact]
    public void OpeningAndClosingChangeTheRows()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 4, 2);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);
        Assert.Equal(3, tree.RowCount);

        tree.Open(data[1]);
        Layout(root);
        Assert.Equal(7, tree.RowCount);
        Assert.Equal(0, tree.GetRowDepth(1));
        Assert.Equal(1, tree.GetRowDepth(2));
        Assert.Equal(0, tree.GetRowDepth(6));
        Assert.Equal("r1c0", tree.GetRowItem(2).Name);
        Assert.Equal("r2", tree.GetRowItem(6).Name);
        Assert.Equal(1, tree.GetParentRow(3));
        Assert.Equal(6, tree.GetSubtreeEnd(1));
        Assert.True(tree.GetRowPanel(1)!.HasClass("open"));
        Assert.True(tree.GetRowPanel(1)!.HasClass("has-children"));

        tree.Open(data[1].Children[0]);
        Layout(root);
        Assert.Equal(9, tree.RowCount);
        Assert.Equal(2, tree.GetRowDepth(3));
        Assert.Equal(Length.Pixels(32), tree.GetRowPanel(3)!.Style.PaddingLeft);

        tree.Close(data[1], recursive: true);
        Layout(root);
        Assert.Equal(3, tree.RowCount);
        Assert.False(tree.IsOpen(data[1].Children[0]));
    }

    [Fact]
    public void ClickingTheExpanderTogglesTheRow()
    {
        var root = CreateRoot();
        var data = MakeTree(2, 2, 0);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);

        var row = tree.GetRowPanel(0)!;
        row.Expander.DispatchEventImmediate(new MousePanelEvent("onclick", row.Expander, "mouseleft"));
        Assert.True(tree.IsOpen(data[0]));
        Assert.Empty(tree.Selection);

        Layout(root);
        Assert.Equal(4, tree.RowCount);
        row.Expander.DispatchEventImmediate(new MousePanelEvent("onclick", row.Expander, "mouseleft"));
        Assert.False(tree.IsOpen(data[0]));
    }

    [Fact]
    public void ScrollingReusesPanels()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(100));
        Layout(root);
        Layout(root);
        var first = tree.GetRowPanel(0)!;

        tree.ScrollOffset = new Vector2(0, 400);
        Layout(root);

        var maxPooled = 10 + 2 + (tree.OverscanRows * 2);
        Assert.True(tree.PooledRowCount <= maxPooled, $"pool grew to {tree.PooledRowCount}");
        Assert.False(first.IsDeleted);
        Assert.Null(tree.GetRowPanel(0));
        Assert.Equal("r10", tree.GetRowPanel(10)!.Text);

        for (int y = 400; y < (100 * 40) - 400; y += 10)
        {
            tree.ScrollOffset = new Vector2(0, y);
            Layout(root);
            Assert.True(tree.PooledRowCount <= maxPooled, $"pool grew to {tree.PooledRowCount} at {y}");
        }
    }

    [Fact]
    public void ListCountChangesAreNoticed()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 2, 0);
        var tree = CreateTree(root, data);
        tree.Open(data[0]);
        Layout(root);
        Layout(root);
        Assert.Equal(5, tree.RowCount);

        data[0].Children.Add(new Node("new"));
        Layout(root);
        Assert.Equal(6, tree.RowCount);
        Assert.Equal("new", tree.GetRowItem(3).Name);

        data.Add(new Node("root"));
        Layout(root);
        Assert.Equal(7, tree.RowCount);

        data.RemoveAt(0);
        Layout(root);
        Assert.Equal(3, tree.RowCount);
    }

    [Fact]
    public void PerItemHeightsPositionRows()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(5, 0, 0));
        tree.GetHeight = n => n.Name == "r1" ? 100 : 20;
        Layout(root);
        Layout(root);

        Assert.Equal(Length.Pixels(0), tree.GetRowPanel(0)!.Style.Top);
        Assert.Equal(Length.Pixels(20), tree.GetRowPanel(1)!.Style.Top);
        Assert.Equal(Length.Pixels(100), tree.GetRowPanel(1)!.Style.Height);
        Assert.Equal(Length.Pixels(120), tree.GetRowPanel(2)!.Style.Top);
    }

    [Fact]
    public void SelectionReplacesTogglesAndRanges()
    {
        var root = CreateRoot();
        var data = MakeTree(10, 0, 0);
        var tree = CreateTree(root, data);
        int changes = 0;
        tree.OnSelectionChanged = () => changes++;
        Layout(root);
        Layout(root);

        tree.SelectRow(2);
        Assert.Single(tree.Selection);
        Assert.True(tree.IsSelected(data[2]));
        Assert.True(tree.GetRowPanel(2)!.HasClass("selected"));

        tree.SelectRow(4, toggle: true);
        Assert.Equal(2, tree.Selection.Count);
        tree.SelectRow(4, toggle: true);
        Assert.Single(tree.Selection);
        Assert.False(tree.GetRowPanel(4)!.HasClass("selected"));

        tree.SelectRow(7, range: true);
        Assert.Equal(4, tree.Selection.Count);
        Assert.True(tree.IsSelected(data[4]) && tree.IsSelected(data[7]));

        tree.SelectRow(0);
        Assert.Single(tree.Selection);
        Assert.Equal(5, changes);

        tree.ClearSelection();
        Assert.Empty(tree.Selection);
    }

    [Fact]
    public void ClicksSelectWithCtrlAndShift()
    {
        var root = CreateRoot();
        var data = MakeTree(6, 0, 0);
        var tree = CreateTree(root, data);
        Node? selected = null;
        tree.OnSelect = n => selected = n;
        Layout(root);
        Layout(root);

        Click(tree.GetRowPanel(1)!);
        Click(tree.GetRowPanel(3)!, KeyboardModifiers.Ctrl);
        Assert.Equal([data[1], data[3]], tree.Selection.OrderBy(n => n.Name));
        Assert.Same(data[3], selected);

        Click(tree.GetRowPanel(5)!, KeyboardModifiers.Shift);
        Assert.Equal(["r3", "r4", "r5"], tree.Selection.Select(n => n.Name).Order());

        tree.DispatchEventImmediate(new MousePanelEvent("onclick", tree, "mouseleft"));
        Assert.Empty(tree.Selection);
    }

    [Fact]
    public void ArrowKeysStayInTheTreeInsteadOfMovingFocus()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(3, 0, 0));
        var below = new Button("Below") { Parent = root };
        Layout(root);
        Layout(root);

        Press(tree, "down");
        Press(tree, "down");

        Assert.Same(tree, root.Focused);
        Assert.Equal(1, tree.CursorRow);
        Assert.False(below.HasFocus);
    }

    [Fact]
    public void KeyboardMovesOpensClosesAndActivates()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 2, 0);
        var tree = CreateTree(root, data);
        Node? activated = null;
        tree.OnActivate = n => activated = n;
        Layout(root);
        Layout(root);

        Press(tree, "down");
        Assert.Equal(0, tree.CursorRow);
        Assert.True(tree.IsSelected(data[0]));

        Press(tree, "down");
        Assert.Equal(1, tree.CursorRow);

        Press(tree, "right");
        Layout(root);
        Assert.True(tree.IsOpen(data[1]));
        Assert.Equal(5, tree.RowCount);

        Press(tree, "right");
        Assert.Equal(2, tree.CursorRow);
        Assert.Same(data[1].Children[0], tree.CursorItem);

        Press(tree, "left");
        Assert.Equal(1, tree.CursorRow);

        Press(tree, "left");
        Layout(root);
        Assert.False(tree.IsOpen(data[1]));
        Assert.Equal(3, tree.RowCount);

        Press(tree, "space");
        Layout(root);
        Assert.True(tree.IsOpen(data[1]));
        Press(tree, "space");
        Layout(root);

        Press(tree, "enter");
        Assert.Same(data[1], activated);

        Press(tree, "end");
        Assert.Equal(2, tree.CursorRow);

        Press(tree, "home");
        Assert.Equal(0, tree.CursorRow);

        Press(tree, "down", shift: true);
        Press(tree, "down", shift: true);
        Assert.Equal(3, tree.Selection.Count);
    }

    [Fact]
    public void KeysReachTheFocusedTree()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 0, 0);
        var tree = CreateTree(root, data);
        Layout(root);
        tree.Focus();
        Layout(root);

        root.AddButtonEvent(new ButtonEvent("down", true));
        root.AddButtonEvent(new ButtonEvent("down", false));
        root.AddButtonEvent(new ButtonEvent("down", true));
        Layout(root);
        Layout(root);

        Assert.Equal(1, tree.CursorRow);
        Assert.True(tree.IsSelected(data[1]));
    }

    [Fact]
    public void SelectOpensThePathAndScrollsToIt()
    {
        var root = CreateRoot();
        var data = MakeTree(50, 5, 5);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);

        var target = data[40].Children[2].Children[3];
        tree.Select(target);
        Layout(root);

        Assert.True(tree.IsOpen(data[40]));
        Assert.True(tree.IsOpen(data[40].Children[2]));
        Assert.True(tree.IsSelected(target));

        var row = tree.GetItemRow(target);
        Assert.True(row > 40);
        Assert.NotNull(tree.GetRowPanel(row));
        Assert.True(tree.ScrollOffset.Y > 0);
        Assert.False(tree.ExpandPathTo(new Node("stranger")));
    }

    [Fact]
    public void RenamingRoundTrips()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 0, 0);
        var tree = CreateTree(root, data);
        string? renamed = null;
        tree.OnRename = (n, text) =>
        {
            n.Name = text;
            renamed = text;
        };
        Layout(root);
        Layout(root);

        tree.SelectRow(1);
        Press(tree, "f2");
        var panel = tree.GetRowPanel(1)!;
        Assert.True(panel.IsRenaming);
        Assert.True(panel.HasClass("renaming"));

        var entry = panel.Children.OfType<TextEntry>().Single();
        Assert.Equal("r1", entry.Text);
        entry.Text = "Renamed";
        entry.CreateEvent("onsubmit", "Renamed");
        Layout(root);
        Layout(root);

        Assert.Equal("Renamed", renamed);
        Assert.Equal("Renamed", data[1].Name);
        Assert.False(panel.IsRenaming);
        Assert.Equal("Renamed", tree.GetRowPanel(1)!.Text);
    }

    [Fact]
    public void RenamingNeedsAHandler()
    {
        var root = CreateRoot();
        var tree = CreateTree(root, MakeTree(3, 0, 0));
        Layout(root);
        Layout(root);

        tree.SelectRow(1);
        Press(tree, "f2");
        Assert.False(tree.GetRowPanel(1)!.IsRenaming);
    }

    [Fact]
    public void RefreshRebindsAndTheObjectTreeWorks()
    {
        var root = CreateRoot();
        var tree = new TreeView { Parent = root };
        tree.Style.Set("width: 300px; height: 400px;");
        tree.Roots = new List<object> { "one", "two" };
        Layout(root);
        Layout(root);

        Assert.Equal(2, tree.RowCount);
        Assert.Equal("two", tree.GetRowPanel(1)!.Text);

        var binds = tree.BindCount;
        Layout(root);
        Assert.Equal(binds, tree.BindCount);

        tree.Refresh();
        Layout(root);
        Assert.Equal(binds + 2, tree.BindCount);
        Assert.Equal(Length.Pixels(0), tree.GetRowPanel(0)!.Style.Top);
        Assert.Equal(Length.Pixels(24), tree.GetRowPanel(1)!.Style.Top);
    }

    [Fact]
    public void HighlightFlashesOneRowThenFades()
    {
        var root = CreateRoot();
        var data = MakeTree(50, 5, 5);
        var tree = CreateTree(root, data);
        tree.HighlightTime = 1;
        tree.HighlightFadeTime = 1;
        Layout(root);
        Layout(root);

        var target = data[30].Children[1].Children[2];
        tree.Highlight(target);
        Layout(root);

        var row = tree.GetItemRow(target);
        var panel = tree.GetRowPanel(row)!;
        Assert.True(tree.IsOpen(data[30]));
        Assert.True(panel.HasClass("highlight"));
        Assert.False(panel.HasClass("highlight-fade"));
        Assert.False(tree.GetRowPanel(row - 1)!.HasClass("highlight"));

        Layout(root, 1.5f);
        Assert.False(panel.HasClass("highlight"));
        Assert.True(panel.HasClass("highlight-fade"));

        Layout(root, 1);
        Assert.False(panel.HasClass("highlight-fade"));
    }

    [Fact]
    public void RecursiveCloseOnlyAsksOpenItemsForChildren()
    {
        var root = CreateRoot();
        var data = MakeTree(2, 3, 3);
        var tree = CreateTree(root, data);
        int asked = 0;
        tree.GetChildren = n =>
        {
            asked++;
            return n.Children;
        };
        tree.Open(data[0]);
        tree.Open(data[0].Children[1]);
        Layout(root);
        Layout(root);
        asked = 0;

        tree.Close(data[0], recursive: true);

        Assert.Equal(2, asked);
        Assert.False(tree.IsOpen(data[0].Children[1]));
    }

    [Fact]
    public void AParentWalkStopsAtTheTopAndAtLoops()
    {
        var numbers = new TreeView<int>
        {
            Roots = [1, 2],
            GetChildren = i => i < 10 ? [i * 10, (i * 10) + 1] : [],
            GetParent = i => i / 10,
        };
        Assert.True(numbers.ExpandPathTo(11));
        Assert.True(numbers.IsOpen(1));

        var a = new Node("a");
        var b = a.Add("b");
        var looped = CreateTree(CreateRoot(), [a]);
        looped.GetParent = n => n == a ? b : a;
        Assert.True(looped.ExpandPathTo(b));
    }

    [Fact]
    public void CanExpandWithoutChildrenFindsNothing()
    {
        var tree = new TreeView<Node> { Roots = [new Node("a")], CanExpand = _ => true };

        Assert.False(tree.ExpandPathTo(new Node("stranger")));
    }

    [Fact]
    public void GetParentWalksUpInsteadOfSearching()
    {
        var root = CreateRoot();
        var data = MakeTree(20, 5, 5);
        var parents = new Dictionary<Node, Node>();
        foreach (var r in data)
        {
            foreach (var c in r.Children)
            {
                parents[c] = r;
                foreach (var g in c.Children)
                {
                    parents[g] = c;
                }
            }
        }

        var tree = CreateTree(root, data);
        tree.GetParent = n => parents.GetValueOrDefault(n);
        int asked = 0;
        tree.GetChildren = n =>
        {
            asked++;
            return n.Children;
        };
        Layout(root);
        Layout(root);
        asked = 0;

        var target = data[15].Children[3].Children[1];
        Assert.True(tree.ExpandPathTo(target));
        tree.Rebuild();

        Assert.True(tree.IsOpen(data[15]) && tree.IsOpen(data[15].Children[3]));
        Assert.True(tree.GetItemRow(target) > 0);
        Assert.True(asked <= 20 + 5 + 5, $"asked {asked} times");
    }

    [Fact]
    public void AComparerLetsNewObjectsMatchOldOnes()
    {
        var temp = Directory.CreateTempSubdirectory("socotra-treeview-");
        try
        {
            var deep = temp.CreateSubdirectory("a").CreateSubdirectory("b");
            var file = new FileInfo(Path.Combine(deep.FullName, "leaf.txt"));
            File.WriteAllText(file.FullName, "x");
            temp.CreateSubdirectory("c");

            var root = CreateRoot();
            var tree = new TreeView<FileSystemInfo> { Parent = root };
            tree.Style.Set("width: 300px; height: 400px;");
            tree.Comparer = new PathComparer();
            tree.Roots = temp.GetFileSystemInfos();
            tree.CanExpand = f => f is DirectoryInfo;
            tree.GetChildren = f => f is DirectoryInfo d ? d.GetFileSystemInfos() : null;
            tree.GetParent = f => f is DirectoryInfo d ? d.Parent : ((FileInfo)f).Directory;
            tree.OnRow = (r, f) => r.Text = f.Name;
            Layout(root);
            Layout(root);
            Assert.Equal(2, tree.RowCount);

            tree.Highlight(new FileInfo(file.FullName));
            Layout(root);

            Assert.Equal(4, tree.RowCount);
            Assert.Equal("leaf.txt", tree.GetRowItem(2).Name);
            Assert.Equal(2, tree.GetRowDepth(2));
            Assert.True(tree.GetRowPanel(2)!.HasClass("highlight"));
        }
        finally
        {
            temp.Delete(true);
        }
    }

    [Fact]
    public void DoubleClickOpensAClosedRowBeforeActivatingIt()
    {
        var root = CreateRoot();
        var data = MakeTree(2, 2, 0);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);

        var branch = data[0];
        Node? activated = null;
        bool openWhenActivated = false;
        tree.OnActivate = item =>
        {
            activated = item;
            openWhenActivated = tree.IsOpen(branch);
        };
        tree.RowDoubleClicked(0);
        Assert.Same(branch, activated);
        Assert.True(openWhenActivated);
        Layout(root);
        Assert.Equal(4, tree.RowCount);

        tree.RowDoubleClicked(0);
        Assert.True(tree.IsOpen(branch));
        tree.RowDoubleClicked(1);
        Assert.Same(branch.Children[0], activated);
        Assert.False(tree.IsOpen(branch.Children[0]));

        tree.ExpandOnDoubleClick = false;
        tree.Close(branch);
        Layout(root);
        tree.RowDoubleClicked(0);
        Assert.False(tree.IsOpen(branch));
    }

    [Fact]
    public void RefreshingSelectionKeepsPanelsInPlace()
    {
        var root = CreateRoot();
        var data = MakeTree(6, 0, 0);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);
        var panels = Enumerable.Range(0, data.Count).Select(i => tree.GetRowPanel(i)!).ToList();
        var binds = tree.BindCount;
        tree.OnSelect = _ => tree.RebindRows();
        data[2].Name = "Updated";
        tree.SelectRow(2);
        Layout(root);
        Layout(root);

        for (int i = 0; i < data.Count; i++)
        {
            Assert.Same(panels[i], tree.GetRowPanel(i));
            Assert.Equal(data[i].Name, panels[i].Text);
        }

        Assert.Equal(binds + data.Count, tree.BindCount);
        Assert.True(panels[2].HasClass("selected"));
    }

    [Fact]
    public void ClickToExpandIsOptIn()
    {
        var root = CreateRoot();
        var data = MakeTree(2, 2, 0);
        var tree = CreateTree(root, data);
        Layout(root);
        Layout(root);

        Assert.False(tree.ExpandOnClick);
        Click(tree.GetRowPanel(0)!);
        Assert.False(tree.IsOpen(data[0]));
        Assert.Same(data[0], tree.CursorItem);

        tree.ExpandOnClick = true;
        Click(tree.GetRowPanel(0)!);
        Assert.True(tree.IsOpen(data[0]));
        Layout(root);
        Layout(root);
        Assert.Equal(4, tree.RowCount);
        Click(tree.GetRowPanel(0)!);
        Assert.True(tree.IsOpen(data[0]));

        Click(tree.GetRowPanel(1)!);
        Assert.Same(data[0].Children[0], tree.CursorItem);
        Assert.False(tree.IsOpen(data[0].Children[0]));
    }

    [Fact]
    public void DroppingARowOnAnotherReportsBothItems()
    {
        var root = CreateRoot();
        var data = MakeTree(3, 0, 0);
        var tree = CreateTree(root, data);
        (Node Source, Node? Target)? dropped = null;
        tree.CanDrag = n => n != data[2];
        tree.OnItemDropped = (source, target) => dropped = (source, target);
        Layout(root);
        Layout(root);

        var first = tree.GetRowPanel(0)!;
        var second = tree.GetRowPanel(1)!;
        Assert.True(first.WantsDrag);
        Assert.False(tree.GetRowPanel(2)!.WantsDrag);

        second.DispatchEventImmediate(new PanelEvent("ondragenter", first));
        Assert.True(second.HasClass("drop-target"));
        second.DispatchEventImmediate(new PanelEvent("ondrop", first));
        Assert.False(second.HasClass("drop-target"));
        Assert.Equal((data[0], data[1]), dropped);

        tree.DispatchEventImmediate(new PanelEvent("ondrop", second));
        Assert.Equal((data[1], (Node?)null), dropped);
    }

    private sealed class Node(string name)
    {
        public string Name { get; set; } = name;

        public List<Node> Children { get; } = [];

        public Node Add(string name)
        {
            var child = new Node(name);
            Children.Add(child);
            return child;
        }
    }

    private sealed class PathComparer : IEqualityComparer<FileSystemInfo>
    {
        public bool Equals(FileSystemInfo? a, FileSystemInfo? b) => string.Equals(a?.FullName, b?.FullName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(FileSystemInfo f) => StringComparer.OrdinalIgnoreCase.GetHashCode(f.FullName);
    }
}
