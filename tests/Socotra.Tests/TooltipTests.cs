using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class TooltipTests
{
    private sealed class CountingPanel : Panel
    {
        public int Builds { get; private set; }

        public override bool HasTooltip => true;

        protected override Panel? CreateTooltipPanel()
        {
            Builds++;
            return null;
        }
    }

    private static RootPanel Build(out Panel a, out Panel b)
    {
        var root = Root();
        a = Placed(root, 0, 0, 100, 100);
        a.Tooltip = "A";
        b = Placed(root, 200, 0, 100, 100);
        b.Tooltip = "B";
        Update(root);
        return root;
    }

    private static string? TextOf(Panel? tooltip) => (tooltip?.Children.FirstOrDefault() as Label)?.Text;

    [Fact]
    public void RestingOnAPanelShowsItsTooltipInTheRoot()
    {
        var root = Build(out var a, out _);
        a.TooltipClass = "dark";

        MoveTo(root, new Vector2(50, 50));

        var tooltip = root.Tooltips.Current;
        Assert.NotNull(tooltip);
        Assert.True(root.Tooltips.IsShowing);
        Assert.Same(root, tooltip.Parent);
        Assert.True(tooltip.HasClass("tooltip") && tooltip.HasClass("dark"));
        Assert.Equal("A", TextOf(tooltip));
    }

    [Fact]
    public void DisabledPanelsShowTheirTooltipWithoutTakingInput()
    {
        var root = Build(out var a, out _);
        a.Disabled = true;
        var clicks = 0;
        a.AddEventListener("onclick", () => clicks++);

        MoveTo(root, new Vector2(50, 50));

        Assert.Equal("A", TextOf(root.Tooltips.Current));
        Assert.Same(root, root.Hovered);

        Press(root, new Vector2(50, 50));

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void AContainersTooltipCoversWhatsInside()
    {
        var root = Build(out var a, out _);
        var child = a.AddChild<Panel>();
        child.Style.Width = 50;
        child.Style.Height = 50;
        Update(root);

        MoveTo(root, new Vector2(10, 10));
        var first = root.Tooltips.Current;
        MoveTo(root, new Vector2(80, 80));

        Assert.Equal("A", TextOf(first));
        Assert.Same(first, root.Tooltips.Current);
    }

    [Fact]
    public void LeavingHidesAndMovingToAnotherPanelSwaps()
    {
        var root = Build(out _, out _);

        MoveTo(root, new Vector2(50, 50));
        var first = root.Tooltips.Current;
        MoveTo(root, new Vector2(250, 50));

        Assert.True(first!.IsDeleting || first.IsDeleted);
        Assert.Equal("B", TextOf(root.Tooltips.Current));

        MoveTo(root, new Vector2(150, 50));
        Assert.False(root.Tooltips.IsShowing);

        root.SetMousePosition(null);
        MoveTo(root, new Vector2(250, 50));
        Assert.True(root.Tooltips.IsShowing);
        root.SetMousePosition(null);
        Update(root);
        Assert.False(root.Tooltips.IsShowing);
    }

    [Fact]
    public void ADelayWaitsForTheMouseToRestExceptRightAfterAnotherTooltip()
    {
        var root = Build(out _, out _);
        root.Tooltips.Delay = 0.5f;

        MoveTo(root, new Vector2(50, 50));
        Assert.False(root.Tooltips.IsShowing);
        Update(root, 0.3f);
        Assert.False(root.Tooltips.IsShowing);
        Update(root, 0.3f);
        Assert.Equal("A", TextOf(root.Tooltips.Current));

        MoveTo(root, new Vector2(250, 50));
        Assert.Equal("B", TextOf(root.Tooltips.Current));

        MoveTo(root, new Vector2(150, 50));
        Update(root, 1);
        MoveTo(root, new Vector2(50, 50));
        Assert.False(root.Tooltips.IsShowing);
    }

    [Fact]
    public void ScrollingAnotherPanelUnderAStillMouseWaitsForItToMove()
    {
        var root = Build(out var a, out _);
        MoveTo(root, new Vector2(50, 50));
        Assert.True(root.Tooltips.IsShowing);

        a.Style.Left = 500;
        var b2 = Placed(root, 0, 0, 100, 100);
        b2.Tooltip = "Under";
        Update(root);
        Update(root);
        Assert.False(root.Tooltips.IsShowing);

        MoveTo(root, new Vector2(51, 50));
        Assert.Equal("Under", TextOf(root.Tooltips.Current));
    }

    [Fact]
    public void OnTooltipAddsToTheTextAndCanStandAlone()
    {
        var root = Build(out var a, out var b);
        a.OnTooltip = tooltip => tooltip.AddChild(new Label { Text = "Detail" });
        b.Tooltip = null;
        b.OnTooltip = tooltip => tooltip.AddChild(new Label { Text = "Only me" });
        Assert.True(b.HasTooltip);

        MoveTo(root, new Vector2(50, 50));
        Assert.Equal(["A", "Detail"], root.Tooltips.Current!.Children.OfType<Label>().Select(label => label.Text));

        MoveTo(root, new Vector2(250, 50));
        Assert.Equal("Only me", TextOf(root.Tooltips.Current));
    }

    [Fact]
    public void TheTextFollowsTheTooltipPropertyWhileShowing()
    {
        var root = Build(out var a, out _);
        MoveTo(root, new Vector2(50, 50));

        a.Tooltip = "After";
        Update(root);

        Assert.Equal("After", TextOf(root.Tooltips.Current));
    }

    [Fact]
    public void APanelThatBuildsNothingIsNotAskedAgain()
    {
        var root = Root();
        var panel = new CountingPanel { Parent = root };
        panel.Style.Width = 100;
        panel.Style.Height = 100;
        Update(root);

        MoveTo(root, new Vector2(50, 50));
        Update(root);
        Update(root);

        Assert.Equal(1, panel.Builds);
        Assert.False(root.Tooltips.IsShowing);
    }

    [Fact]
    public void TheTooltipSitsBesideTheMouseAndStaysOnTheScreen()
    {
        var root = Root();
        var left = Placed(root, 0, 500, 100, 100);
        left.Tooltip = "Left";
        var corner = Placed(root, 1800, 0, 100, 50);
        corner.Tooltip = "Corner";
        Update(root);

        MoveTo(root, new Vector2(50, 550));
        var tooltip = root.Tooltips.Current!;
        Assert.Equal(Length.Pixels(70), tooltip.Style.Left);
        Assert.Equal(Length.Pixels(550), tooltip.Style.Bottom);
        Assert.Null(tooltip.Style.Right);
        Assert.Null(tooltip.Style.Top);

        MoveTo(root, new Vector2(1850, 20));
        tooltip = root.Tooltips.Current!;
        Assert.Equal(Length.Pixels(90), tooltip.Style.Right);
        Assert.Equal(Length.Pixels(40), tooltip.Style.Top);
        Assert.Null(tooltip.Style.Left);
        Assert.Null(tooltip.Style.Bottom);
    }
}
