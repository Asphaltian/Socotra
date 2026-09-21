namespace Socotra.Tests;

public class AnimationTests
{
    private static (RootPanel Root, Panel Panel) Setup(string styles, string classes = "a")
    {
        var root = new RootPanel();
        root.StyleSheet.Parse(styles);
        var panel = root.AddChild<Panel>(classes);
        Update(root);
        Update(root);
        return (root, panel);
    }

    private static void Update(RootPanel root, float deltaTime = 0) => root.Update(new Rect(0, 0, 1920, 1080), deltaTime);

    [Fact]
    public void TransitionsFromDifferentRulesCombine()
    {
        var (root, panel) = Setup("""
            .a { width: 0px; height: 0px; transition: width 1s linear; }
            .a.big { width: 100px; height: 100px; transition: height 2s linear; }
            """);

        panel.AddClass("big");
        Update(root);
        Update(root, 0.5f);

        Assert.Equal(["width", "height"], panel.ComputedStyle!.Transitions!.List.Select(t => t.Property));
        Assert.Equal(50, panel.ComputedStyle.Width!.Value.Value, 0.01f);
        Assert.Equal(25, panel.ComputedStyle.Height!.Value.Value, 0.01f);
    }

    [Fact]
    public void TransitionDelaysHoldTheStartValue()
    {
        var (root, panel) = Setup(".a { opacity: 1; transition: opacity 1s linear 500ms; } .a.gone { opacity: 0; }");

        panel.AddClass("gone");
        Update(root);
        Update(root, 0.25f);
        Assert.Equal(1, panel.ComputedStyle!.Opacity!.Value, 0.001f);

        Update(root, 0.75f);
        Assert.Equal(0.5f, panel.ComputedStyle.Opacity!.Value, 0.001f);
    }

    [Fact]
    public void AllTransitionsEveryProperty()
    {
        var (root, panel) = Setup(".a { transition: all 1s linear; margin-left: 0px; background-color: rgba(255, 0, 0, 0); } .a.b { margin-left: 10px; background-color: red; font-weight: 700; }");

        panel.AddClass("b");
        Update(root);
        Update(root, 0.5f);

        Assert.Equal(5, panel.ComputedStyle!.MarginLeft!.Value.Value, 0.01f);
        Assert.Equal(1, panel.ComputedStyle.BackgroundColor!.Value.R, 0.01f);
        Assert.Equal(0.5f, panel.ComputedStyle.BackgroundColor.Value.A, 0.01f);
        Assert.Equal(550, panel.ComputedStyle.FontWeight);
    }

    [Fact]
    public void ShorthandAndShadowTransitionsBlendTheirParts()
    {
        var (root, panel) = Setup("""
            .a { padding: 0px; border-radius: 0px; box-shadow: 0px 0px 0px black; transition: padding 1s linear, border-radius 1s linear, box-shadow 1s linear; }
            .a.b { padding: 10px; border-radius: 20px / 10px; box-shadow: 10px 10px 10px black; }
            """);

        panel.AddClass("b");
        Update(root);
        Update(root, 0.5f);
        var style = panel.ComputedStyle!;

        Assert.Equal(5, style.PaddingBottom!.Value.Value, 0.01f);
        Assert.Equal(10, style.BorderTopLeftRadius!.Value.Value, 0.01f);
        Assert.Equal(5, style.BorderTopLeftRadiusV!.Value.Value, 0.01f);
        Assert.Equal(5, Assert.Single(style.BoxShadow!).OffsetX, 0.01f);
    }

    [Fact]
    public void TransformsTransition()
    {
        var (root, panel) = Setup(".a { transform: translateX(0px); transition: transform 1s linear; } .a.b { transform: translateX(100px) rotate(90deg); }");

        panel.AddClass("b");
        Update(root);
        Update(root, 0.5f);

        var point = Vector2.Transform(Vector2.Zero, panel.ComputedStyle!.BuildTransformMatrix(Vector2.Zero));
        Assert.Equal(50, point.X, 0.01f);
    }

