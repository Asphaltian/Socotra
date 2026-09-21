using System.Collections.ObjectModel;

namespace Socotra.Layout.Tests.Grid;

public class GridRegressionTests
{
    [Fact]
    public void IntrinsicRowRerunUsesRowFlag()
    {
        var root = new LayoutNode();
        root.Style.Display = Display.Grid;
        root.Style.GridTemplateColumns = new TrackList(TrackSizingFunction.Percent(50));
        root.Style.GridTemplateRows = new TrackList(TrackSizingFunction.Auto);

        var child = new LayoutNode { MeasureFunc = WrappingMeasure };
        root.AddChild(child);

        root.CalculateLayout(float.NaN, float.NaN, Direction.LTR);

        Assert.Equal(20, root.LayoutHeight, 0.01f);
        Assert.Equal(20, child.LayoutHeight, 0.01f);
    }

    private static LayoutSize WrappingMeasure(LayoutNode node, float width, MeasureMode widthMode, float height, MeasureMode heightMode)
    {
        var measuredWidth = widthMode switch
        {
            MeasureMode.Exactly => width,
            MeasureMode.AtMost => MathF.Min(width, 100),
            _ => 100,
        };
        var measuredHeight = measuredWidth < 100 ? 20.0f : 10.0f;
        if (heightMode == MeasureMode.Exactly)
        {
            measuredHeight = height;
        }

        return new LayoutSize(measuredWidth, measuredHeight);
    }

    [Fact]
    public void RepeatLeadingNamesAppearAtEveryRepeatBoundary()
    {
        Assert.True(GridParser.TryParseTrackList("[outer] repeat(2, [inner] 40px [end])", out var template));
        Assert.Equal("[outer] repeat(2, [inner] 40px [end])", template.ToString());

        var root = new LayoutNode();
        root.Style.Display = Display.Grid;
        root.Style.Width = StyleLength.Points(80);
        root.Style.Height = StyleLength.Points(20);
        root.Style.GridTemplateColumns = template;
        root.Style.JustifyItems = Align.FlexStart;
        root.Style.AlignItems = Align.FlexStart;

        var child = new LayoutNode();
        child.Style.Width = StyleLength.Points(10);
        child.Style.Height = StyleLength.Points(10);
        child.Style.GridColumnStart = GridPlacement.NamedLine("inner", 2);
        root.AddChild(child);

        root.CalculateLayout(float.NaN, float.NaN, Direction.LTR);

        Assert.Equal(40, child.LayoutLeft, 0.01f);
    }

    [Fact]
    public void TrackRepresentationsDefensivelyCopyAndExposeReadOnlyCollections()
    {
        var leading = new[] { "before" };
        var repeatLeading = new[] { "repeat-start" };
        var tracks = new[] { TrackSizingFunction.Points(10) };
        var trailing = new[] { new[] { "after" } };
        var component = new TrackTemplateComponent(RepetitionKind.Count, 2, tracks, leading, trailing, repeatLeading);
        var components = new[] { component };
        var list = new TrackList(components);

        leading[0] = "changed";
        repeatLeading[0] = "changed";
        tracks[0] = TrackSizingFunction.Points(99);
        trailing[0][0] = "changed";
        components[0] = new TrackTemplateComponent(TrackSizingFunction.Auto);

        Assert.Equal("before", component.LeadingNames[0]);
        Assert.Equal("repeat-start", component.RepeatLeadingNames[0]);
        Assert.Equal(TrackSizingFunction.Points(10), component.Tracks[0]);
        Assert.Equal("after", component.TrailingNames[0][0]);
        Assert.Same(component, list.Components[0]);

        Assert.Throws<NotSupportedException>(() => ((IList<string>)component.LeadingNames)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)component.RepeatLeadingNames)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<TrackSizingFunction>)component.Tracks)[0] = TrackSizingFunction.Auto);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)component.TrailingNames[0])[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<ReadOnlyCollection<string>>)component.TrailingNames)[0] = Array.AsReadOnly(new[] { "changed" }));
        Assert.Throws<NotSupportedException>(() => ((IList<TrackTemplateComponent>)list.Components)[0] = components[0]);
    }

    [Fact]
    public void AutoTracksAreDefensivelyCopied()
    {
        var node = new LayoutNode();
        var tracks = new[] { TrackSizingFunction.Points(10) };
        node.Style.GridAutoColumns = tracks;

        tracks[0] = TrackSizingFunction.Points(20);
        var returned = node.Style.GridAutoColumns;
        returned[0] = TrackSizingFunction.Points(30);

        Assert.Equal(TrackSizingFunction.Points(10), node.Style.GridAutoColumns[0]);
    }

    [Fact]
    public void RepeatCountsAreCappedWithoutOverflow()
    {
        var component = new TrackTemplateComponent(RepetitionKind.Count, int.MaxValue,
            new[] { TrackSizingFunction.Points(10), TrackSizingFunction.Points(10) }, null, null);
        var template = new TrackList(new[] { component });
        var size = GridLayout.ComputeExplicitGridSize(template, StyleLength.Points(0), 100, true);
        Assert.Equal(10000, size.TrackCount);

        var node = new LayoutNode();
        node.Style.Display = Display.Grid;
        node.Style.Width = 100;
        node.Style.GridTemplateColumns = template;
        var child = new LayoutNode();
        child.Style.GridColumnStart = GridPlacement.Line(-2);
        child.Style.GridColumnEnd = GridPlacement.Line(-1);
        child.Style.Height = 10;
        node.AddChild(child);

        node.CalculateLayout();

        Assert.Equal(99990f, child.LayoutLeft, 0.01f);
        Assert.Equal(10f, child.LayoutWidth, 0.01f);
        Assert.Equal(10f, child.LayoutHeight, 0.01f);
    }

    [Fact]
    public void RepeatComponentsRequireTracks()
    {
        Assert.Throws<ArgumentException>(() => new TrackTemplateComponent(
            RepetitionKind.Count, int.MaxValue, Array.Empty<TrackSizingFunction>(), null, null, new[] { "line" }));
    }

    [Theory]
    [InlineData("40px 60px 80px", 200f, 120f, 80f)]
    [InlineData("1fr 2fr 1fr", 200f, 155f, 45f)]
    [InlineData("repeat(auto-fill, 40px)", 200f, 150f, 40f)]
    public void EmptyTracksDefineAbsoluteGridAreas(string columns, float width, float expectedLeft, float expectedWidth)
    {
        Assert.True(GridParser.TryParseTrackList(columns, out var template));
        var root = new LayoutNode();
        root.Style.Display = Display.Grid;
        root.Style.Width = width;
        root.Style.Height = 40;
        root.Style.JustifyContent = Justify.FlexStart;
        root.Style.GridTemplateColumns = template;
        root.Style.SetGap(Gutter.Column, 10);

        var probe = new LayoutNode();
        probe.Style.PositionType = PositionType.Absolute;
        probe.Style.GridColumnStart = GridPlacement.Line(-2);
        probe.Style.GridColumnEnd = GridPlacement.Line(-1);
        probe.Style.SetPosition(Edge.Left, 0);
        probe.Style.SetPosition(Edge.Right, 0);
        probe.Style.Height = 10;
        root.AddChild(probe);
        root.CalculateLayout();

        Assert.Equal(expectedLeft, probe.LayoutLeft, 0.001f);
        Assert.Equal(expectedWidth, probe.LayoutWidth, 0.001f);
    }
}
