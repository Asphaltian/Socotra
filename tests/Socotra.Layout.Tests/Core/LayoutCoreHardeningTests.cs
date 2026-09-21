namespace Socotra.Layout.Tests.Core;

public class LayoutCoreHardeningTests
{
    [Fact]
    public void TreeRejectsSelfAndAncestorCycles()
    {
        var root = new LayoutNode();
        var child = new LayoutNode();
        var grandchild = new LayoutNode();
        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Throws<InvalidOperationException>(() => root.AddChild(root));
        Assert.Throws<InvalidOperationException>(() => grandchild.AddChild(root));

        Assert.Same(root, child.Owner);
        Assert.Same(child, grandchild.Owner);
        Assert.Equal(1, root.ChildCount);
        Assert.Equal(1, child.ChildCount);
        Assert.Equal(0, grandchild.ChildCount);
    }

    [Fact]
    public void ChildrenCannotMutateTheTree()
    {
        var root = new LayoutNode();
        var child = new LayoutNode();
        root.AddChild(child);

        var children = (ICollection<LayoutNode>)root.Children;
        Assert.True(children.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => children.Add(new LayoutNode()));
        Assert.Same(root, child.Owner);
        Assert.Equal(1, root.ChildCount);
    }

    [Fact]
    public void ResultAffectingCallbacksAndFlagsDirtyAncestors()
    {
        var root = new LayoutNode();
        var child = new LayoutNode
        {
            MeasureFunc = static (_, _, _, _, _) => new LayoutSize(10, 10),
            BaselineFunc = static (_, _, _) => 8,
        };
        root.AddChild(child);
        root.CalculateLayout();

        child.MeasureFunc = static (_, _, _, _, _) => new LayoutSize(20, 10);
        Assert.True(child.IsDirty);
        Assert.True(root.IsDirty);
        root.CalculateLayout();
        Assert.Equal(20, child.LayoutWidth);

        child.BaselineFunc = static (_, _, _) => 7;
        Assert.True(root.IsDirty);
        root.CalculateLayout();

        child.IsReferenceBaseline = true;
        Assert.True(root.IsDirty);
        root.CalculateLayout();

        child.AlwaysFormsContainingBlock = true;
        Assert.True(root.IsDirty);
    }

