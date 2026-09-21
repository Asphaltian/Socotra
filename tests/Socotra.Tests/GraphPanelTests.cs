using System.Globalization;

namespace Socotra.Tests;

public class GraphPanelTests
{
    private readonly RootPanel _root = new UnscaledRoot();

    private void Frame()
    {
        for (int i = 0; i < 4; i++)
        {
            _root.Update(new Rect(0, 0, 1000, 800), 0.016f);
        }
    }

    private GraphPanel CreateGraph()
    {
        var graph = _root.AddChild<GraphPanel>();
        graph.Style.Width = 600;
        graph.Style.Height = 400;
        graph.SetView(new Vector2(-2, -10), new Vector2(8, 30));
        Frame();
        return graph;
    }

    private static NumberEntry Bound(GraphPanel graph, string name) => graph.Descendants.OfType<NumberEntry>().Single(entry => entry.HasClass(name));

    [Fact]
    public void AxisBoxesAreOptionalAndOnlyReportEdits()
    {
        var graph = CreateGraph();
        var maximum = Bound(graph, "axis-y-max");
        Assert.False(maximum.IsVisible);

        graph.EditableAxes = true;
        Frame();
        Assert.True(maximum.IsVisible);
        Assert.Equal("30.0", maximum.Text);

        var edits = new List<(GraphPanel.Axis Axis, bool Maximum, float Value)>();
        graph.AxisBoundEdited += (axis, upper, value) => edits.Add((axis, upper, value));
        maximum.OnTextEdited!("50");
        maximum.OnTextEdited!("NaN");
        maximum.OnTextEdited!("invalid");

        Assert.Equal([(GraphPanel.Axis.Vertical, true, 50f)], edits);
        Assert.Equal(new Vector2(8, 30), graph.ViewMax);
    }

    [Fact]
    public void AxisBoxesSayWhichEndTheyAre()
    {
        var graph = CreateGraph();
        graph.EditableAxes = true;
        Frame();
        var maximum = Bound(graph, "axis-y-max");
        Assert.Equal("Axis maximum", maximum.Tooltip);
        Assert.Equal("Axis minimum", Bound(graph, "axis-x-min").Tooltip);

        _root.SetMousePosition(maximum.Box.Rect.Center);
        Frame();
        Assert.Equal("Axis maximum", (_root.Tooltips.Current?.Children.FirstOrDefault() as Label)?.Text);
    }

    [Fact]
    public void PlotLeavesRoomForTheAxesAndYGoesUp()
    {
        var graph = CreateGraph();
        var minimum = graph.CanvasToScreen(graph.ViewMin);
        var maximum = graph.CanvasToScreen(graph.ViewMax);
        Assert.Equal(60, minimum.X, 0.01f);
        Assert.Equal(16, maximum.Y, 0.01f);
        Assert.True(minimum.Y > maximum.Y);

        graph.YAxisUp = false;
        Frame();
        Assert.True(graph.HasClass("y-axis-down"));
        Assert.Equal(16, graph.CanvasToScreen(graph.ViewMin).Y, 0.01f);
        var point = new Vector2(3, 12);
        Assert.True((point - graph.ScreenToCanvas(graph.CanvasToScreen(point))).Length() < 0.001f);
    }

    [Fact]
    public void AxisFormatsStayTheSameWhileZooming()
    {
        var graph = CreateGraph();
        graph.EditableAxes = true;
        graph.HorizontalAxisFormat = "0";
        graph.VerticalAxisFormat = "0.00";
        var x = Bound(graph, "axis-x-max");
        var y = Bound(graph, "axis-y-max");
        Frame();
        Assert.Equal("8", x.Text);
        Assert.Equal("30.00", y.Text);

        graph.ZoomAt(graph.CanvasToScreen(Vector2.Zero), 0.5f);
        Frame();
        Assert.Equal("4", x.Text);
        Assert.Equal("15.00", y.Text);
        Assert.Equal("0.00", GraphAxis.FormatLabel(-0.0001, graph.VerticalAxisFormat));
    }

    [Fact]
    public void FocusingABoxShowsTheFullNumber()
    {
        var graph = CreateGraph();
        graph.EditableAxes = true;
        graph.SetView(new Vector2(622.79f, -3.33521f), new Vector2(676.293f, 12.7155f));
        Frame();
        var maximum = Bound(graph, "axis-y-max");
        Assert.Equal("12.7", maximum.Text);

        maximum.Focus();
        Frame();
        Assert.Equal(graph.ViewMax.Y, float.Parse(maximum.Text, CultureInfo.InvariantCulture));

        graph.Focus();
        Frame();
        Assert.Equal("12.7", maximum.Text);
    }

    [Theory]
    [InlineData(1d, 500d, 0.2d)]
    [InlineData(100d, 500d, 20d)]
    [InlineData(0.001d, 500d, 0.0002d)]
    public void TickSpacingUsesAxisUnits(double span, double pixels, double expected) =>
        Assert.Equal(expected, GraphAxis.TickStep(span, pixels, 90), expected * 0.000001);

    [Fact]
    public void TitlesAndTurnedLabelsTakeRoomWithoutMovingTheData()
    {
        var graph = CreateGraph();
        var before = graph.CanvasToScreen(graph.ViewMin);
        graph.VerticalAxis.Label = "Revenue";
        graph.VerticalAxis.LabelWidth = 90;
        graph.HorizontalAxis.Label = "Month";
        graph.HorizontalAxis.LabelWidth = 90;
        graph.HorizontalAxis.LabelRotation = -45;
        Frame();

        var after = graph.CanvasToScreen(graph.ViewMin);
        Assert.True(after.X > before.X);
        Assert.True(after.Y < before.Y);
        Assert.True((graph.ScreenToCanvas(after) - graph.ViewMin).Length() < 0.001f);
        Assert.Equal(2, graph.Children.OfType<GraphAxis>().Count());
    }

    [Fact]
    public void AStandaloneAxisTakesCategoriesCurrencyAndReversal()
    {
        var axis = _root.AddChild<GraphAxis>();
        axis.Style.Width = 600;
        axis.Style.Height = 100;
        axis.Minimum = 0;
        axis.Maximum = 200000;
        axis.NumberFormat = "$#,0";
        axis.TickInterval = 50000;
        var ticks = axis.GetTicks(600).Where(x => x.Major).ToArray();
        Assert.Equal(5, ticks.Length);
        Assert.Equal("$50,000", ticks[1].Label);
        Assert.Equal("$200,000", ticks[^1].Label);

        axis.Maximum = 3;
        axis.Ticks = [new(0.5, "January"), new(1.5, "February"), new(2.5, "March"), new(3.5, "April")];
        axis.Label = "Month";
        axis.LabelRotation = -45;
        Assert.Equal(3, axis.GetTicks(600).Count());
        Assert.Equal("February", axis.GetTicks(600).ElementAt(1).Label);
        Assert.Equal(0.25f, axis.Fraction(0.75), 0.0001f);

        axis.Reversed = true;
        Assert.Equal(0.75f, axis.Fraction(0.75), 0.0001f);
        Frame();
    }

    [Fact]
    public void MarkupMakesGraphsAndAxes()
    {
        Assert.IsType<GraphPanel>(Panel.CreateElement("graphpanel"));
        Assert.IsType<GraphAxis>(Panel.CreateElement("graphaxis"));
    }
}
