namespace Socotra.Layout;

internal static partial class LayoutAlgorithm
{
    private static void MeasureNodeWithMeasureFunc(
        LayoutNode node,
        Direction direction,
        float availableWidth,
        float availableHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight)
    {
        if (widthSizingMode == SizingMode.MaxContent || widthSizingMode == SizingMode.MinContent)
        {
            availableWidth = Num.Undefined;
        }

        if (heightSizingMode == SizingMode.MaxContent || heightSizingMode == SizingMode.MinContent)
        {
            availableHeight = Num.Undefined;
        }

        ref var layout = ref node.Layout;
        var paddingAndBorderAxisRow = layout.Padding(PhysicalEdge.Left)
            + layout.Padding(PhysicalEdge.Right)
            + layout.Border(PhysicalEdge.Left)
            + layout.Border(PhysicalEdge.Right);
        var paddingAndBorderAxisColumn = layout.Padding(PhysicalEdge.Top)
            + layout.Padding(PhysicalEdge.Bottom)
            + layout.Border(PhysicalEdge.Top)
            + layout.Border(PhysicalEdge.Bottom);

        var innerWidth = Num.IsUndefined(availableWidth) ? availableWidth : Num.MaxOrDefined(0.0f, availableWidth - paddingAndBorderAxisRow);
        var innerHeight = Num.IsUndefined(availableHeight) ? availableHeight : Num.MaxOrDefined(0.0f, availableHeight - paddingAndBorderAxisColumn);

        if (widthSizingMode == SizingMode.StretchFit && heightSizingMode == SizingMode.StretchFit)
        {
            node.Layout.SetMeasuredDimension(Dimension.Width, BoundAxis(node, FlexDirection.Row, direction, availableWidth, ownerWidth, ownerWidth));
            node.Layout.SetMeasuredDimension(Dimension.Height, BoundAxis(node, FlexDirection.Column, direction, availableHeight, ownerHeight, ownerWidth));
        }
        else
        {
            var measuredSize = node.Measure(innerWidth, Axis.ToMeasureMode(widthSizingMode), innerHeight, Axis.ToMeasureMode(heightSizingMode));

            node.Layout.SetMeasuredDimension(Dimension.Width, BoundAxis(node, FlexDirection.Row, direction,
                widthSizingMode != SizingMode.StretchFit ? measuredSize.Width + paddingAndBorderAxisRow : availableWidth, ownerWidth, ownerWidth));

            node.Layout.SetMeasuredDimension(Dimension.Height, BoundAxis(node, FlexDirection.Column, direction,
                heightSizingMode != SizingMode.StretchFit ? measuredSize.Height + paddingAndBorderAxisColumn : availableHeight, ownerHeight, ownerWidth));
        }
    }

    private static void MeasureNodeWithoutChildren(
        LayoutNode node,
        Direction direction,
        float availableWidth,
        float availableHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight)
    {
        ref var layout = ref node.Layout;

        var width = availableWidth;
        if (widthSizingMode != SizingMode.StretchFit)
        {
            width = layout.Padding(PhysicalEdge.Left) + layout.Padding(PhysicalEdge.Right) + layout.Border(PhysicalEdge.Left) + layout.Border(PhysicalEdge.Right);
        }
        node.Layout.SetMeasuredDimension(Dimension.Width, BoundAxis(node, FlexDirection.Row, direction, width, ownerWidth, ownerWidth));

        var height = availableHeight;
        if (heightSizingMode != SizingMode.StretchFit)
        {
            height = layout.Padding(PhysicalEdge.Top) + layout.Padding(PhysicalEdge.Bottom) + layout.Border(PhysicalEdge.Top) + layout.Border(PhysicalEdge.Bottom);
        }
        node.Layout.SetMeasuredDimension(Dimension.Height, BoundAxis(node, FlexDirection.Column, direction, height, ownerHeight, ownerWidth));
    }

    private static bool MeasureNodeWithFixedSize(
        LayoutNode node,
        Direction direction,
        float availableWidth,
        float availableHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight)
    {
        var noRoom = (Num.IsDefined(availableWidth) && widthSizingMode == SizingMode.FitContent && availableWidth <= 0.0f)
            || (Num.IsDefined(availableHeight) && heightSizingMode == SizingMode.FitContent && availableHeight <= 0.0f);

        if (noRoom || (widthSizingMode == SizingMode.StretchFit && heightSizingMode == SizingMode.StretchFit))
        {
            if (noRoom)
            {
                _nonContentMeasurements++;
            }

            node.Layout.SetMeasuredDimension(Dimension.Width, BoundAxis(node, FlexDirection.Row, direction,
                Num.IsUndefined(availableWidth) || (widthSizingMode == SizingMode.FitContent && availableWidth < 0.0f) ? 0.0f : availableWidth, ownerWidth, ownerWidth));

            node.Layout.SetMeasuredDimension(Dimension.Height, BoundAxis(node, FlexDirection.Column, direction,
                Num.IsUndefined(availableHeight) || (heightSizingMode == SizingMode.FitContent && availableHeight < 0.0f) ? 0.0f : availableHeight, ownerHeight, ownerWidth));

            return true;
        }

        return false;
    }

    private static bool IsAnsweredByAvailableSize(
        LayoutNode node,
        MeasureScope scope,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        uint generationCount)
    {
        if ((scope == MeasureScope.Width ? widthSizingMode : heightSizingMode) != SizingMode.StretchFit)
        {
            return false;
        }

        if (widthSizingMode == SizingMode.StretchFit && heightSizingMode == SizingMode.StretchFit)
        {
            return false;
        }

        if (node.LayoutChildCount == 0)
        {
            return false;
        }

        if (node.Style.Display == Display.Grid || node.Style.Display == Display.Block)
        {
            return false;
        }

        return !node.HasSingleStickyFlexChild(generationCount);
    }

    private static void MeasureFromAvailableSize(
        LayoutNode node,
        float availableWidth,
        float availableHeight,
        Direction ownerDirection,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight)
    {
        var direction = node.ResolveDirection(ownerDirection);
        node.Layout.Direction = direction;
        node.Layout.HadOverflow = false;

        var width = Num.Undefined;
        if (widthSizingMode == SizingMode.StretchFit)
        {
            width = BoundAxis(node, FlexDirection.Row, direction, availableWidth - node.Style.ComputeMarginForAxis(FlexDirection.Row, ownerWidth), ownerWidth, ownerWidth);
        }

        var height = Num.Undefined;
        if (heightSizingMode == SizingMode.StretchFit)
        {
            height = BoundAxis(node, FlexDirection.Column, direction, availableHeight - node.Style.ComputeMarginForAxis(FlexDirection.Column, ownerWidth), ownerHeight, ownerWidth);
        }

        node.Layout.SetMeasuredDimension(Dimension.Width, width);
        node.Layout.SetMeasuredDimension(Dimension.Height, height);
        CleanupContentsNodesRecursively(node);
    }
}