    [Fact]
    public void ResetClearsDirtiedCallbackBeforeResettingStyle()
    {
        var node = new LayoutNode();
        node.Style.Width = 20;
        node.CalculateLayout();
        var dirtiedCalls = 0;
        node.DirtiedCallback = _ => dirtiedCalls++;

        node.Reset();

        Assert.Equal(0, dirtiedCalls);
        Assert.Null(node.DirtiedCallback);
        Assert.True(node.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CssDefaultsApplyToNewAndResetNodes(bool reset)
    {
        var node = new LayoutNode();
        if (reset)
        {
            node.Style.FlexDirection = FlexDirection.Column;
            node.Style.AlignContent = Align.FlexStart;
            node.Style.JustifyContent = Justify.FlexEnd;
            node.Style.AlignItems = Align.Center;
            node.Style.FlexShrink = 0;
            node.Style.FlexGrow = 2;
            node.Style.FlexBasis = 50;
            node.Style.Width = 80;
            node.Style.Height = 40;
            node.CalculateLayout();
            node.Reset();
        }

        Assert.Equal(FlexDirection.Row, node.Style.FlexDirection);
        Assert.Equal(Align.Stretch, node.Style.AlignContent);
        Assert.Equal(Justify.Stretch, node.Style.JustifyContent);
        Assert.Equal(Align.Stretch, node.Style.AlignItems);
        Assert.Equal(StyleLength.Auto, node.Style.FlexBasis);
        Assert.Equal(StyleLength.Auto, node.Style.Width);
        Assert.Equal(StyleLength.Auto, node.Style.Height);

        var root = new LayoutNode();
        root.Style.Width = 100;
        node.Style.Width = 80;
        var sibling = new LayoutNode();
        sibling.Style.Width = 80;
        root.AddChild(node);
        root.AddChild(sibling);
        root.CalculateLayout();
        Assert.Equal(50f, node.LayoutWidth);
        Assert.Equal(50f, sibling.LayoutLeft);
        Assert.Equal(50f, sibling.LayoutWidth);

        root.Style.Width = 200;
        root.CalculateLayout();
        Assert.Equal(80f, node.LayoutWidth);
        Assert.Equal(80f, sibling.LayoutLeft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FractionalSizesAreNotRounded(bool measured)
    {
        var node = new LayoutNode();
        if (measured)
        {
            node.MeasureFunc = static (_, _, _, _, _) => new LayoutSize(20.4f, 10.4f);
        }
        else
        {
            node.Style.Width = 20.4f;
            node.Style.Height = 10.4f;
        }

        node.CalculateLayout();
        Assert.Equal(20.4f, node.LayoutWidth, 0.001f);
        Assert.Equal(10.4f, node.LayoutHeight, 0.001f);
        node.CalculateLayout();
        Assert.Equal(20.4f, node.LayoutWidth, 0.001f);
        Assert.Equal(10.4f, node.LayoutHeight, 0.001f);
    }

    [Theory]
    [InlineData(-1f, 10.4f, 0f, 10.4f)]
    [InlineData(float.NaN, 10.4f, 0f, 10.4f)]
    [InlineData(20.4f, -1f, 20.4f, 0f)]
    [InlineData(20.4f, float.NaN, 20.4f, 0f)]
    [InlineData(-1f, float.NaN, 0f, 0f)]
    [InlineData(float.NaN, -1f, 0f, 0f)]
    public void InvalidMeasuredDimensionsAreSanitized(float width, float height, float expectedWidth, float expectedHeight)
    {
        var node = new LayoutNode { MeasureFunc = (_, _, _, _, _) => new LayoutSize(width, height) };
        var size = node.Measure(float.NaN, MeasureMode.Undefined, float.NaN, MeasureMode.Undefined);
        Assert.Equal(expectedWidth, size.Width);
        Assert.Equal(expectedHeight, size.Height);

        node.CalculateLayout();
        Assert.Equal(expectedWidth, node.LayoutWidth, 0.001f);
        Assert.Equal(expectedHeight, node.LayoutHeight, 0.001f);
    }

    [Fact]
    public void LayoutCacheIncludesPercentageReferenceSize()
    {
        var node = new LayoutNode();
        node.Style.Width = 100;
        node.Style.Height = 20;
        node.Style.SetPadding(Edge.Left, StyleLength.Percent(10));

        node.CalculateLayout(200, 100);
        Assert.Equal(20, node.LayoutPadding(PhysicalEdge.Left));

        node.CalculateLayout(400, 100);
        Assert.Equal(40, node.LayoutPadding(PhysicalEdge.Left));
    }

    [Fact]
    public void MeasurementCacheIncludesPercentageReferenceSize()
    {
        var node = new LayoutNode();
        node.Style.SetPadding(Edge.Left, StyleLength.Percent(10));
        var measureCalls = 0;
        node.MeasureFunc = (_, _, _, _, _) =>
        {
            measureCalls++;
            return new LayoutSize(10, 10);
        };
        node.ProcessDimensions();
        var generation = LayoutNode.NextGeneration();

        LayoutAlgorithm.CalculateLayoutInternal(node, 100, 20, Direction.LTR, SizingMode.FitContent, SizingMode.FitContent, 200, 100, false, 0, generation);
        node.SetDirty(false);
        LayoutAlgorithm.CalculateLayoutInternal(node, 100, 20, Direction.LTR, SizingMode.FitContent, SizingMode.FitContent, 400, 100, false, 0, generation);

        Assert.Equal(2, measureCalls);
        Assert.Equal(40, node.LayoutPadding(PhysicalEdge.Left));
    }

    [Fact]
    public void LayoutCacheRestoresOverflowResult()
    {
        var root = new LayoutNode();
        root.Style.Width = 100;
        root.Style.Height = 20;
        var child = new LayoutNode();
        child.Style.Width = 200;
        child.Style.FlexShrink = 0;
        root.AddChild(child);

        root.CalculateLayout();
        Assert.True(root.LayoutHadOverflow);

        root.Layout.HadOverflow = false;
        root.CalculateLayout();
        Assert.True(root.LayoutHadOverflow);
    }

    [Fact]
    public void ReentrantLayoutDoesNotLeakMeasurementTaint()
    {
        var inner = new LayoutNode();
        inner.Style.MaxWidth = 0;
        inner.AddChild(new LayoutNode());

        var measureCalls = 0;
        var outer = new LayoutNode();
        outer.MeasureFunc = (_, _, _, _, _) =>
        {
            measureCalls++;
            inner.CalculateLayout();
            return new LayoutSize(10, 10);
        };

        outer.CalculateLayout();
        Assert.True(outer.Layout.CachedLayout.ContentBased);
        outer.CalculateLayout();

        Assert.Equal(1, measureCalls);
    }
}
