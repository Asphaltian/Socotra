namespace Socotra.Layout;

internal readonly record struct GridSpan(int Start, int End)
{
    public int Span => Math.Max(End - Start, 0);
}

internal struct TrackCounts
{
    public int NegativeImplicit;
    public int Explicit;
    public int PositiveImplicit;

    public int Length => NegativeImplicit + Explicit + PositiveImplicit;

    public int ImplicitStartLine => -NegativeImplicit;

    public int ImplicitEndLine => Explicit + PositiveImplicit;

    public int LineToNextTrack(int line) => line + NegativeImplicit;

    public int TrackToPrevLine(int trackIndex) => trackIndex - NegativeImplicit;

    public int TryLineToTrackVecIndex(int line)
    {
        if (line < -NegativeImplicit)
        {
            return -1;
        }

        if (line > Explicit + PositiveImplicit)
        {
            return -1;
        }

        return 2 * (line + NegativeImplicit);
    }

    public int LineToTrackVecIndex(int line)
    {
        var index = TryLineToTrackVecIndex(line);
        if (index < 0)
        {
            throw new InvalidOperationException($"Grid line {line} is outside the grid");
        }

        return index;
    }
}

internal enum GridTrackKind : byte
{
    Track,
    Gutter,
}

internal sealed class GridTrack
{
    public GridTrackKind Kind;
    public bool IsCollapsed;
    public TrackBreadth Min;
    public TrackBreadth Max;
    public StyleLength FitContentLimitLength;

    public float Offset;
    public float BaseSize;
    public float GrowthLimit;
    public float ContentAlignmentAdjustment;
    public float ItemIncurredIncrease;
    public float BaseSizePlannedIncrease;
    public float GrowthLimitPlannedIncrease;
    public bool InfinitelyGrowable;

    public static GridTrack Create(TrackSizingFunction function)
    {
        return Rent(
            GridTrackKind.Track,
            function.Min,
            function.IsFitContent ? TrackBreadth.MaxContent : function.Max,
            function.FitContentLimit);
    }

    public static GridTrack Gutter(StyleLength size)
    {
        var breadth = size.IsPercent ? TrackBreadth.Percent(size.Value) : TrackBreadth.Points(size.IsPoints ? size.Value : 0);
        return Rent(GridTrackKind.Gutter, breadth, breadth, StyleLength.Undefined);
    }

    private static GridTrack Rent(GridTrackKind kind, TrackBreadth min, TrackBreadth max, StyleLength fitContentLimit)
    {
        var track = GridPool<GridTrack>.Rent();
        track.Kind = kind;
        track.IsCollapsed = false;
        track.Min = min;
        track.Max = max;
        track.FitContentLimitLength = fitContentLimit;
        track.Offset = 0;
        track.BaseSize = 0;
        track.GrowthLimit = 0;
        track.ContentAlignmentAdjustment = 0;
        track.ItemIncurredIncrease = 0;
        track.BaseSizePlannedIncrease = 0;
        track.GrowthLimitPlannedIncrease = 0;
        track.InfinitelyGrowable = false;
        return track;
    }

    public static void ReturnAll(List<GridTrack> tracks)
    {
        foreach (var track in tracks)
        {
            GridPool<GridTrack>.Return(track);
        }

        tracks.Clear();
    }

    public void Collapse()
    {
        IsCollapsed = true;
        Min = TrackBreadth.Points(0);
        Max = TrackBreadth.Points(0);
        FitContentLimitLength = StyleLength.Undefined;
    }

    public bool IsFitContent => FitContentLimitLength.IsDefined;
    public bool IsFlexible => Max.IsFraction;
    public float FlexFactor => Max.IsFraction ? Max.Value : 0;
    public bool UsesPercentage => Min.Kind == TrackBreadthKind.Percent
        || Max.Kind == TrackBreadthKind.Percent
        || FitContentLimitLength.IsPercent;

    public bool HasIntrinsicSizingFunction => MinIsIntrinsic || MaxIsIntrinsic;

    public bool MinIsIntrinsic => Min.IsIntrinsic;

    public bool MinIsMinOrMaxContent => Min.IsMinContent || Min.IsMaxContent;

    public bool MaxIsIntrinsic => Max.IsIntrinsic || IsFitContent;

    public bool MaxIsMaxContentAlike => !IsFitContent && (Max.IsMaxContent || Max.IsAuto);

    public bool MaxIsMaxOrFitContent => IsFitContent || Max.IsMaxContent;

    public bool MaxIsAuto => !IsFitContent && Max.IsAuto;
    public bool MaxIsMinContent => !IsFitContent && Max.IsMinContent;

    public float MinDefiniteValue(float percentageBasis) => Min.ResolveFixed(percentageBasis);

    public float MaxDefiniteValue(float percentageBasis) => IsFitContent ? Num.Undefined : Max.ResolveFixed(percentageBasis);

    public float MaxDefiniteLimit(float percentageBasis)
    {
        if (IsFitContent)
        {
            return FitContentLimitLength.IsPercent
                ? FitContentLimitLength.Resolve(percentageBasis)
                : FitContentLimitLength.Value;
        }

        return Max.ResolveFixed(percentageBasis);
    }

    public bool MaxHasDefiniteValue(float percentageBasis) => Num.IsDefined(MaxDefiniteValue(percentageBasis));

    public float FitContentLimit(float axisInnerSize)
    {
        if (!IsFitContent)
        {
            return float.PositiveInfinity;
        }

        if (FitContentLimitLength.IsPercent)
        {
            return Num.IsDefined(axisInnerSize)
                ? FitContentLimitLength.Value * axisInnerSize * 0.01f
                : float.PositiveInfinity;
        }

        return FitContentLimitLength.Value;
    }

    public float FitContentLimitedGrowthLimit(float axisInnerSize) => MathF.Min(GrowthLimit, FitContentLimit(axisInnerSize));

    public static float ResolvedPercentageSize(TrackBreadth breadth, float size)
    {
        return breadth.Kind == TrackBreadthKind.Percent
            ? breadth.Value * size * 0.01f
            : Num.Undefined;
    }

    public override string ToString()
    {
        return $"{(Kind == GridTrackKind.Gutter ? "gutter" : "track")} {Min}/{Max} base={BaseSize} limit={GrowthLimit} offset={Offset}";
    }
}
