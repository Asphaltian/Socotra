using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Socotra;

internal enum GradientType
{
    Linear,
    Radial,
    Conic,
}

internal enum GradientCorner
{
    None,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

internal enum RadialSize
{
    FarthestSide,
    FarthestCorner,
    ClosestSide,
    ClosestCorner,
    Circle,
}

internal record struct GradientInfo
{
    public const int MaxStops = 8;

    public float Angle;
    public Length CenterX;
    public Length CenterY;
    public RadialSize Size;
    public GradientType Type;
    public bool Circle;
    public GradientCorner Corner;
    public GradientStops Stops;

    public readonly bool IsEmpty => Stops.Count == 0;
}

internal readonly record struct ColorStop(Color Color, float? Offset, bool OffsetIsPixels = false);

internal struct GradientStops : IEquatable<GradientStops>
{
    private Stops _stops;

    public int Count { readonly get; private set; }

    public readonly ColorStop this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return _stops[index];
        }
    }

    public readonly GradientStops Add(ColorStop stop)
    {
        if (Count == GradientInfo.MaxStops)
        {
            throw new ArgumentOutOfRangeException(nameof(stop), $"Gradients have at most {GradientInfo.MaxStops} stops.");
        }

        var result = this;
        result._stops[Count] = stop;
        result.Count++;
        return result;
    }

    public readonly bool Equals(GradientStops other)
    {
        if (Count != other.Count)
        {
            return false;
        }

        for (int i = 0; i < Count; i++)
        {
            if (_stops[i] != other._stops[i])
            {
                return false;
            }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is GradientStops other && Equals(other);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Count; i++)
        {
            hash.Add(_stops[i]);
        }

        return hash.ToHashCode();
    }

    [InlineArray(GradientInfo.MaxStops)]
    private struct Stops
    {
        private ColorStop _element;
    }
}

internal record struct TextGradientInfo
{
    public float Angle;
    public Length CenterX;
    public Length CenterY;
    public RadialSize Size;
    public GradientType Type;
    public ImmutableArray<ColorStop> Stops;

    public readonly bool IsEmpty => Stops.IsDefaultOrEmpty;

    public readonly bool Equals(TextGradientInfo other) =>
        Angle == other.Angle && CenterX == other.CenterX && CenterY == other.CenterY && Size == other.Size && Type == other.Type
        && Stops.AsSpan().SequenceEqual(other.Stops.AsSpan());

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Angle);
        hash.Add(CenterX);
        hash.Add(CenterY);
        hash.Add(Size);
        hash.Add(Type);
        foreach (var stop in Stops.AsSpan())
        {
            hash.Add(stop);
        }

        return hash.ToHashCode();
    }
}
