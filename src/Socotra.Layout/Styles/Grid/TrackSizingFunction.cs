namespace Socotra.Layout;

internal readonly struct TrackSizingFunction : IEquatable<TrackSizingFunction>
{
    public readonly TrackBreadth Min;
    public readonly TrackBreadth Max;

    public readonly StyleLength FitContentLimit;

    public TrackSizingFunction(TrackBreadth min, TrackBreadth max)
    {
        Min = min.IsFraction ? TrackBreadth.Auto : min;
        Max = max;
        FitContentLimit = StyleLength.Undefined;
    }

    private TrackSizingFunction(StyleLength fitContentLimit)
    {
        Min = TrackBreadth.Auto;
        Max = TrackBreadth.MaxContent;
        FitContentLimit = fitContentLimit;
    }

    public static readonly TrackSizingFunction Auto = Single(TrackBreadth.Auto);
    public static readonly TrackSizingFunction MinContent = Single(TrackBreadth.MinContent);
    public static readonly TrackSizingFunction MaxContent = Single(TrackBreadth.MaxContent);

    public static TrackSizingFunction Single(TrackBreadth breadth) => new(breadth, breadth);
    public static TrackSizingFunction MinMax(TrackBreadth min, TrackBreadth max) => new(min, max);
    public static TrackSizingFunction FitContent(StyleLength limit) => new(limit);
    public static TrackSizingFunction Points(float value) => Single(TrackBreadth.Points(value));
    public static TrackSizingFunction Percent(float value) => Single(TrackBreadth.Percent(value));
    public static TrackSizingFunction Fraction(float value) => new(TrackBreadth.Auto, TrackBreadth.Fraction(value));

    public bool IsFitContent => FitContentLimit.IsDefined;

    public bool IsFlexible => Max.IsFraction;

    public bool IsFixed => Min.IsFixed && Max.IsFixed && !IsFitContent;

    public bool Equals(TrackSizingFunction other)
    {
        return Min.Equals(other.Min)
            && Max.Equals(other.Max)
            && FitContentLimit == other.FitContentLimit;
    }

    public override bool Equals(object? obj) => obj is TrackSizingFunction other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max, FitContentLimit);

    public override string ToString()
    {
        if (IsFitContent)
        {
            return $"fit-content({FitContentLimit})";
        }

        if (Min.Equals(Max))
        {
            return Min.ToString();
        }

        if (Min.IsAuto && Max.IsFraction)
        {
            return Max.ToString();
        }

        return $"minmax({Min}, {Max})";
    }
}
