using System.Runtime.InteropServices;

namespace Socotra;

/// <summary>A rectangle in pixels, like a panel's box on screen. Its edges run straight across and down.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Rect : IEquatable<Rect>
{
    /// <summary>A rectangle with its top left corner at <paramref name="x"/>, <paramref name="y"/>.</summary>
    public Rect(float x, float y, float width, float height)
    {
        Left = x;
        Top = y;
        Right = x + width;
        Bottom = y + height;
    }

    /// <summary>A rectangle with its top left corner at <paramref name="position"/>.</summary>
    public Rect(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    /// <summary>Where the left edge is across.</summary>
    public float Left { get; set; }

    /// <summary>Where the top edge is down.</summary>
    public float Top { get; set; }

    /// <summary>Where the right edge is across.</summary>
    public float Right { get; set; }

    /// <summary>Where the bottom edge is down.</summary>
    public float Bottom { get; set; }

    /// <summary>How wide it is. Setting it moves the right edge.</summary>
    public float Width
    {
        readonly get => Right - Left;
        set => Right = Left + value;
    }

    /// <summary>How tall it is. Setting it moves the bottom edge.</summary>
    public float Height
    {
        readonly get => Bottom - Top;
        set => Bottom = Top + value;
    }

    /// <summary>The top left corner. Setting it moves the whole rectangle.</summary>
    public Vector2 Position
    {
        readonly get => new(Left, Top);
        set => this = new Rect(value, Size);
    }

    /// <summary>The width and height. Setting it moves the right and bottom edges.</summary>
    public Vector2 Size
    {
        readonly get => new(Width, Height);
        set => (Width, Height) = (value.X, value.Y);
    }

    /// <summary>The middle of the rectangle.</summary>
    public readonly Vector2 Center => new((Left + Right) * 0.5f, (Top + Bottom) * 0.5f);

    /// <summary>The top left corner.</summary>
    public readonly Vector2 TopLeft => new(Left, Top);

    /// <summary>The top right corner.</summary>
    public readonly Vector2 TopRight => new(Right, Top);

    /// <summary>The bottom left corner.</summary>
    public readonly Vector2 BottomLeft => new(Left, Bottom);

    /// <summary>The bottom right corner.</summary>
    public readonly Vector2 BottomRight => new(Right, Bottom);

    /// <summary>Whether <paramref name="point"/> is inside the rectangle.</summary>
    public readonly bool IsInside(Vector2 point) => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>Whether this overlaps <paramref name="other"/> at all. Pass <paramref name="fully"/> as true to ask whether it's entirely inside instead.</summary>
    public readonly bool IsInside(Rect other, bool fully = false) => fully
        ? Left >= other.Left && Top >= other.Top && Right <= other.Right && Bottom <= other.Bottom
        : Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    /// <summary>Whether the rectangles share any area. Touching edges don't count.</summary>
    public readonly bool Overlaps(Rect other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    /// <summary>The smallest rectangle with both points on its corners.</summary>
    public static Rect FromPoints(Vector2 a, Vector2 b)
    {
        var min = Vector2.Min(a, b);
        var max = Vector2.Max(a, b);
        return new Rect(min, max - min);
    }

    /// <summary>Grows this rectangle to cover <paramref name="other"/> as well.</summary>
    public void Add(Rect other)
    {
        Left = MathF.Min(Left, other.Left);
        Top = MathF.Min(Top, other.Top);
        Right = MathF.Max(Right, other.Right);
        Bottom = MathF.Max(Bottom, other.Bottom);
    }

    /// <summary>Grows this rectangle to cover <paramref name="point"/> as well.</summary>
    public void Add(Vector2 point)
    {
        Left = MathF.Min(Left, point.X);
        Top = MathF.Min(Top, point.Y);
        Right = MathF.Max(Right, point.X);
        Bottom = MathF.Max(Bottom, point.Y);
    }

    /// <summary>The area both rectangles cover. Empty if they don't overlap.</summary>
    public static Rect Intersect(Rect a, Rect b)
    {
        var left = MathF.Max(a.Left, b.Left);
        var top = MathF.Max(a.Top, b.Top);
        return new Rect(left, top, MathF.Max(0, MathF.Min(a.Right, b.Right) - left), MathF.Max(0, MathF.Min(a.Bottom, b.Bottom) - top));
    }

    /// <summary>A copy with each edge moved inwards by its own amount.</summary>
    public readonly Rect Shrink(float left, float top, float right, float bottom) =>
        new(Left + left, Top + top, Width - left - right, Height - top - bottom);

    /// <summary>A copy with the edges moved inwards by <paramref name="margin"/>, like taking off padding.</summary>
    public readonly Rect Shrink(Margin margin) => Shrink(margin.Left, margin.Top, margin.Right, margin.Bottom);

    /// <summary>A copy with every edge moved inwards by <paramref name="amount"/>.</summary>
    public readonly Rect Shrink(float amount) => Shrink(amount, amount, amount, amount);

    /// <summary>A copy with each edge moved outwards by its own amount.</summary>
    public readonly Rect Grow(float left, float top, float right, float bottom) => Shrink(-left, -top, -right, -bottom);

    /// <summary>A copy with the edges moved outwards by <paramref name="margin"/>.</summary>
    public readonly Rect Grow(Margin margin) => Grow(margin.Left, margin.Top, margin.Right, margin.Bottom);

    /// <summary>A copy with every edge moved outwards by <paramref name="amount"/>.</summary>
    public readonly Rect Grow(float amount) => Grow(amount, amount, amount, amount);

    /// <summary>A copy with the edges rounded to whole pixels.</summary>
    public readonly Rect Round() => new() { Left = MathF.Round(Left), Top = MathF.Round(Top), Right = MathF.Round(Right), Bottom = MathF.Round(Bottom) };

    /// <summary>A copy with the edges rounded down to whole pixels.</summary>
    public readonly Rect Floor() => new() { Left = MathF.Floor(Left), Top = MathF.Floor(Top), Right = MathF.Floor(Right), Bottom = MathF.Floor(Bottom) };

    /// <summary>A copy with the edges rounded up to whole pixels.</summary>
    public readonly Rect Ceiling() => new() { Left = MathF.Ceiling(Left), Top = MathF.Ceiling(Top), Right = MathF.Ceiling(Right), Bottom = MathF.Ceiling(Bottom) };

    /// <summary>A rectangle of <paramref name="size"/> placed inside this one, at the top left unless <paramref name="align"/> says otherwise.</summary>
    public readonly Rect Align(Vector2 size, TextFlag align)
    {
        var rect = new Rect(Position, size);

        if (align.HasFlag(TextFlag.Right))
        {
            rect.Left = Right - size.X;
            rect.Right = Right;
        }

        if (align.HasFlag(TextFlag.CenterHorizontally))
        {
            rect.Left = Left + ((Width - size.X) * 0.5f);
            rect.Right = rect.Left + size.X;
        }

        if (align.HasFlag(TextFlag.Bottom))
        {
            rect.Top = Bottom - size.Y;
            rect.Bottom = Bottom;
        }

        if (align.HasFlag(TextFlag.CenterVertically))
        {
            rect.Top = Top + ((Height - size.Y) * 0.5f);
            rect.Bottom = rect.Top + size.Y;
        }

        return rect;
    }

    /// <summary>The edges as left, top, right, bottom.</summary>
    public readonly Vector4 ToVector4() => new(Left, Top, Right, Bottom);

    /// <summary>Moves the rectangle by <paramref name="offset"/>.</summary>
    public static Rect operator +(Rect rect, Vector2 offset) => new(rect.Position + offset, rect.Size);

    /// <summary>Moves the rectangle by minus <paramref name="offset"/>.</summary>
    public static Rect operator -(Rect rect, Vector2 offset) => new(rect.Position - offset, rect.Size);

    /// <summary>Multiplies the position and size by <paramref name="scale"/>.</summary>
    public static Rect operator *(Rect rect, float scale) => new(rect.Position * scale, rect.Size * scale);

    /// <inheritdoc/>
    public readonly bool Equals(Rect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Rect other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <inheritdoc/>
    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    /// <inheritdoc/>
    public override readonly string ToString() => FormattableString.Invariant($"{Left},{Top},{Width},{Height}");
}
