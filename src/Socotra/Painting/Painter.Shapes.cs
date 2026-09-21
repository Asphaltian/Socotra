using System.Buffers;
using System.Runtime.InteropServices;

namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Draws a line through <paramref name="points"/> with <see cref="Stroke"/>. It isn't closed; for a closed outline,
    /// use <see cref="Polygon"/> with <see cref="Socotra.Fill.None"/>.
    /// </summary>
    public void Line(ReadOnlySpan<Vector2> points)
        => Path.DrawPolyline(ActiveContext, points, Stroke);

    /// <summary>
    /// Draws a straight line from <paramref name="from"/> to <paramref name="to"/> with <see cref="Stroke"/>.
    /// </summary>
    public void Line(Vector2 from, Vector2 to)
        => Line([from, to]);

    /// <summary>
    /// Draws a closed shape through <paramref name="points"/> with <see cref="Fill"/> and <see cref="Stroke"/>. It can be
    /// concave, and where it crosses itself the overlap is left empty. Throws if you pass fewer than 3 points.
    /// </summary>
    public void Polygon(ReadOnlySpan<Vector2> points)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points.Length, 3);
        if (!Fill.IsTransparent)
        {
            Path.DrawPolygon(ActiveContext, points, Fill);
        }

        if (HasStroke(Stroke))
        {
            Path.DrawPolyline(ActiveContext, points, Stroke, closed: true);
        }
    }

    /// <summary>
    /// Draws a triangle with <see cref="Fill"/> and <see cref="Stroke"/>.
    /// </summary>
    public void Triangle(Vector2 a, Vector2 b, Vector2 c)
        => Polygon([a, b, c]);

    /// <summary>
    /// Draws a four-sided shape with <see cref="Fill"/> and <see cref="Stroke"/>. Pass the corners in order around the edge.
    /// </summary>
    public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        => Polygon([a, b, c, d]);

    /// <summary>
    /// Draws a rectangle with <see cref="Fill"/> and <see cref="Stroke"/>. Pass a number to round every corner by that
    /// radius, or a <see cref="CornerRadii"/> to round each corner differently. Corners too big for the rectangle
    /// shrink to fit, like in CSS.
    /// </summary>
    public void Rect(Rect rect, CornerRadii corners = default) => DrawRect(rect, corners.Resolve(rect));

    internal void DrawRect(Rect rect, BorderRadii radii, BorderShape? shape = null)
    {
        if (!ValidBounds(rect))
        {
            return;
        }

        var context = ActiveContext;
        ref var state = ref context.State;
        var combined = TryGetBoxStroke(state.Stroke, out var border);
        if (!state.Fill.IsTransparent || combined)
        {
            var desc = state.Fill.CreateDescriptor(rect, context);
            desc.Radii = radii;
            SetBorderShape(ref desc, shape);
            if (combined)
            {
                desc.Stroke = border.WithAlphaMultiplied(context.InheritedOpacity);
            }

            Add(context, desc);
        }
        if (!combined)
        {
            StrokeRect(rect, radii, Stroke);
        }
    }

    internal void Rect(in BoxDescriptor descriptor)
    {
        if (!ValidBounds(descriptor.Rect))
        {
            return;
        }

        var context = GetActiveContext();
        context.Batcher.Add(descriptor, context.InheritedOpacity, ResolveBlendMode(descriptor, context.InitialBlendMode));
    }

    internal void Outline(Rect rect, Color color, float width, BorderRadii radii, float offset)
    {
        var context = GetActiveContext();
        context.Batcher.Add(new OutlineDescriptor(rect, color.WithAlphaMultiplied(context.InheritedOpacity), width)
        {
            Radii = radii,
            Offset = offset,
            OverrideBlendMode = context.InitialBlendMode
        }, Matrix4x4.Identity, -1);
    }

    void StrokeRect(Rect rect, BorderRadii radii, Stroke stroke)
    {
        if (!HasStroke(stroke))
        {
            return;
        }

        if (radii.IsZero)
        {
            Path.DrawPolyline(ActiveContext, [rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft], stroke, closed: true);
            return;
        }

        Span<Vector2> sizes = [radii.TopLeft, radii.TopRight, radii.BottomRight, radii.BottomLeft];
        Span<Vector2> centers = [rect.TopLeft + radii.TopLeft,
            rect.TopRight + new Vector2(-radii.TopRight.X, radii.TopRight.Y),
            rect.BottomRight - radii.BottomRight,
            rect.BottomLeft + new Vector2(radii.BottomLeft.X, -radii.BottomLeft.Y)];
        Span<int> counts = stackalloc int[4];
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            counts[i] = sizes[i].X <= 0 ? 1 : CurveSegments(MathF.Max(sizes[i].X, sizes[i].Y), 90) + 1;
            count += counts[i];
        }
        var rented = ArrayPool<Vector2>.Shared.Rent(count);
        try
        {
            var points = rented.AsSpan(0, count);
            int offset = 0;
            for (int i = 0; i < 4; i++)
            {
                SampleArc(points.Slice(offset, counts[i]), centers[i], sizes[i], 180 + i * 90, 90);
                offset += counts[i];
            }
            Path.DrawPolyline(ActiveContext, points, stroke, closed: true);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Draws a circle with <see cref="Fill"/> and <see cref="Stroke"/>.
    /// </summary>
    public void Circle(Vector2 center, float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0)
        {
            return;
        }

        var rect = new Rect(center - new Vector2(radius), new Vector2(radius * 2));
        if (!ValidBounds(rect))
        {
            return;
        }

        FillEllipse(rect, Fill);
        if (HasStroke(Stroke))
        {
            Arc(center, radius, 0, 360);
        }
    }

    /// <summary>
    /// Draws an ellipse that fills <paramref name="rect"/>, with <see cref="Fill"/> and <see cref="Stroke"/>.
    /// </summary>
    public void Circle(Rect rect)
    {
        if (!ValidBounds(rect))
        {
            return;
        }

        var radius = rect.Size * 0.5f;
        FillEllipse(rect, Fill);
        if (!HasStroke(Stroke))
        {
            return;
        }

        int count = CurveSegments(MathF.Max(radius.X, radius.Y), 360);
        var rented = ArrayPool<Vector2>.Shared.Rent(count);
        try
        {
            var points = rented.AsSpan(0, count);
            SampleArc(points, rect.Center, radius, 0, 360, true);
            Path.DrawPolyline(ActiveContext, points, Stroke, closed: true);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Draws an ellipse <paramref name="size"/> wide and high around <paramref name="center"/>, with <see cref="Fill"/>
    /// and <see cref="Stroke"/>.
    /// </summary>
    public void Circle(Vector2 center, Vector2 size)
        => Circle(new Rect(center - size * 0.5f, size));

    /// <summary>
    /// Draws a ring (a circle with a round hole) with <see cref="Fill"/>, and <see cref="Stroke"/> on both edges.
    /// <paramref name="outerRadius"/> must be bigger than <paramref name="innerRadius"/>, or nothing is drawn.
    /// </summary>
    public void Ring(Vector2 center, float innerRadius, float outerRadius)
    {
        if (!IsFinite(center) || !float.IsFinite(innerRadius) || !float.IsFinite(outerRadius)
            || innerRadius < 0 || outerRadius <= innerRadius)
        {
            return;
        }

        if (innerRadius == 0)
        {
            Circle(center, outerRadius);
            return;
        }

        if (!Fill.IsTransparent)
        {
            var width = outerRadius - innerRadius;
            Path.DrawArc(ActiveContext, center, innerRadius + width * 0.5f, 0, 360, new Stroke(Fill, width));
        }
        if (!HasStroke(Stroke))
        {
            return;
        }

        var mask = Stroke.Alignment == Stroke.StrokeAlignment.Center ? null : Path.CircleMask(center, outerRadius, innerRadius);
        Path.DrawArc(ActiveContext, center, outerRadius, 0, 360, Stroke, mask);
        Path.DrawArc(ActiveContext, center, innerRadius, 0, 360, Stroke, mask);
    }

    /// <summary>
    /// Draws part of a ring with <see cref="Fill"/> and <see cref="Stroke"/>, like a segment of a progress circle. It starts
    /// at <paramref name="startAngle"/> and covers <paramref name="sweepAngle"/>, both in degrees clockwise from the right;
    /// a negative sweep goes counterclockwise.
    /// </summary>
    public void Ring(Vector2 center, float innerRadius, float outerRadius, float startAngle, float sweepAngle)
    {
        if (!IsFinite(center) || !float.IsFinite(innerRadius) || !float.IsFinite(outerRadius)
            || innerRadius < 0 || outerRadius <= innerRadius || !float.IsFinite(startAngle)
            || !float.IsFinite(sweepAngle) || sweepAngle == 0)
        {
            return;
        }

        if (MathF.Abs(sweepAngle) >= 360)
        {
            Ring(center, innerRadius, outerRadius);
            return;
        }
        if (innerRadius == 0)
        {
            Pie(center, outerRadius, startAngle, sweepAngle);
            return;
        }

        int count = CurveSegments(outerRadius, sweepAngle) + 1;
        var rented = ArrayPool<Vector2>.Shared.Rent(count * 2);
        try
        {
            var points = rented.AsSpan(0, count * 2);
            startAngle %= 360;
            SampleArc(points[..count], center, new Vector2(outerRadius), startAngle, sweepAngle);
            SampleArc(points[count..], center, new Vector2(innerRadius), startAngle + sweepAngle, -sweepAngle);
            Polygon(points);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Draws part of a circle's edge with <see cref="Stroke"/>. It starts at <paramref name="startAngle"/> and covers
    /// <paramref name="sweepAngle"/>, both in degrees clockwise from the right; a negative sweep goes counterclockwise.
    /// </summary>
    public void Arc(Vector2 center, float radius, float startAngle, float sweepAngle)
        => Path.DrawArc(ActiveContext, center, radius, startAngle, sweepAngle, Stroke);

    /// <summary>
    /// Draws a pie slice with <see cref="Fill"/> and <see cref="Stroke"/>. It starts at <paramref name="startAngle"/> and
    /// covers <paramref name="sweepAngle"/>, both in degrees clockwise from the right; a negative sweep goes counterclockwise.
    /// </summary>
    public void Pie(Vector2 center, float radius, float startAngle, float sweepAngle)
    {
        if (!IsFinite(center) || !float.IsFinite(radius) || radius <= 0
            || !float.IsFinite(startAngle) || !float.IsFinite(sweepAngle) || sweepAngle == 0)
        {
            return;
        }

        if (MathF.Abs(sweepAngle) >= 360)
        {
            Circle(center, radius);
            return;
        }
        int count = CurveSegments(radius, sweepAngle) + 2;
        var rented = ArrayPool<Vector2>.Shared.Rent(count);
        try
        {
            var points = rented.AsSpan(0, count);
            points[0] = center;
            SampleArc(points[1..], center, new Vector2(radius), startAngle, sweepAngle);
            Polygon(points);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Draws a curve from <paramref name="from"/> to <paramref name="to"/> with <see cref="Stroke"/>, bent toward
    /// <paramref name="control"/>.
    /// </summary>
    public void Bezier(Vector2 from, Vector2 control, Vector2 to)
        => Bezier(from, from + (control - from) * (2f / 3f), to + (control - to) * (2f / 3f), to);

    /// <summary>
    /// Draws a curve from <paramref name="from"/> to <paramref name="to"/> with <see cref="Stroke"/>. It leaves toward
    /// <paramref name="control1"/> and arrives from the direction of <paramref name="control2"/>.
    /// </summary>
    public void Bezier(Vector2 from, Vector2 control1, Vector2 control2, Vector2 to)
    {
        if (!HasStroke(Stroke) || !IsFinite(from) || !IsFinite(control1) || !IsFinite(control2) || !IsFinite(to))
        {
            return;
        }

        var points = new List<Vector2> { from };
        FlattenBezier(points, from, control1, control2, to);
        Line(CollectionsMarshal.AsSpan(points));
    }

    static void FlattenBezier(List<Vector2> points, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int depth = 0)
    {
        if (depth == 12 || (DistanceToSegmentSquared(b, a, d) <= 0.0625 && DistanceToSegmentSquared(c, a, d) <= 0.0625))
        {
            points.Add(d);
            return;
        }
        var ab = a * 0.5f + b * 0.5f;
        var bc = b * 0.5f + c * 0.5f;
        var cd = c * 0.5f + d * 0.5f;
        var abc = ab * 0.5f + bc * 0.5f;
        var bcd = bc * 0.5f + cd * 0.5f;
        var middle = abc * 0.5f + bcd * 0.5f;
        FlattenBezier(points, a, ab, abc, middle, depth + 1);
        FlattenBezier(points, middle, bcd, cd, d, depth + 1);
    }

    /// <summary>
    /// Draws a star with <see cref="Fill"/> and <see cref="Stroke"/>. <paramref name="bodyRadius"/> reaches the inner
    /// corners and <paramref name="spikeLength"/> is how far the tips stick out past them; a spike length of 0 gives a
    /// regular polygon. The first tip points up unless you change <paramref name="rotation"/> (degrees clockwise from
    /// the right).
    /// </summary>
    public void Star(Vector2 center, float bodyRadius, float spikeLength, int points = 5, float rotation = -90)
    {
        float outerRadius = bodyRadius + spikeLength;
        if (!IsFinite(center) || !float.IsFinite(bodyRadius) || bodyRadius <= 0
            || !float.IsFinite(spikeLength) || spikeLength < 0 || !float.IsFinite(outerRadius)
            || !float.IsFinite(rotation) || points < 3 || points > 4096)
        {
            return;
        }

        int count = points * 2;
        var rented = ArrayPool<Vector2>.Shared.Rent(count);
        try
        {
            var vertices = rented.AsSpan(0, count);
            float start = (rotation % 360) * MathF.PI / 180;
            for (int i = 0; i < count; i++)
            {
                float angle = start + i * MathF.Tau / count;
                vertices[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (i % 2 == 0 ? outerRadius : bodyRadius);
            }
            Polygon(vertices);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Draws a plus sign with <see cref="Fill"/> and <see cref="Stroke"/>. <paramref name="width"/> is how thick each bar
    /// is and <paramref name="length"/> is how long, end to end.
    /// </summary>
    public void Cross(Vector2 center, float width, float length)
    {
        if (!IsFinite(center) || !float.IsFinite(width) || !float.IsFinite(length)
            || width <= 0 || length <= 0 || width > length)
        {
            return;
        }

        float w = width * 0.5f;
        float l = length * 0.5f;
        Polygon([center + new Vector2(-w, -l), center + new Vector2(w, -l),
            center + new Vector2(w, -w), center + new Vector2(l, -w),
            center + new Vector2(l, w), center + new Vector2(w, w),
            center + new Vector2(w, l), center + new Vector2(-w, l),
            center + new Vector2(-w, w), center + new Vector2(-l, w),
            center + new Vector2(-l, -w), center + new Vector2(-w, -w)]);
    }

    /// <summary>
    /// Draws a check mark with <see cref="Fill"/> and <see cref="Stroke"/>. <paramref name="width"/> is how thick it is
    /// and <paramref name="length"/> how wide; the width can be at most half the length.
    /// </summary>
    public void Tick(Vector2 center, float width, float length)
    {
        if (!IsFinite(center) || !float.IsFinite(width) || !float.IsFinite(length)
            || width <= 0 || length <= 0 || width > length * 0.5f)
        {
            return;
        }

        var a = center + new Vector2(-0.5f, 0) * length;
        var b = center + new Vector2(-0.15f, 0.35f) * length;
        var c = center + new Vector2(0.5f, -0.35f) * length;
        var n1 = (b - a).Normal.Perpendicular;
        var n2 = (c - b).Normal.Perpendicular;
        var miter = (n1 + n2) / (1 + Vector2.Dot(n1, n2)) * (width * 0.5f);
        var offset1 = n1 * (width * 0.5f);
        var offset2 = n2 * (width * 0.5f);
        Polygon([a + offset1, b + miter, c + offset2, c - offset2, b - miter, a - offset1]);
    }

    /// <summary>
    /// Draws an arrow from <paramref name="from"/> pointing at <paramref name="to"/>, with <see cref="Fill"/> and
    /// <see cref="Stroke"/>.
    /// </summary>
    /// <param name="from">Where the arrow starts.</param>
    /// <param name="to">Where the tip is.</param>
    /// <param name="width">How thick the shaft is.</param>
    /// <param name="headSize">How long and wide the head is.</param>
    public void Arrow(Vector2 from, Vector2 to, float width = 4, float headSize = 16)
    {
        var delta = to - from;
        var length = delta.Length();
        if (!float.IsFinite(length) || length <= 0 || !float.IsFinite(width) || width <= 0)
        {
            return;
        }

        if (!float.IsFinite(headSize) || headSize <= 0)
        {
            return;
        }

        var direction = delta / length;
        var head = to - direction * MathF.Min(headSize, length);
        var side = direction.Perpendicular * (width * 0.5f);
        var headSide = direction.Perpendicular * (MathF.Max(headSize, width) * 0.5f);
        Polygon([from - side, head - side, head - headSide, to, head + headSide, head + side, from + side]);
    }

    /// <summary>
    /// Draws <see cref="Stroke"/> just outside <paramref name="rect"/>, like CSS <c>outline</c>. A positive
    /// <paramref name="offset"/> moves it further out; a negative one moves it in.
    /// </summary>
    public void Outline(Rect rect, float cornerRadius = 0, float offset = 0)
        => Outline(rect, new CornerRadii(cornerRadius), offset);

    /// <summary>
    /// Draws <see cref="Stroke"/> just outside <paramref name="rect"/>, with each corner rounded its own way. A positive
    /// <paramref name="offset"/> moves it further out; a negative one moves it in.
    /// </summary>
    public void Outline(Rect rect, CornerRadii corners, float offset = 0)
    {
        if (!HasStroke(Stroke) || !ValidBounds(rect) || !float.IsFinite(offset))
        {
            return;
        }

        var expansion = offset + Stroke.Width * 0.5f;
        var bounds = rect.Grow(expansion);
        if (ValidBounds(bounds))
        {
            StrokeRect(bounds, corners.Resolve(rect).Grow(expansion).Clamped(bounds.Width, bounds.Height), Stroke with { Alignment = Stroke.StrokeAlignment.Center });
        }
    }

    void FillEllipse(Rect rect, Fill fill)
    {
        if (fill.IsTransparent)
        {
            return;
        }

        var radius = rect.Size * 0.5f;
        var desc = fill.CreateDescriptor(rect, ActiveContext);
        desc.Radii = new BorderRadii { TopLeft = radius, TopRight = radius, BottomLeft = radius, BottomRight = radius };
        Add(ActiveContext, desc);
    }

    static double DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
    {
        double dx = (double)to.X - from.X, dy = (double)to.Y - from.Y;
        double px = (double)point.X - from.X, py = (double)point.Y - from.Y;
        var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Clamp((px * dx + py * dy) / length, 0, 1);
        px -= t * dx;
        py -= t * dy;
        return px * px + py * py;
    }

    static bool HasStroke(Stroke stroke) => !stroke.IsDisabled && float.IsFinite(stroke.Width) && stroke.Width > 0 && !stroke.Fill.IsTransparent;

    static bool ValidBounds(Rect rect) => IsFinite(rect.Position)
        && float.IsFinite(rect.Width) && float.IsFinite(rect.Height) && rect.Width > 0 && rect.Height > 0
        && float.IsFinite(rect.Right) && float.IsFinite(rect.Bottom);

    static float ValidRadius(float radius) => float.IsFinite(radius) ? MathF.Max(radius, 0) : 0;

    static int CurveSegments(float radius, float sweep)
    {
        var step = 2 * Math.Acos(Math.Clamp(1 - 0.25 / radius, -1, 1));
        step = Math.Clamp(step, Math.PI / 8192, Math.PI / 2);
        return (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) * (Math.PI / 180) / step), 1, 4096);
    }

    static void SampleArc(Span<Vector2> points, Vector2 center, Vector2 radius, float startAngle, float sweepAngle, bool closed = false)
    {
        var start = (startAngle % 360) * (Math.PI / 180);
        var sweep = sweepAngle * (Math.PI / 180);
        int segments = closed ? points.Length : Math.Max(points.Length - 1, 1);
        for (int i = 0; i < points.Length; i++)
        {
            var angle = start + sweep * i / segments;
            points[i] = center + new Vector2((float)Math.Cos(angle) * radius.X, (float)Math.Sin(angle) * radius.Y);
        }
    }

    /// <summary>
    /// How much each corner of a rectangle is rounded. Each corner takes a horizontal and a vertical radius, so corners
    /// can be oval. A plain number works anywhere one of these is asked for.
    /// </summary>
    public readonly record struct CornerRadii(Vector2 TopLeft, Vector2 TopRight, Vector2 BottomRight, Vector2 BottomLeft)
    {
        /// <summary>
        /// Rounds all four corners by <paramref name="radius"/>.
        /// </summary>
        public CornerRadii(float radius) : this(new Vector2(radius)) { }

        /// <summary>
        /// Rounds all four corners by the same horizontal and vertical radius.
        /// </summary>
        public CornerRadii(Vector2 radius) : this(radius, radius, radius, radius) { }

        /// <summary>
        /// Rounds all four corners by <paramref name="radius"/>.
        /// </summary>
        public static implicit operator CornerRadii(float radius) => new(radius);

        internal BorderRadii Resolve(Rect rect) => new BorderRadii
        {
            TopLeft = Clean(TopLeft),
            TopRight = Clean(TopRight),
            BottomRight = Clean(BottomRight),
            BottomLeft = Clean(BottomLeft),
        }.Clamped(rect.Width, rect.Height);
        static Vector2 Clean(Vector2 radius) => new(ValidRadius(radius.X), ValidRadius(radius.Y));
    }
}
