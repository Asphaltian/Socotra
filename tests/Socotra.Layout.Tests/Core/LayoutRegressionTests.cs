namespace Socotra.Layout.Tests.Core;

public class LayoutRegressionTests
{
    [Theory]
    [InlineData(Display.Flex, 40f, 0f)]
    [InlineData(Display.Block, 100f, 100f)]
    [InlineData(Display.Grid, 100f, 100f)]
    public void ConflictingRootBoundsRespectFormattingContext(object display, float width, float height)
    {
        var root = new LayoutNode();
        root.Style.Display = (Display)display;
        root.Style.MinWidth = 100;
        root.Style.MaxWidth = 40;
        root.Style.MinHeight = 100;
        root.Style.MaxHeight = 0;
        var child = new LayoutNode();
        child.Style.Width = 20;
        child.Style.Height = 20;
        root.AddChild(child);

        root.CalculateLayout();
        Assert.Equal(width, root.LayoutWidth);
        Assert.Equal(height, root.LayoutHeight);
        root.Style.SetPadding(Edge.Vertical, 3);
        root.CalculateLayout();
        Assert.Equal(MathF.Max(6, height), root.LayoutHeight);
    }

    [Theory]
    [InlineData(Display.Flex, Display.Flex, 40f, 0f)]
    [InlineData(Display.Flex, Display.Block, 100f, 100f)]
    [InlineData(Display.Flex, Display.Grid, 100f, 100f)]
    [InlineData(Display.Block, Display.Flex, 100f, 100f)]
    [InlineData(Display.Block, Display.Block, 100f, 100f)]
    [InlineData(Display.Block, Display.Grid, 100f, 100f)]
    [InlineData(Display.Grid, Display.Flex, 100f, 100f)]
    [InlineData(Display.Grid, Display.Block, 100f, 100f)]
    [InlineData(Display.Grid, Display.Grid, 100f, 100f)]
    public void ConflictingChildBoundsRespectOwnAndOwnerDisplay(object ownerDisplay, object childDisplay, float width, float height)
    {
        foreach (var contentsDepth in new[] { 0, 2 })
        {
            var root = new LayoutNode();
            root.Style.Display = (Display)ownerDisplay;
            root.Style.Width = 300;
            root.Style.Height = 300;
            var owner = root;
            for (int i = 0; i < contentsDepth; i++)
            {
                var contents = new LayoutNode();
                contents.Style.Display = Display.Contents;
                owner.AddChild(contents);
                owner = contents;
            }
            var child = new LayoutNode();
            child.Style.Display = (Display)childDisplay;
            child.Style.MinWidth = 100;
            child.Style.MaxWidth = 40;
            child.Style.MinHeight = 100;
            child.Style.MaxHeight = 0;
            owner.AddChild(child);
            for (int pass = 0; pass < 2; pass++)
            {
                root.CalculateLayout();
                Assert.Equal(width, child.LayoutWidth);
                Assert.Equal(height, child.LayoutHeight);
                if (pass == 0)
                {
                    child.AddChild(new LayoutNode());
                }
            }
        }
    }

