using System.Globalization;

namespace Socotra;

/// <summary>
/// A horizontal or vertical axis for a graph: tick marks with number or custom labels, an optional title, and optional
/// boxes at each end where the user can type the axis range. It only shows a range; it doesn't change the graph itself.
/// <see cref="GraphPanel"/> makes and sizes two of these for you. In markup it's <c>&lt;graphaxis&gt;</c>.
/// </summary>
[StyleSheet.Inline("graphaxis", ".graphaxis { position: absolute; overflow: visible; pointer-events: none; } .graphaxis > .graph-axis-bound { position: absolute; min-width: 0; height: 24px; pointer-events: all; } .graphaxis > .graph-axis-bound .content-label { width: 100%; }")]
public class GraphAxis : Panel
{
    private readonly NumberEntry _minimum;
    private readonly NumberEntry _maximum;
    private GraphPanel.Axis _orientation;

    /// <summary>Makes a horizontal axis from 0 to 1.</summary>
    public GraphAxis()
    {
        AddClass("graphaxis");
        _minimum = AddBound(false);
        _maximum = AddBound(true);
        UpdateBoundClasses();
    }

    /// <summary>Called when the user types a new end for the axis, with true for the maximum end and the number they typed. Apply it however suits your graph.</summary>
    public event Action<bool, float>? BoundEdited;

    /// <summary>Called when the user starts typing in one of the end boxes.</summary>
    public event Action? EditStarted;

    /// <summary>Called when the user leaves one of the end boxes.</summary>
    public event Action? EditFinished;

