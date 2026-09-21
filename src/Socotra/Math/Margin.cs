namespace Socotra;

/// <summary>A size for each of the four edges, in pixels, like a panel's padding or border widths.</summary>
public readonly record struct Margin(float Left, float Top, float Right, float Bottom)
{
    /// <summary>A margin of <paramref name="all"/> pixels on every edge.</summary>
    public Margin(float all)
        : this(all, all, all, all)
    {
    }

    /// <summary>How much the edges add up to across (left plus right) and down (top plus bottom).</summary>
    public Vector2 EdgeSize => new(Left + Right, Top + Bottom);

    internal static Margin GetEdges(Vector2 size, Length? left, Length? top, Length? right, Length? bottom) => new(
        left?.GetPixels(size.X) ?? 0,
        top?.GetPixels(size.Y) ?? 0,
        right?.GetPixels(size.X) ?? 0,
        bottom?.GetPixels(size.Y) ?? 0);

    /// <summary>Adds each edge of <paramref name="b"/> to the same edge of <paramref name="a"/>.</summary>
    public static Margin operator +(Margin a, Margin b) => new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);

    /// <summary>Multiplies every edge by <paramref name="scale"/>.</summary>
    public static Margin operator *(Margin margin, float scale) => new(margin.Left * scale, margin.Top * scale, margin.Right * scale, margin.Bottom * scale);
}
