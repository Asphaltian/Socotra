using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class LengthReferenceTests
{
    private static void Update(RootPanel root, float width = 1920) => root.Update(new Rect(0, 0, width, 1080), 0.016f);

    [Fact]
    public void ARelativeRootFontSizeStartsFromTheInitialSize()
    {
        var root = Root("rootpanel { font-size: 200%; }");

        Update(root);

        Assert.Equal(26 * root.Scale, root.ComputedStyle!.FontSize!.Value.Value, 0.01f);
    }

    [Fact]
    public void CalcFollowsTheParentsSizeFromTheFirstUpdate()
    {
        var root = Root("""
            .parent { width: 400px; height: 100px; flex-shrink: 0; }
            .child { width: calc(100% - 20px); height: 10px; flex-shrink: 0; }
            """);
        var parent = root.AddChild<Panel>("parent");
        var child = parent.AddChild<Panel>("child");

        Update(root);
        Assert.Equal(380, child.Box.Rect.Width);

        parent.Style.Width = 600;
        Update(root);
        Assert.Equal(580, child.Box.Rect.Width);
    }

    [Fact]
    public void CalcFollowsAParentThatResizesWithTheWindow()
    {
        var root = Root("""
            .parent { width: 50%; height: 100px; flex-shrink: 0; }
            .child { width: calc(100% - 20px); height: 10px; flex-shrink: 0; }
            """);
        var child = root.AddChild<Panel>("parent").AddChild<Panel>("child");

        Update(root, 1920);
        Assert.Equal(940, child.Box.Rect.Width);

        Update(root, 1000);
        Assert.Equal(480, child.Box.Rect.Width);
    }

    [Fact]
    public void TwoRootsWithDifferentScalesResolveTheirOwnLengths()
    {
        var small = Root(".box { width: 10vw; height: 1rem; flex-shrink: 0; }");
        var large = Root(".box { width: 10vw; height: 1rem; flex-shrink: 0; }");
        var smallBox = small.AddChild<Panel>("box");
        var largeBox = large.AddChild<Panel>("box");

        small.Update(new Rect(0, 0, 960, 540), 0.016f);
        large.Update(new Rect(0, 0, 1920, 1080), 0.016f);
        small.Update(new Rect(0, 0, 960, 540), 0.016f);

        Assert.Equal(96, smallBox.Box.Rect.Width);
        Assert.Equal(192, largeBox.Box.Rect.Width);
    }
}
