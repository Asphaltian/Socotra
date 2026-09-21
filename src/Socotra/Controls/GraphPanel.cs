namespace Socotra;

/// <summary>
/// A graph area the user can pan and zoom, with an axis along the bottom and left and a grid behind. Subclass it and
/// draw your data in <see cref="OnDraw"/> after calling the base method, placing points with <see cref="CanvasPanel.CanvasToScreen"/>.
/// Typing in the axis end boxes only reports the edit through <see cref="AxisBoundEdited"/>; it doesn't change the data or
/// the view. In markup it's <c>&lt;graphpanel&gt;</c>.
/// </summary>
/// <example>
/// <code>
/// // A graph of a list of samples, drawn as a line
/// class SampleGraph : GraphPanel
/// {
///     public List&lt;Vector2&gt; Samples { get; } = [];
///
///     public override void OnDraw(Painter painter)
///     {
///         base.OnDraw(painter);
///         painter.Stroke = Stroke.Solid(Color.White, 2);
///         for (int i = 1; i &lt; Samples.Count; i++)
///         {
///             painter.Line(CanvasToScreen(Samples[i - 1]), CanvasToScreen(Samples[i]));
///         }
///     }
/// }
/// </code>
/// </example>
[StyleSheet.Inline("graphpanel", ".graphpanel { min-width: 0; min-height: 0; }")]
public class GraphPanel : CanvasPanel
{
    /// <summary>Makes a graph showing 0 to 1 on both axes, with Y going up.</summary>
    public GraphPanel()
    {
        AddClass("graphpanel");
        HorizontalAxis = AddAxis(Axis.Horizontal);
        VerticalAxis = AddAxis(Axis.Vertical);
        YAxisUp = true;
        PreserveAspectRatio = false;
    }

    /// <summary>Called when the user types a new end for an axis: which axis, true for its maximum end, and the number in axis units. Apply it however suits your graph.</summary>
    public event Action<Axis, bool, float>? AxisBoundEdited;

    /// <summary>Called when the user starts typing in an axis end box.</summary>
    public event Action? AxisEditStarted;

    /// <summary>Called when the user leaves an axis end box.</summary>
    public event Action? AxisEditFinished;

    /// <summary>The axis along the bottom. Set its ticks, labels and title here.</summary>
    public GraphAxis HorizontalAxis { get; }

    /// <summary>The axis up the left side. Set its ticks, labels and title here.</summary>
    public GraphAxis VerticalAxis { get; }

    /// <summary>How numbers on the horizontal axis are written, as a .NET format string like <c>0.00</c>.</summary>
    public string HorizontalAxisFormat
    {
        get => HorizontalAxis.NumberFormat;
        set => HorizontalAxis.NumberFormat = value;
    }

    /// <summary>How numbers on the vertical axis are written, as a .NET format string like <c>0.00</c>.</summary>
    public string VerticalAxisFormat
    {
        get => VerticalAxis.NumberFormat;
        set => VerticalAxis.NumberFormat = value;
    }

    /// <summary>Shows boxes at the ends of both axes where the user can type the range.</summary>
    public bool EditableAxes
    {
        get => HorizontalAxis.EditableBounds && VerticalAxis.EditableBounds;
        set => HorizontalAxis.EditableBounds = VerticalAxis.EditableBounds = value;
    }

    /// <summary>Draws grid lines at the axes' ticks. On by default.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Draws a frame around the plot. On by default.</summary>
    public bool ShowFrame { get; set; } = true;

    /// <summary>Draws the lines through zero on both axes a little brighter. On by default.</summary>
    public bool ShowZeroLines { get; set; } = true;

    /// <summary>Where the plot is inside the panel, in pixels. The axes take the space around it.</summary>
    protected virtual Rect Plot => new(
        VerticalAxis.Thickness * ScaleToScreen,
        16 * ScaleToScreen,
        Math.Max(1, Box.Rect.Width - ((VerticalAxis.Thickness + 18) * ScaleToScreen)),
        Math.Max(1, Box.Rect.Height - ((HorizontalAxis.Thickness + 16) * ScaleToScreen)));

    /// <inheritdoc/>
    protected override Rect Viewport => Plot;

    /// <summary>The color of the axes and grid. The panel's text color by default.</summary>
    protected virtual Color AxisColor => ComputedStyle?.FontColor ?? Color.White;

    /// <summary>The numbers shown in the start boxes of the horizontal (X) and vertical (Y) axes. The start of the range in view by default.</summary>
    protected virtual Vector2 AxisMinimum => CanvasToAxis(VisibleMinimum);

    /// <summary>The numbers shown in the end boxes of the horizontal (X) and vertical (Y) axes. The end of the range in view by default.</summary>
    protected virtual Vector2 AxisMaximum => CanvasToAxis(VisibleMaximum);