    /// <summary>Which way values go up along the axis, before <see cref="Reversed"/>.</summary>
    public GraphPanel.Axis Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            UpdateBoundClasses();
        }
    }

    /// <summary>Makes values go up the other way, like up the screen on a vertical axis.</summary>
    public bool Reversed { get; set; }

    /// <summary>The value at the start of the axis.</summary>
    public double Minimum { get; set; }

    /// <summary>The value at the end of the axis.</summary>
    public double Maximum { get; set; } = 1;

    /// <summary>A title shown under a horizontal axis or turned sideways beside a vertical one.</summary>
    public string? Label { get; set; }

    /// <summary>How numbers are written, as a .NET format string like <c>0.00</c> or <c>$#,0</c>. It doesn't change as the graph zooms.</summary>
    public string NumberFormat { get; set; } = "0.0";

    /// <summary>Writes the tick labels your own way. The end boxes still use <see cref="NumberFormat"/>.</summary>
    public Func<double, string>? LabelFormatter { get; set; }

    /// <summary>Ticks of your own, for categories, dates and other labels that aren't plain numbers. Leave it null to space number ticks evenly.</summary>
    public IReadOnlyList<TickMark>? Ticks { get; set; }

    /// <summary>The gap between labeled ticks, in axis units. Leave it null to pick one that suits the range in view.</summary>
    public double? TickInterval { get; set; }

    /// <summary>Turns the tick labels clockwise by this many degrees. Negative angles suit long labels, like month names, on a horizontal axis.</summary>
    public float LabelRotation { get; set; }

    /// <summary>The space each tick label gets, in pixels. It also sets how much room the axis takes beside the graph.</summary>
    public float LabelWidth { get; set; } = 48;

    /// <summary>The size of the tick labels, in pixels.</summary>
    public float FontSize { get; set; } = 10;

    /// <summary>How long the labeled tick marks are, in pixels. Set it to 0 for a graph with only grid lines.</summary>
    public float TickLength { get; set; } = 4;

    /// <summary>Draws a line along the axis.</summary>
    public bool ShowLine { get; set; } = true;

    /// <summary>Shows boxes at the ends of the axis where the user can type the range, in place of the end tick labels.</summary>
    public bool EditableBounds { get; set; }

    /// <summary>The number shown in the start box, when it should differ from <see cref="Minimum"/>.</summary>
    public float? MinimumBound { get; set; }

    /// <summary>The number shown in the end box, when it should differ from <see cref="Maximum"/>.</summary>
    public float? MaximumBound { get; set; }

    /// <summary>How much room the axis needs beside the graph, in pixels, allowing for turned labels and the title.</summary>
    public float Thickness
    {
        get
        {
            float angle = LabelRotation * MathF.PI / 180;
            float extent = Horizontal
                ? (MathF.Abs(MathF.Sin(angle)) * LabelWidth) + (MathF.Abs(MathF.Cos(angle)) * (FontSize + 6))
                : (MathF.Abs(MathF.Cos(angle)) * LabelWidth) + (MathF.Abs(MathF.Sin(angle)) * (FontSize + 6));
            return Math.Max(Horizontal ? 40 : 60, extent + 12) + (string.IsNullOrEmpty(Label) ? 0 : 24);
        }
    }

    internal Color? ColorOverride { get; set; }

    private bool Horizontal => Orientation == GraphPanel.Axis.Horizontal;

    private Color AxisColor => ColorOverride ?? ComputedStyle?.FontColor ?? Color.White;

    /// <summary>How far along the axis <paramref name="value"/> is, from 0 at the start to 1 at the end, allowing for <see cref="Reversed"/>.</summary>
    public float Fraction(double value)
    {
        float fraction = (float)((value - Minimum) / (Maximum - Minimum));
        return Reversed ? 1 - fraction : fraction;
    }

    /// <summary>The ticks in view on an axis <paramref name="length"/> pixels long. The graph's grid lines use the same ticks.</summary>
    public IEnumerable<TickMark> GetTicks(float length)
    {
        if (!double.IsFinite(Minimum) || !double.IsFinite(Maximum) || Maximum <= Minimum)
        {
            yield break;
        }

        if (Ticks is not null)
        {
            foreach (var tick in Ticks)
            {
                if (tick.Value >= Minimum && tick.Value <= Maximum)
                {
                    yield return tick;
                }
            }

            yield break;
        }

        double step = TickInterval ?? TickStep(Maximum - Minimum, length, Horizontal ? Math.Max(90, LabelWidth + 12) : Math.Max(55, FontSize + 12));
        if (!double.IsFinite(step) || step <= 0)
        {
            yield break;
        }

        double minor = step / 5;
        double first = Math.Ceiling(Minimum / minor) * minor;
        for (int i = 0; i < 500; i++)
        {
            double value = first + (i * minor);
            if (value > Maximum)
            {
                break;
            }

            bool major = Math.Abs((value / step) - Math.Round(value / step)) < 0.0001;
            yield return new TickMark(value, major ? LabelFormatter?.Invoke(value) ?? FormatLabel(value, NumberFormat) : null, major);
        }
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        SyncBounds();
    }

    /// <summary>Draws the line, ticks, labels and title.</summary>
    public override void OnDraw(Painter painter)
    {
        using var scope = painter.Scope();
        var size = Box.Rect.Size;
        float length = Horizontal ? size.X : size.Y;
        painter.Stroke = Stroke.Solid(AxisColor.WithAlpha(0.35f), 1);
        if (ShowLine)
        {
            painter.Line(Horizontal ? Vector2.Zero : new Vector2(size.X, 0), Horizontal ? new Vector2(size.X, 0) : size);
        }

        foreach (var tick in GetTicks(length / ScaleToScreen))
        {
            if (!tick.Major)
            {
                continue;
            }

            float position = Fraction(tick.Value) * length;
            var origin = Horizontal ? new Vector2(position, 0) : new Vector2(size.X, position);
            var end = origin + ((Horizontal ? new Vector2(0, TickLength) : new Vector2(-TickLength, 0)) * ScaleToScreen);
            if (TickLength > 0)
            {
                painter.Line(origin, end);
            }

            float margin = EditableBounds ? (Horizontal ? Math.Max(80, LabelWidth) : 16) * ScaleToScreen : 0;
            if ((EditableBounds && (position < margin || position > length - margin)) || tick.Label is not { } label)
            {
                continue;
            }

            using var labelScope = painter.Scope();
            painter.Translate(end + ((Horizontal ? new Vector2(0, 5) : new Vector2(-6, 0)) * ScaleToScreen));
            painter.Rotate(LabelRotation);
            bool centered = Horizontal && LabelRotation == 0;
            bool left = Horizontal && LabelRotation > 0;
            painter.TextStyle = new TextStyle { FontSize = FontSize, Color = AxisColor.WithAlpha(0.65f), Alignment = centered ? TextFlag.Center : left ? TextFlag.LeftCenter : TextFlag.RightCenter };
            float width = LabelWidth * ScaleToScreen;
            painter.Text(label, new Rect(centered ? -width / 2 : left ? 0 : -width, Horizontal && LabelRotation == 0 ? 0 : -8 * ScaleToScreen, width, 16 * ScaleToScreen));
        }

        if (string.IsNullOrEmpty(Label))
        {
            return;
        }

        painter.TextStyle = new TextStyle { FontSize = FontSize + 2, Color = AxisColor, Alignment = TextFlag.Center };
        if (Horizontal)
        {
            painter.Text(Label, new Rect(0, size.Y - (22 * ScaleToScreen), size.X, 20 * ScaleToScreen));
            return;
        }

        painter.Translate(new Vector2(10 * ScaleToScreen, size.Y / 2));
        painter.Rotate(-90);
        painter.Text(Label, new Rect(-size.Y / 2, -10 * ScaleToScreen, size.Y, 20 * ScaleToScreen));
    }

    internal static double TickStep(double span, double pixels, double spacing)
    {
        double rough = span / Math.Max(2, pixels / spacing);
        if (!double.IsFinite(rough) || rough <= 0)
        {
            return 1;
        }

        double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double mantissa = rough / power;
        return (mantissa <= 1 ? 1 : mantissa <= 2 ? 2 : mantissa <= 2.5 ? 2.5 : mantissa <= 5 ? 5 : 10) * power;
    }

    internal static string FormatLabel(double value, string format)
    {
        var text = value.ToString(format, CultureInfo.InvariantCulture);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var rounded) && rounded == 0
            ? 0d.ToString(format, CultureInfo.InvariantCulture)
            : text;
    }

    private NumberEntry AddBound(bool maximum)
    {
        var entry = AddChild<NumberEntry>("graph-axis-bound");
        entry.Tooltip = maximum ? "Axis maximum" : "Axis minimum";
        entry.OnTextEdited = text =>
        {
            if (Translation.TryParseTypedNumber(text, out var value) && float.IsFinite(value))
            {
                BoundEdited?.Invoke(maximum, value);
            }
        };
        entry.AddEventListener("onfocus", () =>
        {
            entry.Text = BoundValue(maximum).ToString("G9", CultureInfo.InvariantCulture);
            EditStarted?.Invoke();
        });
        entry.AddEventListener("onblur", () =>
        {
            EditFinished?.Invoke();
            SyncBounds();
        });
        return entry;
    }

    private float BoundValue(bool maximum) => maximum ? MaximumBound ?? (float)Maximum : MinimumBound ?? (float)Minimum;

    private void UpdateBoundClasses()
    {
        _minimum.SetClass("axis-x-min", Horizontal);
        _minimum.SetClass("axis-y-min", !Horizontal);
        _maximum.SetClass("axis-x-max", Horizontal);
        _maximum.SetClass("axis-y-max", !Horizontal);
    }

    private void SyncBounds()
    {
        foreach (var entry in (NumberEntry[])[_minimum, _maximum])
        {
            bool maximum = entry == _maximum;
            entry.Style.Display = EditableBounds ? DisplayMode.Flex : DisplayMode.None;
            float width = Math.Max(54, LabelWidth);
            float fraction = maximum != Reversed ? 1 : 0;
            entry.Style.Width = width;
            entry.Style.Left = Horizontal ? fraction * ((Box.Rect.Width / ScaleToScreen) - width) : (Box.Rect.Width / ScaleToScreen) - width - 6;
            entry.Style.Top = Horizontal ? 4 : (fraction * Box.Rect.Height / ScaleToScreen) - 12;
            entry.Style.TextAlign = Horizontal && fraction == 0 ? TextAlign.Left : TextAlign.Right;
            if (!entry.HasFocus)
            {
                entry.Text = FormatLabel(BoundValue(maximum), NumberFormat);
            }
        }
    }

    /// <summary>A tick on an axis: where it is in axis units, its label, and whether it's a labeled tick or a smaller one between them.</summary>
    /// <param name="Value">Where the tick is, in axis units.</param>
    /// <param name="Label">The text shown by the tick. Only labeled ticks are drawn with it.</param>
    /// <param name="Major">Whether the tick is labeled. Smaller ticks between them only show as fainter grid lines.</param>
    public readonly record struct TickMark(double Value, string? Label, bool Major = true);
}
