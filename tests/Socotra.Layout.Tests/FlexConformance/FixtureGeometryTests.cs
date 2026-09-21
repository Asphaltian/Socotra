namespace Socotra.Layout.Tests.FlexConformance;

public class FixtureGeometryTests
{
    [Fact]
    public void AccumulatesUnroundedAncestorsWithoutChangingLayoutOrCache()
    {
        var root = Box(0.4f, 100);
        var child = Box(0.4f, 10);
        var grandchild = Box(0.4f, 0.4f);
        root.AddChild(child);
        child.AddChild(grandchild);
        root.CalculateLayout();

        var raw = FixtureGeometry.GetRect(grandchild, false);
        Assert.Equal((0.4f, 0.4f, 0.4f, 0.4f), raw);
        var generation = grandchild.Layout.GenerationCount;
        var cachedLayout = grandchild.Layout.CachedLayout;
        root.HasNewLayout = child.HasNewLayout = grandchild.HasNewLayout = false;

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal((0f, 0f, 1f, 1f), FixtureGeometry.GetRect(grandchild, true));
            Assert.Equal(raw, FixtureGeometry.GetRect(grandchild, false));
            Assert.Equal(generation, grandchild.Layout.GenerationCount);
            Assert.Equal(cachedLayout, grandchild.Layout.CachedLayout);
            Assert.False(root.HasNewLayout || child.HasNewLayout || grandchild.HasNewLayout);
            Assert.False(root.IsDirty || child.IsDirty || grandchild.IsDirty);
        }

        root.CalculateLayout();
        Assert.Equal(raw, FixtureGeometry.GetRect(grandchild, false));
    }

    [Theory]
    [InlineData(1.2f, 2f)]
    [InlineData(1f, 1f)]
    [InlineData(0.99995f, 1f)]
    public void MeasuredTextFloorsPositionsAndPreservesIntegralSizes(float size, float expectedSize)
    {
        var root = Box(0.4f, 100);
        var text = Box(-0.6f, size);
        text.MeasureFunc = (_, _, _, _, _) => new LayoutSize(size, size);
        root.AddChild(text);
        root.CalculateLayout();

        Assert.Equal((-1f, -1f, expectedSize, expectedSize), FixtureGeometry.GetRect(text, true));
        Assert.Equal((-0.6f, -0.6f, size, size), FixtureGeometry.GetRect(text, false));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.49995f, 1f)]
    [InlineData(-0.5002f, -1f)]
    public void RoundsNegativeAndNearHalfPositionsLikeUpstream(float position, float expected)
    {
        var node = Box(position, 1);
        node.CalculateLayout();
        Assert.Equal((expected, expected, 1f, 1f), FixtureGeometry.GetRect(node, true));
    }

    [Fact]
    public void FixedPositionsResetTheAccumulatedOrigin()
    {
        var root = Box(0.4f, 100);
        var fixedNode = Box(0.4f, 10);
        fixedNode.Style.PositionType = PositionType.Fixed;
        var child = Box(0.4f, 0.4f);
        root.AddChild(fixedNode);
        fixedNode.AddChild(child);
        root.CalculateLayout();

        Assert.Equal((0f, 0f, 0f, 0f), FixtureGeometry.GetRect(child, true));
    }

    private static LayoutNode Box(float position, float size)
    {
        var node = new LayoutNode();
        node.Style.PositionType = PositionType.Absolute;
        node.Style.SetPosition(Edge.Left, StyleLength.Points(position));
        node.Style.SetPosition(Edge.Top, StyleLength.Points(position));
        node.Style.Width = StyleLength.Points(size);
        node.Style.Height = StyleLength.Points(size);
        return node;
    }
}
