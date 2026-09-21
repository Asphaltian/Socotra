namespace Socotra;

/// <summary>The kinds of shape a <c>border-shape</c> can be.</summary>
public enum BorderShapeKind
{
    /// <summary>No shape, so the panel keeps its normal box and rounded corners.</summary>
    None,

    /// <summary><c>polygon()</c>: a shape with 3 to 8 corners.</summary>
    Polygon,

    /// <summary><c>circle()</c>.</summary>
    Circle,
}

/// <summary>A corner of a polygon <c>border-shape</c>, measured from the panel's top left. Percentages are of the panel's size.</summary>
public readonly record struct BorderShapePoint(Length X, Length Y);

/// <summary>
/// A <c>border-shape</c>: a polygon or circle the panel is drawn in and clicked in, instead of its box.
/// It doesn't cut off children; use <c>overflow</c> for that.
/// </summary>
public sealed class BorderShape : IEquatable<BorderShape>
{
    /// <summary>The most corners a polygon can have.</summary>
    public const int MaxPoints = 8;

    private readonly BorderShapePoint[] _points;

    private BorderShape(BorderShapeKind kind, BorderShapePoint[] points, Length? circleRadius, Length circleCenterX, Length circleCenterY)
    {
        Kind = kind;
        _points = points;
        CircleRadius = circleRadius;
        CircleCenterX = circleCenterX;
        CircleCenterY = circleCenterY;
    }

    /// <summary>No shape, so the panel keeps its normal box.</summary>
    public static BorderShape None { get; } = new(BorderShapeKind.None, [], null, default, default);

    /// <summary>Whether it's a polygon, a circle or no shape.</summary>
    public BorderShapeKind Kind { get; }

    /// <summary>A polygon's corners, in order. Empty for a circle.</summary>
    public IReadOnlyList<BorderShapePoint> Points => _points;

    /// <summary>A circle's radius, or null to reach the nearest side of the panel.</summary>
    public Length? CircleRadius { get; }

    /// <summary>How far a circle's center is from the panel's left edge.</summary>
    public Length CircleCenterX { get; }

    /// <summary>How far a circle's center is from the panel's top edge.</summary>
    public Length CircleCenterY { get; }

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Kind == BorderShapeKind.None;

    /// <summary>A polygon with the corners in <paramref name="points"/>, in order.</summary>
    /// <example><code>
    /// // A triangle pointing up
    /// panel.Style.BorderShape = BorderShape.Polygon([new(Length.Percent(50), 0), new(Length.Percent(100), Length.Percent(100)), new(0, Length.Percent(100))]);
    /// </code></example>
    /// <exception cref="ArgumentOutOfRangeException">There are fewer than 3 or more than <see cref="MaxPoints"/> corners.</exception>
    public static BorderShape Polygon(ReadOnlySpan<BorderShapePoint> points)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points.Length, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(points.Length, MaxPoints);
        return new BorderShape(BorderShapeKind.Polygon, points.ToArray(), null, default, default);
    }

    /// <summary>A circle centered at <paramref name="centerX"/>, <paramref name="centerY"/>. Pass null for <paramref name="radius"/> to reach the nearest side of the panel.</summary>
    public static BorderShape Circle(Length? radius, Length centerX, Length centerY) =>
        new(BorderShapeKind.Circle, [], radius, centerX, centerY);

    /// <summary>Where a circle's center is and how big its radius is, in pixels, for a panel at <paramref name="rect"/>.</summary>
    public (Vector2 Center, float Radius) ResolveCircle(Rect rect)
    {
        var center = new Vector2(rect.Left + CircleCenterX.GetPixels(rect.Width), rect.Top + CircleCenterY.GetPixels(rect.Height));
        var radius = CircleRadius is { } length
            ? length.GetPixels(MathF.Sqrt((rect.Width * rect.Width) + (rect.Height * rect.Height)) * 0.70710678118f)
            : MathF.Min(MathF.Min(center.X - rect.Left, rect.Right - center.X), MathF.Min(center.Y - rect.Top, rect.Bottom - center.Y));
        return (center, MathF.Max(radius, 0));
    }

    /// <inheritdoc/>
    public bool Equals(BorderShape? other) =>
        ReferenceEquals(this, other)
        || (other is not null && Kind == other.Kind && CircleRadius == other.CircleRadius && CircleCenterX == other.CircleCenterX
            && CircleCenterY == other.CircleCenterY && _points.AsSpan().SequenceEqual(other._points));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as BorderShape);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(CircleRadius);
        hash.Add(CircleCenterX);
        hash.Add(CircleCenterY);
        foreach (var point in _points)
        {
            hash.Add(point);
        }

        return hash.ToHashCode();
    }

    internal static BorderShape? Parse(string value)
    {
        value = value.Trim();
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return None;
        }

        if (value.StartsWith("circle(", StringComparison.OrdinalIgnoreCase) && value[^1] == ')')
        {
            return ParseCircle(value[7..^1]);
        }

        if (!value.StartsWith("polygon(", StringComparison.OrdinalIgnoreCase) || value[^1] != ')')
        {
            return null;
        }

        var points = new List<BorderShapePoint>();
        var p = new Parse(value[8..^1]);
        while (!p.IsEnd)
        {
            var corner = new Parse(p.ReadUntilOrEnd(",", respectParens: true));
            p.Pointer++;
            if (points.Count == MaxPoints || !corner.TryReadLength(out var x) || !corner.TryReadLength(out var y) || !corner.SkipWhitespaceAndNewlines().IsEnd)
            {
                return null;
            }

            points.Add(new BorderShapePoint(x, y));
        }

        return points.Count >= 3 ? Polygon([.. points]) : null;
    }

    internal BorderShape Scaled(float amount)
    {
        if (Kind == BorderShapeKind.None || amount == 1)
        {
            return this;
        }

        return new BorderShape(
            Kind,
            [.. _points.Select(point => new BorderShapePoint(point.X.Scaled(amount), point.Y.Scaled(amount)))],
            CircleRadius?.Scaled(amount),
            CircleCenterX.Scaled(amount),
            CircleCenterY.Scaled(amount));
    }

    private static BorderShape? ParseCircle(string contents)
    {
        Length? radius = null;
        var center = Length.Percent(50);
        var p = new Parse(contents);
        p.SkipWhitespaceAndNewlines();
        if (!p.IsEnd && !p.Is("at", ignoreCase: true))
        {
            if (!p.TryReadLength(out var r) || (r.Unit != LengthUnit.Expression && r.Value < 0))
            {
                return null;
            }

            radius = r;
            p.SkipWhitespaceAndNewlines();
        }

        if (p.IsEnd)
        {
            return Circle(radius, center, center);
        }

        if (!p.TrySkip("at", ignoreCase: true) || !(p.IsWhitespace || p.IsNewline) || !p.TryReadLength(out var x) || !p.TryReadLength(out var y)
            || !p.SkipWhitespaceAndNewlines().IsEnd)
        {
            return null;
        }

        return Circle(radius, x, y);
    }
}
