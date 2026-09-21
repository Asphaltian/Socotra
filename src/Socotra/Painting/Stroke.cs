namespace Socotra;

/// <summary>
/// The line drawn around shapes, and along lines, arcs and curves. Set it on <see cref="Painter.Stroke"/>. Make one with
/// <see cref="Solid"/>, <see cref="Dashed"/> or <see cref="Dotted"/>, then change it with the <c>With</c> methods.
/// </summary>
/// <example>
/// <code>
/// // a 2px white outline around a rounded box
/// painter.Stroke = Stroke.Solid(Color.White, 2);
/// painter.Rect(new Rect(10, 10, 100, 40), 6);
///
/// // a thick dashed line with round ends
/// painter.Stroke = Stroke.Dashed(Color.White, 4, dashLength: 12, gap: 6).WithCap(Stroke.LineCap.Round);
/// painter.Line(new Vector2(10, 80), new Vector2(200, 80));
/// </code>
/// </example>
public readonly record struct Stroke
{
    /// <summary>
    /// Where the stroke sits on the edge of a closed shape. Lines are always centered.
    /// </summary>
    public enum StrokeAlignment
    {
        /// <summary>
        /// Half inside the edge and half outside.
        /// </summary>
        Center,
        /// <summary>
        /// All inside the shape.
        /// </summary>
        Inside,
        /// <summary>
        /// All outside the shape.
        /// </summary>
        Outside
    }

    internal bool IsDisabled => Style is BorderStyle.None or BorderStyle.Hidden;

    /// <summary>
    /// Where the stroke sits on the edge of closed shapes. Centered unless you change it; lines ignore it.
    /// </summary>
    public StrokeAlignment Alignment { get; init; }

    /// <summary>A copy that sits <paramref name="alignment"/> on the edge of closed shapes.</summary>
    public Stroke WithAlignment(StrokeAlignment alignment)
    {
        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        return this with { Alignment = alignment };
    }

    /// <summary>
    /// How the ends of a line look.
    /// </summary>
    public enum LineCap
    {
        /// <summary>
        /// Flat, stopping right at the end point.
        /// </summary>
        Butt,

        /// <summary>
        /// Flat, reaching half the width past the end point.
        /// </summary>
        Square,

        /// <summary>
        /// Rounded, reaching half the width past the end point.
        /// </summary>
        Round,

        /// <summary>
        /// Pointed, with the tip half the width past the end point.
        /// </summary>
        Triangle,

        /// <summary>
        /// An arrowhead twice as wide as the line.
        /// </summary>
        Arrow
    }

    /// <summary>
    /// How corners look where a line bends.
    /// </summary>
    public enum LineJoin
    {
        /// <summary>
        /// Sharp. Very sharp corners are cut off flat; see <see cref="MiterLimit"/>.
        /// </summary>
        Miter,

        /// <summary>
        /// Cut off flat.
        /// </summary>
        Bevel,

        /// <summary>
        /// Rounded.
        /// </summary>
        Round
    }

    /// <summary>
    /// No stroke. Set it on <see cref="Painter.Stroke"/> to draw shapes without an outline.
    /// </summary>
    public static Stroke None => default;

    /// <summary>
    /// A plain line <paramref name="width"/> pixels wide, painted with <paramref name="fill"/>.
    /// </summary>
    public static Stroke Solid(Fill fill, float width = 1) => new(fill, width);

    /// <summary>
    /// A line of round dots, painted with <paramref name="fill"/>.
    /// </summary>
    /// <param name="fill">A color, gradient or image for the dots.</param>
    /// <param name="width">How big each dot is, in pixels.</param>
    /// <param name="gap">The space between dots, in pixels.</param>
    /// <param name="offset">Shifts the dots along the line, in pixels.</param>
    public static Stroke Dotted(Fill fill, float width = 1, float gap = 4, float offset = 0)
        => new(fill, width) { Style = BorderStyle.Dotted, Gap = gap, Offset = offset };

    /// <summary>
    /// A dashed line, painted with <paramref name="fill"/>.
    /// </summary>
    /// <param name="fill">A color, gradient or image for the dashes.</param>
    /// <param name="width">How thick the line is, in pixels.</param>
    /// <param name="dashLength">How long each dash is, in pixels.</param>
    /// <param name="gap">The space between dashes, in pixels.</param>
    /// <param name="offset">Shifts the dashes along the line, in pixels.</param>
    public static Stroke Dashed(Fill fill, float width = 1, float dashLength = 8, float gap = 4, float offset = 0)
        => new(fill, width) { Style = BorderStyle.Dashed, DashLength = dashLength, Gap = gap, Offset = offset };

    /// <summary>
    /// A stroke with nothing set yet, for filling in with an object initializer. Usually you'll want
    /// <see cref="Solid"/>, <see cref="Dashed"/> or <see cref="Dotted"/> instead.
    /// </summary>
    public Stroke()
    {
        DashLength = 8;
        Gap = 4;
    }

    /// <summary>
    /// A plain line <paramref name="width"/> pixels wide, painted with <paramref name="fill"/>, with <paramref name="cap"/> at its ends.
    /// </summary>
    public Stroke(Fill fill, float width = 1, LineCap cap = LineCap.Butt)
    {
        Fill = fill;
        Width = width;
        Cap = cap;
        Join = LineJoin.Round;
        MiterLimit = 4;
        DashLength = 8;
        Gap = 4;
    }

    /// <summary>
    /// A copy with <paramref name="cap"/> at the ends of lines and dashes. Dots stay round.
    /// </summary>
    public Stroke WithCap(LineCap cap) => this with { Cap = cap };

    /// <summary>
    /// What the stroke is painted with: a color, gradient or image. A gradient runs along the whole line, not each dash.
    /// </summary>
    public Fill Fill { get; init; }

    /// <summary>
    /// How thick the stroke is, in pixels. 0 draws nothing.
    /// </summary>
    public float Width { get; init; }

    /// <summary>
    /// How the ends of lines and dashes look. Closed shapes have no ends, and dots stay round.
    /// </summary>
    public LineCap Cap { get; init; }

    /// <summary>
    /// How corners look where the line bends.
    /// </summary>
    public LineJoin Join { get; init; }

    /// <summary>
    /// How far a sharp <see cref="LineJoin.Miter"/> corner may stick out, in half-widths, before it's cut off flat.
    /// </summary>
    public float MiterLimit { get; init; }

    /// <summary>
    /// Solid, dashed or dotted. Other border styles, like <see cref="BorderStyle.Double"/>, only show on
    /// <see cref="Painter.Rect(Socotra.Rect, Painter.CornerRadii)"/> with an <see cref="StrokeAlignment.Inside"/> stroke and
    /// a plain color; everywhere else they draw solid. Drawing throws if a line has too many dashes or dots.
    /// </summary>
    public BorderStyle Style { get; init; }

    /// <summary>
    /// How long each dash is, in pixels.
    /// </summary>
    public float DashLength { get; init; }

    /// <summary>
    /// The space between dashes or dots, in pixels.
    /// </summary>
    public float Gap { get; init; }

    /// <summary>
    /// Shifts the dashes or dots along the line, in pixels. Change it over time to make them crawl.
    /// </summary>
    public float Offset { get; init; }
}
