using static Socotra.Tests.ControlTesting;

namespace Socotra.Tests;

public class SliderControlTests
{
    private const string Styles = """
        rootpanel { flex-direction: column; align-items: flex-start; pointer-events: all; }
        .slidercontrol { width: 216px; flex-grow: 0; }
        """;

    private static void Update(RootPanel root)
    {
        root.Update(new Rect(0, 0, 1920, 1080), 0.016f);
        root.Update(new Rect(0, 0, 1920, 1080), 0.016f);
    }

    private static Panel Track(SliderControl slider) => slider.Descendants.Single(x => x.HasClass("track"));

    private static Panel Thumb(SliderControl slider) => slider.Descendants.Single(x => x.HasClass("thumb"));

    private static Vector2 OnTrack(SliderControl slider, float fraction)
    {
        var track = Track(slider).Box.Rect;
        return new Vector2(track.Left + (track.Width * fraction), track.Center.Y);
    }

    [Fact]
    public void PressingTheTrackJumpsThereAndDraggingFollowsTheMouse()
    {
        var root = Root(Styles);
        var slider = root.AddChild(new SliderControl(0, 100, 1));
        var values = new List<float>();
        slider.OnValueChanged = values.Add;
        Update(root);

        root.SetMousePosition(OnTrack(slider, 0.25f));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Assert.Equal(25, slider.Value);
        Assert.True(slider.HasActive);

        root.SetMousePosition(OnTrack(slider, 0.8f));
        Update(root);
        Assert.Equal(80, slider.Value);

        root.SetMousePosition(OnTrack(slider, 1.5f));
        Update(root);
        Assert.Equal(100, slider.Value);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        root.SetMousePosition(OnTrack(slider, 0.1f));
        Update(root);
        Assert.Equal(100, slider.Value);
        Assert.Equal([25, 80, 100], values);
    }

    [Fact]
    public void DraggedValuesRoundToTheStepAndStayInRange()
    {
        var slider = new SliderControl(-10, 10, 5);
        var root = Root(Styles);
        root.AddChild(slider);
        Update(root);

        Assert.Equal(-10, slider.ScreenPosToValue(OnTrack(slider, -1)));
        Assert.Equal(10, slider.ScreenPosToValue(OnTrack(slider, 2)));
        Assert.Equal(0, slider.ScreenPosToValue(OnTrack(slider, 0.55f)));
        Assert.Equal(5, slider.ScreenPosToValue(OnTrack(slider, 0.7f)));

        slider.Step = 0;
        Assert.Equal(4, slider.ScreenPosToValue(OnTrack(slider, 0.7f)), 0.01f);
    }

    [Fact]
    public void ThumbAndFillFollowTheValue()
    {
        var slider = new SliderControl(0, 200, 1) { Value = 50 };
        var fill = slider.Descendants.Single(x => x.HasClass("track-active"));

        Assert.Equal(Length.Percent(25), Thumb(slider).Style.Left);
        Assert.Equal(Length.Percent(0), fill.Style.Left);
        Assert.Equal(Length.Percent(25), fill.Style.Width);

        slider.Fill = SliderFill.Right;
        Assert.Equal(Length.Percent(25), fill.Style.Left);
        Assert.Equal(Length.Percent(75), fill.Style.Width);

        slider.Fill = SliderFill.Center;
        Assert.Equal(Length.Percent(25), fill.Style.Left);
        Assert.Equal(Length.Percent(25), fill.Style.Width);

        slider.Fill = SliderFill.None;
        Assert.Equal(DisplayMode.None, fill.Style.Display);

        slider.Max = 100;
        Assert.Equal(Length.Percent(50), Thumb(slider).Style.Left);
    }

    [Fact]
    public void TypingInTheEntrySetsTheValue()
    {
        var root = Root(Styles);
        var slider = root.AddChild(new SliderControl(0, 10, 1) { ShowTextEntry = true, Value = 3 });
        var entry = slider.Descendants.OfType<NumberEntry>().Single();
        float? reported = null;
        slider.OnValueChanged = value => reported = value;
        Update(root);
        Assert.Equal("3", entry.Text);

        entry.Focus();
        Update(root);
        entry.Text = "";
        root.TypeText("7.5");
        Update(root);

        Assert.Equal(7.5f, slider.Value);
        Assert.Equal(7.5f, reported);

        entry.Blur();
        Update(root);
        slider.Value = 2;
        Assert.Equal("2", entry.Text);
    }

    [Fact]
    public void TickMarksFollowTheRangeAndRangeLabelsShow()
    {
        var slider = new SliderControl(0, 10, 1) { TickStep = 2.5f, ShowRange = true, NumberFormat = "0.0" };
        var ticks = slider.Descendants.Where(x => x.HasClass("tick")).ToArray();

        Assert.Equal(5, ticks.Length);
        Assert.Equal(Length.Percent(75), ticks[3].Style.Left);
        Assert.Equal(["0.0", "10.0"], slider.Descendants.OfType<Label>().Where(x => x.HasClass("left") || x.HasClass("right")).Select(x => x.Text));

        slider.TickStep = 0;
        Assert.DoesNotContain(slider.Descendants, x => x.HasClass("tick"));
    }

    [Fact]
    public void ValueTooltipShowsOnlyWhileDragging()
    {
        var root = Root(Styles);
        var slider = root.AddChild(new SliderControl(0, 100, 1));
        Update(root);

        root.SetMousePosition(OnTrack(slider, 0.5f));
        Update(root);
        root.SetMouseButton(MouseButtons.Left, true);
        Update(root);
        Update(root);
        var tooltip = Assert.Single(slider.Descendants, x => x.HasClass("value-tooltip"));
        Assert.Equal("50", tooltip.Children.OfType<Label>().Single().Text);

        root.SetMouseButton(MouseButtons.Left, false);
        Update(root);
        Update(root);
        Assert.DoesNotContain(slider.Descendants, x => x.HasClass("value-tooltip"));
    }
}
