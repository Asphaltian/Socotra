using System.Runtime.CompilerServices;

namespace Socotra.Layout;

internal static class Axis
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRow(FlexDirection flexDirection) => flexDirection == FlexDirection.Row || flexDirection == FlexDirection.RowReverse;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsColumn(FlexDirection flexDirection) => flexDirection == FlexDirection.Column || flexDirection == FlexDirection.ColumnReverse;

    public static FlexDirection ResolveDirection(FlexDirection flexDirection, Direction direction)
    {
        if (direction == Direction.RTL)
        {
            if (flexDirection == FlexDirection.Row)
            {
                return FlexDirection.RowReverse;
            }

            if (flexDirection == FlexDirection.RowReverse)
            {
                return FlexDirection.Row;
            }
        }

        return flexDirection;
    }

    public static FlexDirection ResolveCrossDirection(FlexDirection flexDirection, Direction direction)
    {
        return IsColumn(flexDirection) ? ResolveDirection(FlexDirection.Row, direction) : FlexDirection.Column;
    }

    public static PhysicalEdge FlexStartEdge(FlexDirection flexDirection)
    {
        return flexDirection switch
        {
            FlexDirection.Column => PhysicalEdge.Top,
            FlexDirection.ColumnReverse => PhysicalEdge.Bottom,
            FlexDirection.Row => PhysicalEdge.Left,
            _ => PhysicalEdge.Right,
        };
    }

    public static PhysicalEdge FlexEndEdge(FlexDirection flexDirection)
    {
        return flexDirection switch
        {
            FlexDirection.Column => PhysicalEdge.Bottom,
            FlexDirection.ColumnReverse => PhysicalEdge.Top,
            FlexDirection.Row => PhysicalEdge.Right,
            _ => PhysicalEdge.Left,
        };
    }

    public static PhysicalEdge InlineStartEdge(FlexDirection flexDirection, Direction direction)
    {
        if (IsRow(flexDirection))
        {
            return direction == Direction.RTL ? PhysicalEdge.Right : PhysicalEdge.Left;
        }

        return PhysicalEdge.Top;
    }

    public static PhysicalEdge InlineEndEdge(FlexDirection flexDirection, Direction direction)
    {
        if (IsRow(flexDirection))
        {
            return direction == Direction.RTL ? PhysicalEdge.Left : PhysicalEdge.Right;
        }

        return PhysicalEdge.Bottom;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dimension DimensionOf(FlexDirection flexDirection) => IsRow(flexDirection) ? Dimension.Width : Dimension.Height;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsTrailingPosition(FlexDirection axis) => axis == FlexDirection.RowReverse || axis == FlexDirection.ColumnReverse;

    public static float GetPositionOfOppositeEdge(float position, FlexDirection axis, LayoutNode containingNode, LayoutNode node)
    {
        return containingNode.Layout.MeasuredDimension(DimensionOf(axis))
            - node.Layout.MeasuredDimension(DimensionOf(axis))
            - position;
    }

    public static void SetChildTrailingPosition(LayoutNode node, LayoutNode child, FlexDirection axis)
    {
        child.Layout.SetPosition(
            FlexEndEdge(axis),
            GetPositionOfOppositeEdge(child.Layout.Position(FlexStartEdge(axis)), axis, node, child)
        );
    }

    public static MeasureMode ToMeasureMode(SizingMode mode)
    {
        return mode switch
        {
            SizingMode.StretchFit => MeasureMode.Exactly,
            SizingMode.MaxContent => MeasureMode.Undefined,
            SizingMode.FitContent => MeasureMode.AtMost,
            _ => MeasureMode.MinContent,
        };
    }

    public static Align ResolveChildAlignment(LayoutNode node, LayoutNode child)
    {
        var align = child.Style.AlignSelf == Align.Auto ? node.Style.AlignItems : child.Style.AlignSelf;
        if (align == Align.Baseline && IsColumn(node.Style.FlexDirection))
        {
            return Align.FlexStart;
        }

        return align;
    }

    public static Align FallbackAlignment(Align align)
    {
        return align switch
        {
            Align.SpaceBetween or Align.Stretch or Align.SpaceAround or Align.SpaceEvenly => Align.FlexStart,
            _ => align,
        };
    }

    public static Justify FallbackAlignment(Justify align)
    {
        return align switch
        {
            Justify.SpaceBetween or Justify.Stretch or Justify.SpaceAround or Justify.SpaceEvenly => Justify.FlexStart,
            _ => align,
        };
    }
}
