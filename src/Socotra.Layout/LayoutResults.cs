using System.Runtime.CompilerServices;

namespace Socotra.Layout;

internal struct CachedMeasurement
{
    public float AvailableWidth;
    public float AvailableHeight;
    public float OwnerWidth;
    public float OwnerHeight;
    public float ComputedWidth;
    public float ComputedHeight;
    public SizingMode WidthSizingMode;
    public SizingMode HeightSizingMode;

    public MeasureScope Scope;

    public bool ContentBased;
    public bool HadOverflow;

    public static CachedMeasurement Empty => new()
    {
        AvailableWidth = -1,
        AvailableHeight = -1,
        OwnerWidth = -1,
        OwnerHeight = -1,
        WidthSizingMode = SizingMode.MaxContent,
        HeightSizingMode = SizingMode.MaxContent,
        ComputedWidth = -1,
        ComputedHeight = -1,
        Scope = MeasureScope.Both,
        ContentBased = true,
    };
}

[InlineArray(LayoutResults.MaxCachedMeasurements)]
internal struct CachedMeasurementBuffer
{
    private CachedMeasurement _element0;
}

internal struct LayoutResults
{
    public const int MaxCachedMeasurements = 8;

    public uint ComputedFlexBasisGeneration;
    public float ComputedFlexBasis;

    public uint GenerationCount;
    public Direction LastOwnerDirection;
    public int NextCachedMeasurementsIndex;
    public CachedMeasurementBuffer CachedMeasurements;
    public CachedMeasurement CachedLayout;

    public Direction Direction;
    public bool HadOverflow;

    public bool BaselineSensitive;

    public bool HasSingleStickyFlexChild;
    public uint StickyFlexGeneration;

    public CollapsibleMargin MarginTopSet;
    public CollapsibleMargin MarginBottomSet;
    public bool MarginsCanCollapseThrough;
    public float InlineBaseline;

    public float StaticPositionX;
    public float StaticPositionY;

    private float _width, _height;
    private float _measuredWidth, _measuredHeight;
    private float _left, _top, _right, _bottom;
    private float _marginLeft, _marginTop, _marginRight, _marginBottom;
    private float _borderLeft, _borderTop, _borderRight, _borderBottom;
    private float _paddingLeft, _paddingTop, _paddingRight, _paddingBottom;

    public static LayoutResults Create()
    {
        LayoutResults results = default;
        results.ComputedFlexBasis = Num.Undefined;
        results.LastOwnerDirection = Direction.Inherit;
        for (int i = 0; i < MaxCachedMeasurements; i++)
        {
            results.CachedMeasurements[i] = CachedMeasurement.Empty;
        }

        results.CachedLayout = CachedMeasurement.Empty;
        results.Direction = Direction.Inherit;
        results.InlineBaseline = float.NaN;
        results._width = results._height = results._measuredWidth = results._measuredHeight = Num.Undefined;
        return results;
    }

    public void Reset() => this = Create();

    public readonly float Dimension(Dimension axis) => axis == Layout.Dimension.Width ? _width : _height;

    public void SetDimension(Dimension axis, float value)
    {
        if (axis == Layout.Dimension.Width)
        {
            _width = value;
        }
        else
        {
            _height = value;
        }
    }

    public readonly float MeasuredDimension(Dimension axis) => axis == Layout.Dimension.Width ? _measuredWidth : _measuredHeight;

    public void SetMeasuredDimension(Dimension axis, float value)
    {
        if (axis == Layout.Dimension.Width)
        {
            _measuredWidth = value;
        }
        else
        {
            _measuredHeight = value;
        }
    }

    public readonly float Position(PhysicalEdge edge)
    {
        return edge switch
        {
            PhysicalEdge.Left => _left,
            PhysicalEdge.Top => _top,
            PhysicalEdge.Right => _right,
            _ => _bottom,
        };
    }

    public void SetPosition(PhysicalEdge edge, float value)
    {
        switch (edge)
        {
            case PhysicalEdge.Left:
                _left = value;
                break;
            case PhysicalEdge.Top:
                _top = value;
                break;
            case PhysicalEdge.Right:
                _right = value;
                break;
            default:
                _bottom = value;
                break;
        }
    }

    public readonly float Margin(PhysicalEdge edge)
    {
        return edge switch
        {
            PhysicalEdge.Left => _marginLeft,
            PhysicalEdge.Top => _marginTop,
            PhysicalEdge.Right => _marginRight,
            _ => _marginBottom,
        };
    }

    public void SetMargin(PhysicalEdge edge, float value)
    {
        switch (edge)
        {
            case PhysicalEdge.Left:
                _marginLeft = value;
                break;
            case PhysicalEdge.Top:
                _marginTop = value;
                break;
            case PhysicalEdge.Right:
                _marginRight = value;
                break;
            default:
                _marginBottom = value;
                break;
        }
    }

    public readonly float Border(PhysicalEdge edge)
    {
        return edge switch
        {
            PhysicalEdge.Left => _borderLeft,
            PhysicalEdge.Top => _borderTop,
            PhysicalEdge.Right => _borderRight,
            _ => _borderBottom,
        };
    }

    public void SetBorder(PhysicalEdge edge, float value)
    {
        switch (edge)
        {
            case PhysicalEdge.Left:
                _borderLeft = value;
                break;
            case PhysicalEdge.Top:
                _borderTop = value;
                break;
            case PhysicalEdge.Right:
                _borderRight = value;
                break;
            default:
                _borderBottom = value;
                break;
        }
    }

    public readonly float Padding(PhysicalEdge edge)
    {
        return edge switch
        {
            PhysicalEdge.Left => _paddingLeft,
            PhysicalEdge.Top => _paddingTop,
            PhysicalEdge.Right => _paddingRight,
            _ => _paddingBottom,
        };
    }

    public void SetPadding(PhysicalEdge edge, float value)
    {
        switch (edge)
        {
            case PhysicalEdge.Left:
                _paddingLeft = value;
                break;
            case PhysicalEdge.Top:
                _paddingTop = value;
                break;
            case PhysicalEdge.Right:
                _paddingRight = value;
                break;
            default:
                _paddingBottom = value;
                break;
        }
    }
}
