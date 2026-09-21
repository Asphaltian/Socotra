using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class VirtualLayoutTests
{
    private static (RootPanel Root, VirtualList List) List(int count)
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("rootpanel { align-items: flex-start; } virtuallist { width: 200px; height: 100px; }");
        var list = root.AddChild<VirtualList>();
        list.ItemHeight = 20;
        list.OnCreateCell = (cell, item) => cell.AddClass($"item-{item}");
        list.Items = Enumerable.Range(0, count).Cast<object>().ToList();
        Update(root);
        Update(root);
        return (root, list);
    }

    private static List<string> Cells(Panel panel) =>
        [.. panel.Children.Where(c => !c.IsDeleted).OrderBy(c => c.Box.Rect.Top).Select(c => c.Class.First(n => n.StartsWith("item-", StringComparison.Ordinal)))];

    [Fact]
    public void OnlyTheRowsInViewGetPanels()
    {
        var (_, list) = List(100);

        Assert.Equal(["item-0", "item-1", "item-2", "item-3", "item-4", "item-5"], Cells(list));
        Assert.Equal(new Vector2(0, 1900), list.ScrollSize);
        Assert.Equal(new Rect(0, 20, 200, 20), list.Children.Single(c => c.HasClass("item-1")).Box.Rect);
    }

    [Fact]
    public void ScrollingSwapsTheRowsInView()
    {
        var (root, list) = List(100);

        list.ScrollOffset = new Vector2(0, 500);
        Update(root);
        Update(root);

        Assert.Equal(["item-25", "item-26", "item-27", "item-28", "item-29", "item-30"], Cells(list));
        Assert.Equal(0, list.Children.Single(c => c.HasClass("item-25")).Box.Rect.Top);
    }

    [Fact]
    public void ChangesToTheSourceListAreNoticed()
    {
        int lastCells = 0;
        var (root, list) = List(3);
        list.OnLastCell = () => lastCells++;
        var items = new List<object> { "x", "y" };
        list.Items = items;
        Update(root);
        Update(root);
        Assert.Equal(["item-x", "item-y"], Cells(list));
        Assert.Equal(1, lastCells);

        items.Add("z");
        Update(root);
        Update(root);

        Assert.Equal(3, list.ItemCount);
        Assert.Equal(["item-x", "item-y", "item-z"], Cells(list));
        Assert.Equal(2, lastCells);
    }

    [Fact]
    public void ItemsCanBeAddedAndRemovedOneAtATime()
    {
        var (root, list) = List(0);

        list.AddItem("a");
        list.AddItems(["b", "c"]);
        list.InsertItem(0, "first");
        list.RemoveItem("b");
        list.RemoveAt(1);
        Update(root);
        Update(root);

        Assert.Equal(["item-first", "item-c"], Cells(list));
        Assert.True(list.HasData(1));
        Assert.False(list.HasData(2));

        list.Clear();
        Update(root);
        Assert.Empty(list.Children);
    }

    [Fact]
    public void GridsFitAsManyColumnsAsWillAndStretchThem()
    {
        var root = new RootPanel();
        root.StyleSheet.Parse("rootpanel { align-items: flex-start; } virtualgrid { width: 300px; height: 100px; }");
        var grid = root.AddChild<VirtualGrid>();
        grid.ItemSize = new Vector2(80, 32);
        grid.OnCreateCell = (cell, item) => cell.AddClass($"item-{item}");
        grid.Items = Enumerable.Range(0, 30).Cast<object>().ToList();
        Update(root);
        Update(root);

        var second = grid.Children.Single(c => c.HasClass("item-1"));
        Assert.Equal(new Rect(100, 0, 100, 40), second.Box.Rect);
        Assert.Equal(12, grid.Children.Count);
        Assert.Equal(new Vector2(0, 300), grid.ScrollSize);
    }
}
