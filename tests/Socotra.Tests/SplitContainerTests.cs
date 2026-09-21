using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class SplitContainerTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .splitcontainer { width: 408px; height: 208px; flex-grow: 0; }
        """;

    private static void Update(RootPanel root)
    {
        root.Update(new Rect(0, 0, 1920, 1080), 0.016f);
        root.Update(new Rect(0, 0, 1920, 1080), 0.016f);
    }

    [Fact]
    public void DraggingTheSplitterResizesThePanes()
    {
        var root = Root(Styles);
        var split = root.AddChild<SplitContainer>();
        Update(root);
        Assert.Equal(200, split.Left.Box.Rect.Width);
        Assert.Equal(200, split.Right.Box.Rect.Width);

        root.SetMousePosition(split.Splitter.Box.Rect.Center);
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Assert.True(split.IsDragging);
        Assert.True(split.HasClass("dragging"));

        root.SetMousePosition(new Vector2(102, 100));
        Update(root);
        Update(root);
        Assert.Equal(Length.Percent(25), split.Left.Style.Width);
        Assert.Equal(Length.Percent(75), split.Right.Style.Width);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Assert.False(split.IsDragging);

        root.SetMousePosition(new Vector2(300, 100));
        Update(root);
        Assert.Equal(Length.Percent(25), split.Left.Style.Width);
    }

    [Fact]
    public void TheSplitStaysInsideTheMinimums()
    {
        var split = new SplitContainer { MinimumFractionLeft = 0.3f, MinimumFractionRight = 0.1f };

        split.UpdateSplitFraction(0.05f);
        Assert.Equal(30, split.Left.Style.Width!.Value.Value, 0.001f);

        split.UpdateSplitFraction(0.99f);
        Assert.Equal(90, split.Left.Style.Width!.Value.Value, 0.001f);
        Assert.Equal(10, split.Right.Style.Width!.Value.Value, 0.001f);
    }

    [Fact]
    public void VerticalSplitsShareTheHeight()
    {
        var split = new SplitContainer { Vertical = true };
        Assert.True(split.HasClass("vertical"));

        split.UpdateSplitFraction(0.4f);
        Assert.Equal(40, split.Left.Style.Height!.Value.Value, 0.001f);
        Assert.Equal(60, split.Right.Style.Height!.Value.Value, 0.001f);
        Assert.Null(split.Left.Style.Width);
    }

    [Fact]
    public void MarkupSetsDirectionMinimumsAndStartingSplit()
    {
        var split = Assert.IsType<SplitContainer>(Panel.CreateElement("split"));

        split.SetProperty("direction", "vertical");
        split.SetProperty("min-left", "0.1");
        split.SetProperty("min-right", "0.15");
        split.SetProperty("default-right", "0.3");

        Assert.True(split.Vertical);
        Assert.Equal(0.1f, split.MinimumFractionLeft);
        Assert.Equal(0.15f, split.MinimumFractionRight);
        Assert.Equal(70, split.Left.Style.Height!.Value.Value, 0.001f);
    }

    [Fact]
    public void SlotsPutMarkupChildrenInThePanes()
    {
        var root = Root(Styles);
        var page = root.AddChild<Socotra.Tests.Razor.SplitSlots>();
        Update(root);

        var split = page.Descendants.OfType<SplitContainer>().Single();
        Assert.Same(split.Left, page.Descendants.Single(p => p.HasClass("left-pane")).Parent);
        Assert.Same(split.Right, page.Descendants.Single(p => p.HasClass("right-pane")).Parent);
    }
}
