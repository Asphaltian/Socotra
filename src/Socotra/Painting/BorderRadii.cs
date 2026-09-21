namespace Socotra;

internal struct BorderRadii : IEquatable<BorderRadii>
{
    public Vector2 TopLeft;
    public Vector2 TopRight;
    public Vector2 BottomLeft;
    public Vector2 BottomRight;

    public static readonly BorderRadii Zero = default;

    public readonly bool IsZero => this == Zero;

    public readonly Vector4 Horizontal => new(TopLeft.X, TopRight.X, BottomLeft.X, BottomRight.X);

    public readonly Vector4 Vertical => new(TopLeft.Y, TopRight.Y, BottomLeft.Y, BottomRight.Y);

    public readonly float Largest => MathF.Max(
        MathF.Max(MathF.Max(TopLeft.X, TopLeft.Y), MathF.Max(TopRight.X, TopRight.Y)),
        MathF.Max(MathF.Max(BottomLeft.X, BottomLeft.Y), MathF.Max(BottomRight.X, BottomRight.Y)));

    public static bool operator ==(BorderRadii left, BorderRadii right) => left.Equals(right);

    public static bool operator !=(BorderRadii left, BorderRadii right) => !left.Equals(right);

    public static BorderRadii FromCorners(Vector4 radii) => new()
    {
        TopLeft = new Vector2(radii.X),
        TopRight = new Vector2(radii.Y),
        BottomLeft = new Vector2(radii.Z),
        BottomRight = new Vector2(radii.W),
    };

    public readonly BorderRadii Clamped(float width, float height)
    {
        var f = 1.0f;
        f = MinRatio(f, width, TopLeft.X + TopRight.X);
        f = MinRatio(f, width, BottomLeft.X + BottomRight.X);
        f = MinRatio(f, height, TopLeft.Y + BottomLeft.Y);
        f = MinRatio(f, height, TopRight.Y + BottomRight.Y);
        return f >= 1.0f ? this : Scaled(f);
    }

    public readonly BorderRadii Inner(Vector4 borderWidth) => new()
    {
        TopLeft = Shrink(TopLeft, borderWidth.X, borderWidth.Y),
        TopRight = Shrink(TopRight, borderWidth.Z, borderWidth.Y),
        BottomLeft = Shrink(BottomLeft, borderWidth.X, borderWidth.W),
        BottomRight = Shrink(BottomRight, borderWidth.Z, borderWidth.W),
    };

    public readonly BorderRadii Grow(float spread)
    {
        if (spread == 0)
        {
            return this;
        }

        return new BorderRadii
        {
            TopLeft = Spread(TopLeft, spread),
            TopRight = Spread(TopRight, spread),
            BottomLeft = Spread(BottomLeft, spread),
            BottomRight = Spread(BottomRight, spread),
        };
    }

    public readonly bool Equals(BorderRadii other) =>
        TopLeft == other.TopLeft && TopRight == other.TopRight && BottomLeft == other.BottomLeft && BottomRight == other.BottomRight;

    public override readonly bool Equals(object? obj) => obj is BorderRadii other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomLeft, BottomRight);

    private static float MinRatio(float f, float side, float sum) => sum <= side || sum <= 0 ? f : MathF.Min(f, side / sum);

    private static Vector2 Shrink(Vector2 radius, float horizontal, float vertical)
    {
        var x = radius.X - horizontal;
        var y = radius.Y - vertical;
        return x <= 0 || y <= 0 ? Vector2.Zero : new Vector2(x, y);
    }

    private static Vector2 Spread(Vector2 radius, float spread) => new(Spread(radius.X, spread), Spread(radius.Y, spread));

    private static float Spread(float radius, float spread)
    {
        if (spread < 0)
        {
            return MathF.Max(0, radius + spread);
        }

        if (radius >= spread)
        {
            return radius + spread;
        }

        var t = (radius / spread) - 1.0f;
        return radius + (spread * (1.0f + (t * t * t)));
    }

    private readonly BorderRadii Scaled(float scale) => new()
    {
        TopLeft = TopLeft * scale,
        TopRight = TopRight * scale,
        BottomLeft = BottomLeft * scale,
        BottomRight = BottomRight * scale,
    };
}