    private Vector2 VisibleMinimum => Vector2.Min(ScreenToCanvas(Plot.Position), ScreenToCanvas(Plot.Position + Plot.Size));

    private Vector2 VisibleMaximum => Vector2.Max(ScreenToCanvas(Plot.Position), ScreenToCanvas(Plot.Position + Plot.Size));

    /// <summary>Turns canvas coordinates into the units the axes show. Override it, along with <see cref="AxisToCanvas"/>, when they differ.</summary>
    public virtual Vector2 CanvasToAxis(Vector2 point) => point;

    /// <summary>Turns axis units into canvas coordinates. Override it, along with <see cref="CanvasToAxis"/>, when they differ.</summary>
    public virtual Vector2 AxisToCanvas(Vector2 point) => point;

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        SetClass("y-axis-down", !YAxisUp);
        var plot = Plot;
        var min = CanvasToAxis(VisibleMinimum);
        var max = CanvasToAxis(VisibleMaximum);
        HorizontalAxis.Minimum = min.X;
        HorizontalAxis.Maximum = max.X;
        VerticalAxis.Minimum = min.Y;
        VerticalAxis.Maximum = max.Y;
        VerticalAxis.Reversed = YAxisUp;
        HorizontalAxis.MinimumBound = AxisMinimum.X;
        HorizontalAxis.MaximumBound = AxisMaximum.X;
        VerticalAxis.MinimumBound = AxisMinimum.Y;
        VerticalAxis.MaximumBound = AxisMaximum.Y;
        HorizontalAxis.ColorOverride = VerticalAxis.ColorOverride = AxisColor;
        HorizontalAxis.Style.Left = plot.Left / ScaleToScreen;
        HorizontalAxis.Style.Top = plot.Bottom / ScaleToScreen;
        HorizontalAxis.Style.Width = plot.Width / ScaleToScreen;
        HorizontalAxis.Style.Height = HorizontalAxis.Thickness;
        VerticalAxis.Style.Left = 0;
        VerticalAxis.Style.Top = plot.Top / ScaleToScreen;
        VerticalAxis.Style.Width = plot.Left / ScaleToScreen;
        VerticalAxis.Style.Height = plot.Height / ScaleToScreen;
    }

    /// <summary>Draws the grid, frame and zero lines. Draw your data after calling it.</summary>
    public override void OnDraw(Painter painter)
    {
        base.OnDraw(painter);
        using var scope = painter.Scope();
        var plot = Plot;
        var color = AxisColor;
        painter.Clip(plot);
        if (ShowGrid)
        {
            DrawGrid(painter, HorizontalAxis, plot, color);
            DrawGrid(painter, VerticalAxis, plot, color);
        }

        painter.Stroke = Stroke.Solid(color.WithAlpha(0.18f), 1);
        painter.Fill = Color.Transparent;
        if (ShowFrame)
        {
            painter.Rect(plot);
        }

        if (ShowZeroLines)
        {
            var origin = AxisToCanvas(Vector2.Zero);
            painter.Line(CanvasToScreen(new Vector2(origin.X, VisibleMinimum.Y)), CanvasToScreen(new Vector2(origin.X, VisibleMaximum.Y)));
            painter.Line(CanvasToScreen(new Vector2(VisibleMinimum.X, origin.Y)), CanvasToScreen(new Vector2(VisibleMaximum.X, origin.Y)));
        }
    }

    private GraphAxis AddAxis(Axis direction)
    {
        var axis = AddChild<GraphAxis>();
        axis.Orientation = direction;
        axis.ShowLine = false;
        axis.TickLength = 0;
        axis.BoundEdited += (maximum, value) => AxisBoundEdited?.Invoke(direction, maximum, value);
        axis.EditStarted += () => AxisEditStarted?.Invoke();
        axis.EditFinished += () => AxisEditFinished?.Invoke();
        return axis;
    }

    private void DrawGrid(Painter painter, GraphAxis axis, Rect plot, Color color)
    {
        bool horizontal = axis.Orientation == Axis.Horizontal;
        foreach (var tick in axis.GetTicks((horizontal ? plot.Width : plot.Height) / ScaleToScreen))
        {
            painter.Stroke = Stroke.None;
            painter.Fill = color.WithAlpha(tick.Major ? 0.10f : 0.035f);
            float fraction = axis.Fraction(tick.Value);
            if (horizontal)
            {
                float x = plot.Left + (plot.Width * fraction);
                painter.Rect(new Rect(x - 0.5f, plot.Top, 1, plot.Height));
            }
            else
            {
                float y = plot.Top + (plot.Height * fraction);
                painter.Rect(new Rect(plot.Left, y - 0.5f, plot.Width, 1));
            }
        }
    }

    /// <summary>One of a graph's two axes.</summary>
    public enum Axis
    {
        /// <summary>The axis along the bottom.</summary>
        Horizontal,

        /// <summary>The axis up the left side.</summary>
        Vertical,
    }
}