    [Fact]
    public void KeyframesAnimateThroughTheirSteps()
    {
        var (root, panel) = Setup("""
            @keyframes grow {
                from { width: 0px; }
                50% { width: 100px; }
                to { width: 200px; }
            }

            .a { animation: grow 1s linear forwards; }
            """);

        Assert.True(panel.ComputedStyle!.IsAnimationActive);
        Assert.True(panel.TryFindKeyframe("GROW", out var frames));
        Assert.Equal([0f, 0.5f, 1f], frames.Blocks.Select(b => b.Interval));

        Update(root, 0.25f);
        Assert.Equal(50, panel.ComputedStyle.Width!.Value.Value, 0.01f);

        Update(root, 0.5f);
        Assert.Equal(150, panel.ComputedStyle.Width!.Value.Value, 0.01f);

        Update(root, 1);
        Assert.Equal(200, panel.ComputedStyle.Width!.Value.Value, 0.01f);
        Assert.False(panel.ComputedStyle.IsAnimationActive);
    }

    [Fact]
    public void KeyframeSelectorsCanShareABlock()
    {
        var (root, panel) = Setup("""
            @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0; } }
            .a { animation: pulse 2s linear infinite; }
            """);

        Update(root, 0.5f);
        Assert.Equal(0.5f, panel.ComputedStyle!.Opacity!.Value, 0.01f);

        Update(root, 2);
        Assert.Equal(0.5f, panel.ComputedStyle.Opacity!.Value, 0.01f);
        Assert.True(panel.ComputedStyle.IsAnimationActive);
    }

    [Fact]
    public void AnimationsWithoutADurationLastASecond()
    {
        var (root, panel) = Setup("@keyframes fade { from { opacity: 0; } to { opacity: 1; } } .a { animation-name: fade; animation-timing-function: linear; }");

        Update(root, 0.25f);

        Assert.Equal(0.25f, panel.ComputedStyle!.Opacity!.Value, 0.01f);
    }

    [Fact]
    public void AlternatingAnimationsRunBackwardsEveryOtherTime()
    {
        var (root, panel) = Setup("@keyframes move { from { left: 0px; } to { left: 100px; } } .a { animation: move 1s linear 2 alternate; }");

        Update(root, 1.25f);

        Assert.Equal(75, panel.ComputedStyle!.Left!.Value.Value, 0.01f);
    }

    [Fact]
    public void BackwardsFillShowsTheFirstFrameDuringTheDelay()
    {
        var (root, panel) = Setup("@keyframes move { from { left: 30px; } to { left: 100px; } } .a { animation: move 1s linear 1s backwards; }");

        Update(root, 0.5f);

        Assert.Equal(30, panel.ComputedStyle!.Left!.Value.Value, 0.01f);
    }

    [Fact]
    public void PausedAnimationsHoldStill()
    {
        var (root, panel) = Setup("@keyframes move { from { left: 0px; } to { left: 100px; } } .a { animation: move 1s linear paused; }");

        Update(root, 0.5f);

        Assert.Equal(0, panel.ComputedStyle!.Left!.Value.Value, 0.01f);
    }

    [Fact]
    public void AnimationsCanStartFromCode()
    {
        var (root, panel) = Setup("@keyframes move { from { top: 0px; } to { top: 10px; } }");

        panel.Style.StartAnimation("move", 2);
        Update(root);
        Update(root, 1);

        Assert.Equal(5, panel.ComputedStyle!.Top!.Value.Value, 0.01f);
    }

    [Fact]
    public void MalformedKeyframesAreSkipped()
    {
        var (_, panel) = Setup("@keyframes broken { 10px { width: 1px; } } .a { width: 2px; }");

        Assert.Equal(Length.Pixels(2), panel.ComputedStyle!.Width);
        Assert.False(panel.TryFindKeyframe("broken", out _));
    }
}