    [Theory]
    [InlineData(Display.Flex)]
    [InlineData(Display.Block)]
    [InlineData(Display.Grid)]
    public void AbsoluteBlockItemsUseCssConflictingBounds(object childDisplay)
    {
        var root = new LayoutNode();
        root.Style.Display = Display.Block;
        root.Style.Width = 200;
        root.Style.Height = 200;
        var contents = new LayoutNode();
        contents.Style.Display = Display.Contents;
        root.AddChild(contents);
        var child = new LayoutNode();
        child.Style.Display = (Display)childDisplay;
        child.Style.PositionType = PositionType.Absolute;
        child.Style.MinWidth = 100;
        child.Style.MaxWidth = 40;
        child.Style.MinHeight = 100;
        child.Style.MaxHeight = 0;
        child.Style.SetPosition(Edge.Right, 10);
        child.Style.SetPosition(Edge.Bottom, 20);
        contents.AddChild(child);
        root.CalculateLayout();
        Assert.Equal(100f, child.LayoutWidth);
        Assert.Equal(100f, child.LayoutHeight);
        Assert.Equal(90f, child.LayoutLeft);
        Assert.Equal(80f, child.LayoutTop);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingBoundsFollowOwnerDisplayChanges(bool measured)
    {
        var root = new LayoutNode();
        root.Style.Width = 300;
        root.Style.Height = 300;
        var contents = new LayoutNode();
        contents.Style.Display = Display.Contents;
        root.AddChild(contents);
        var child = new LayoutNode();
        child.Style.MinWidth = 100;
        child.Style.MaxWidth = 40;
        child.Style.MinHeight = 100;
        child.Style.MaxHeight = 0;
        if (measured)
        {
            child.MeasureFunc = static (_, _, _, _, _) => new LayoutSize(20, 20);
        }

        contents.AddChild(child);
        foreach (var display in new[] { Display.Flex, Display.Block, Display.Grid, Display.Flex })
        {
            root.Style.Display = display;
            root.CalculateLayout();
            Assert.Equal(display == Display.Flex ? 40f : 100f, child.LayoutWidth);
            Assert.Equal(display == Display.Flex ? 0f : 100f, child.LayoutHeight);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaxHeightZeroCollapsesFlexChildWithMinimum(bool measured)
    {
        var root = new LayoutNode();
        root.Style.FlexDirection = FlexDirection.Column;
        root.Style.Width = 100;
        var child = new LayoutNode();
        child.Style.MinHeight = 50;
        child.Style.MaxHeight = 0;
        if (measured)
        {
            child.MeasureFunc = static (_, _, _, _, _) => new LayoutSize(10, 20);
        }

        root.AddChild(child);
        root.CalculateLayout();
        Assert.Equal(0f, child.LayoutHeight);
        Assert.Equal(0f, root.LayoutHeight);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(10f)]
    public void BlockMeasurementComputesEscapingMarginsBeforeFlexBasis(float height)
    {
        var root = new LayoutNode();
        root.Style.FlexDirection = FlexDirection.Column;
        root.Style.Width = 100;
        var outer = new LayoutNode();
        outer.Style.Display = Display.Block;
        var inner = new LayoutNode();
        inner.Style.Display = Display.Block;
        inner.Style.Height = height;
        var child = new LayoutNode();
        child.Style.Display = Display.Block;
        child.Style.Height = height;
        child.Style.SetMargin(Edge.Top, 20);
        inner.AddChild(child);
        outer.AddChild(inner);
        root.AddChild(outer);

        for (int pass = 0; pass < 3; pass++)
        {
            var margin = 20 + pass * 10;
            child.Style.SetMargin(Edge.Top, margin);
            root.CalculateLayout();
            Assert.Equal(height + margin, root.LayoutHeight);
            Assert.Equal(height + margin, outer.LayoutHeight);
            Assert.Equal((float)margin, inner.LayoutTop);
            Assert.Equal(0f, child.LayoutTop);
        }
    }

    [Theory]
    [InlineData(Display.Flex)]
    [InlineData(Display.Block)]
    public void AutoWidthContainerShapesMeasuredLeafOnce(object display)
    {
        var root = new LayoutNode();
        root.Style.Display = (Display)display;
        var calls = 0;
        var child = new LayoutNode
        {
            MeasureFunc = (_, _, _, _, _) =>
            {
                calls++;
                return new LayoutSize(80, 20);
            },
        };
        root.AddChild(child);
        root.CalculateLayout();
        Assert.Equal(80f, root.LayoutWidth);
        Assert.Equal(20f, root.LayoutHeight);
        Assert.Equal(1, calls);
        root.CalculateLayout();
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MeasuredLeafOwnerComparisonIsOnlyNeededForPercentages(bool percentages)
    {
        var calls = 0;
        var node = new LayoutNode
        {
            MeasureFunc = (_, _, _, _, _) =>
            {
                calls++;
                return new LayoutSize(10, 10);
            },
        };
        node.Style.SetPadding(Edge.Left, percentages ? StyleLength.Percent(10) : 10);
        node.ProcessDimensions();
        var generation = LayoutNode.NextGeneration();
        LayoutAlgorithm.CalculateLayoutInternal(node, 100, 50, Direction.LTR, SizingMode.FitContent, SizingMode.FitContent, 200, 100, false, 0, generation);
        LayoutAlgorithm.CalculateLayoutInternal(node, 100, 50, Direction.LTR, SizingMode.FitContent, SizingMode.FitContent, 400, 200, true, 0, generation);
        LayoutAlgorithm.CalculateLayoutInternal(node, 100, 50, Direction.LTR, SizingMode.FitContent, SizingMode.FitContent, 600, 300, true, 0, generation);
        Assert.Equal(percentages ? 3 : 1, calls);
        Assert.Equal(percentages ? 60f : 10f, node.LayoutPadding(PhysicalEdge.Left));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainerReuseStillResolvesDescendantPercentages(bool performLayout)
    {
        var container = new LayoutNode();
        container.Style.FlexDirection = FlexDirection.Column;
        var child = new LayoutNode();
        child.Style.MinHeight = StyleLength.Percent(50);
        container.AddChild(child);
        container.ProcessDimensions();
        var generation = LayoutNode.NextGeneration();
        LayoutAlgorithm.CalculateLayoutInternal(container, 100, 200, Direction.LTR, SizingMode.StretchFit, SizingMode.StretchFit, 100, 200, performLayout, 0, generation);
        var visited = LayoutAlgorithm.CalculateLayoutInternal(container, 100, 200, Direction.LTR, SizingMode.StretchFit, SizingMode.StretchFit, 100, 400, performLayout, 0, generation);
        Assert.True(visited, "Owner changes must not reuse a container entry based only on its own percentage-free style.");
        LayoutAlgorithm.CalculateLayoutInternal(container, 100, 400, Direction.LTR, SizingMode.StretchFit, SizingMode.StretchFit, 100, 400, true, 0, generation);
        Assert.Equal(200f, child.LayoutHeight);
    }

    [Fact]
    public void DescendantPercentagesBlockLooseContainerMeasurementReuse()
    {
        var container = new LayoutNode();
        container.Style.FlexDirection = FlexDirection.Column;
        var child = new LayoutNode();
        child.Style.Height = StyleLength.Percent(50);
        container.AddChild(child);
        container.ProcessDimensions();
        var generation = LayoutNode.NextGeneration();
        LayoutAlgorithm.CalculateLayoutInternal(container, 100, 400, Direction.LTR, SizingMode.StretchFit, SizingMode.FitContent, 100, 400, false, 0, generation, MeasureScope.Height);
        Assert.Equal(200f, container.Layout.MeasuredDimension(Dimension.Height));
        LayoutAlgorithm.CalculateLayoutInternal(container, 100, 300, Direction.LTR, SizingMode.StretchFit, SizingMode.FitContent, 100, 400, false, 0, generation, MeasureScope.Height);
        Assert.Equal(150f, container.Layout.MeasuredDimension(Dimension.Height));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData(" \tAUTO\r\n")]
    public void AutoPlacementDoesNotAllocate(string text)
    {
        GridParser.TryParsePlacement(text, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            GridParser.TryParsePlacement(text, out _);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.True(GridParser.TryParsePlacement(text, out var result));
        Assert.Equal(GridPlacement.Auto, result);
        Assert.False(GridParser.TryParsePlacement("auto extra", out _));
    }

    [Theory]
    [InlineData(Display.Flex, 40, 1)]
    [InlineData(Display.Flex, 1, 600)]
    [InlineData(Display.Block, 40, 1)]
    [InlineData(Display.Block, 1, 600)]
    [InlineData(Display.Grid, 1, 600)]
    [InlineData(Display.Grid, 1, 1500)]
    public void WarmLayoutsBeyondOldPoolLimitsDoNotAllocate(object displayValue, int depth, int breadth)
    {
        var display = (Display)displayValue;
        var root = new LayoutNode();
        var nodes = new List<LayoutNode> { root };
        var current = root;
        for (int d = 0; d < depth; d++)
        {
            current.Style.Display = display;
            current.Style.Width = 1000;
            current.Style.Height = 1000;
            if (display == Display.Grid)
            {
                current.Style.GridTemplateColumns = new TrackList(TrackSizingFunction.Points(10));
            }

            for (int i = 0; i < breadth; i++)
            {
                var child = new LayoutNode();
                child.Style.Width = 1;
                child.Style.Height = 1;
                current.AddChild(child);
                nodes.Add(child);
            }
            current = current.GetChild(0);
        }

        long allocated = 0;
        for (int pass = 0; pass < 12; pass++)
        {
            foreach (var node in nodes)
            {
                node.MarkDirty();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            root.CalculateLayout();
            if (pass >= 8)
            {
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
        }
        Assert.Equal(0L, allocated);
    }

    [Theory]
    [InlineData(0, 128)]
    [InlineData(4096, 8)]
    [InlineData(8192, 0)]
    public void NodeListRetentionHasCountCapacityAndTotalBounds(int capacity, int expectedRetained)
    {
        var lists = new List<LayoutNode>[160];
        var returned = new HashSet<List<LayoutNode>>();
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = LayoutAlgorithm.RentList();
            lists[i].Capacity = capacity;
            returned.Add(lists[i]);
        }
        foreach (var list in lists)
        {
            LayoutAlgorithm.ReturnList(list);
        }

        var retained = 0;
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = LayoutAlgorithm.RentList();
            if (returned.Contains(lists[i]))
            {
                retained++;
            }

            Assert.Empty(lists[i]);
        }
        Assert.Equal(expectedRetained, retained);
        foreach (var list in lists)
        {
            LayoutAlgorithm.ReturnList(list);
        }
    }

    [Theory]
    [InlineData(0, 4096)]
    [InlineData(4096, 8)]
    [InlineData(8192, 0)]
    public void GridRetentionHasCountCapacityAndTotalBounds(int capacity, int expectedRetained)
    {
        var items = new object[5000];
        var returned = new HashSet<object>();
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = GridPool<object>.Rent();
            returned.Add(items[i]);
        }
        foreach (var item in items)
        {
            GridPool<object>.Return(item, capacity);
        }

        var retained = 0;
        for (int i = 0; i < items.Length; i++)
        {
            if (returned.Contains(GridPool<object>.Rent()))
            {
                retained++;
            }
        }
        Assert.Equal(expectedRetained, retained);
    }
}
