using static Socotra.Layout.LayoutAlgorithm;

namespace Socotra.Layout;

internal static partial class FlexLayout
{
    private static void ComputeFlexBasisForChild(
        LayoutNode node,
        LayoutNode child,
        float width,
        SizingMode widthMode,
        float height,
        float ownerWidth,
        float ownerHeight,
        SizingMode heightMode,
        Direction direction,
        int depth,
        uint generationCount,
        bool minContentWidth,
        bool minContentHeight)
    {
        var mainAxis = Axis.ResolveDirection(node.Style.FlexDirection, direction);
        var isMainAxisRow = Axis.IsRow(mainAxis);
        var mainAxisSize = isMainAxisRow ? width : height;
        var mainAxisOwnerSize = isMainAxisRow ? ownerWidth : ownerHeight;

        var childWidth = Num.Undefined;
        var childHeight = Num.Undefined;
        SizingMode childWidthSizingMode;
        SizingMode childHeightSizingMode;

        var resolvedFlexBasis = child.ResolveFlexBasis(direction, mainAxis, mainAxisOwnerSize, ownerWidth);
        var previousFlexBasis = child.Layout.ComputedFlexBasis;
        var isRowStyleDimDefined = child.HasDefiniteLength(Dimension.Width, ownerWidth);
        var isColumnStyleDimDefined = child.HasDefiniteLength(Dimension.Height, ownerHeight);

        if (Num.IsDefined(resolvedFlexBasis) && Num.IsDefined(mainAxisSize))
        {
            if (Num.IsUndefined(child.Layout.ComputedFlexBasis))
            {
                var paddingAndBorder = PaddingAndBorderForAxis(child, mainAxis, direction, ownerWidth);
                child.Layout.ComputedFlexBasis = Num.MaxOrDefined(resolvedFlexBasis, paddingAndBorder);
            }
        }
        else if (isMainAxisRow && isRowStyleDimDefined)
        {
            var paddingAndBorder = PaddingAndBorderForAxis(child, FlexDirection.Row, direction, ownerWidth);
            child.Layout.ComputedFlexBasis = Num.MaxOrDefined(child.GetResolvedDimension(direction, Dimension.Width, ownerWidth, ownerWidth), paddingAndBorder);
        }
        else if (!isMainAxisRow && isColumnStyleDimDefined)
        {
            var paddingAndBorder = PaddingAndBorderForAxis(child, FlexDirection.Column, direction, ownerWidth);
            child.Layout.ComputedFlexBasis = Num.MaxOrDefined(child.GetResolvedDimension(direction, Dimension.Height, ownerHeight, ownerWidth), paddingAndBorder);
        }
        else
        {
            childWidthSizingMode = minContentWidth ? SizingMode.MinContent : SizingMode.MaxContent;
            childHeightSizingMode = minContentHeight ? SizingMode.MinContent : SizingMode.MaxContent;

            var marginRow = child.Style.ComputeMarginForAxis(FlexDirection.Row, ownerWidth);
            var marginColumn = child.Style.ComputeMarginForAxis(FlexDirection.Column, ownerWidth);

            if (isRowStyleDimDefined)
            {
                childWidth = child.GetResolvedDimension(direction, Dimension.Width, ownerWidth, ownerWidth) + marginRow;
                childWidthSizingMode = SizingMode.StretchFit;
            }
            if (isColumnStyleDimDefined)
            {
                childHeight = child.GetResolvedDimension(direction, Dimension.Height, ownerHeight, ownerWidth) + marginColumn;
                childHeightSizingMode = SizingMode.StretchFit;
            }

            if ((!isMainAxisRow && node.Style.Overflow == Overflow.Scroll) || node.Style.Overflow != Overflow.Scroll)
            {
                if (Num.IsUndefined(childWidth) && Num.IsDefined(width))
                {
                    childWidth = width;
                    childWidthSizingMode = SizingMode.FitContent;
                }
            }

            if ((isMainAxisRow && node.Style.Overflow == Overflow.Scroll) || node.Style.Overflow != Overflow.Scroll)
            {
                if (Num.IsUndefined(childHeight) && Num.IsDefined(height))
                {
                    childHeight = height;
                    childHeightSizingMode = SizingMode.FitContent;
                }
            }

            var childStyle = child.Style;
            if (Num.IsDefined(childStyle.AspectRatio))
            {
                if (!isMainAxisRow && childWidthSizingMode == SizingMode.StretchFit)
                {
                    childHeight = marginColumn + (childWidth - marginRow) / childStyle.AspectRatio;
                    childHeightSizingMode = SizingMode.StretchFit;
                }
                else if (isMainAxisRow && childHeightSizingMode == SizingMode.StretchFit)
                {
                    childWidth = marginRow + (childHeight - marginColumn) * childStyle.AspectRatio;
                    childWidthSizingMode = SizingMode.StretchFit;
                }
            }

            var hasExactWidth = Num.IsDefined(width) && widthMode == SizingMode.StretchFit;
            var childWidthStretch = Axis.ResolveChildAlignment(node, child) == Align.Stretch && childWidthSizingMode != SizingMode.StretchFit;
            if (!isMainAxisRow && !isRowStyleDimDefined && hasExactWidth && childWidthStretch)
            {
                childWidth = width;
                childWidthSizingMode = SizingMode.StretchFit;
                if (Num.IsDefined(childStyle.AspectRatio))
                {
                    childHeight = (childWidth - marginRow) / childStyle.AspectRatio;
                    childHeightSizingMode = SizingMode.StretchFit;
                }
            }

            var hasExactHeight = Num.IsDefined(height) && heightMode == SizingMode.StretchFit;
            var childHeightStretch = Axis.ResolveChildAlignment(node, child) == Align.Stretch && childHeightSizingMode != SizingMode.StretchFit;
            if (isMainAxisRow && !isColumnStyleDimDefined && hasExactHeight && childHeightStretch)
            {
                childHeight = height;
                childHeightSizingMode = SizingMode.StretchFit;

                if (Num.IsDefined(childStyle.AspectRatio))
                {
                    childWidth = (childHeight - marginColumn) * childStyle.AspectRatio;
                    childWidthSizingMode = SizingMode.StretchFit;
                }
            }

            ConstrainMaxSizeForMode(child, direction, FlexDirection.Row, ownerWidth, ownerWidth, ref childWidthSizingMode, ref childWidth);
            ConstrainMaxSizeForMode(child, direction, FlexDirection.Column, ownerHeight, ownerWidth, ref childHeightSizingMode, ref childHeight);

            CalculateLayoutInternal(child, childWidth, childHeight, direction, childWidthSizingMode, childHeightSizingMode, ownerWidth, ownerHeight, false, depth, generationCount,
                isMainAxisRow ? MeasureScope.Width : MeasureScope.Height);

            child.Layout.ComputedFlexBasis = Num.MaxOrDefined(
                child.Layout.MeasuredDimension(Axis.DimensionOf(mainAxis)),
                PaddingAndBorderForAxis(child, mainAxis, direction, ownerWidth)
            );
        }

        child.Layout.ComputedFlexBasisGeneration = generationCount;
        if (Num.IsDefined(resolvedFlexBasis))
        {
            NoteFlexBasisChange(child, previousFlexBasis);
        }
    }

    private static float ComputeFlexBasisForChildren(
        LayoutNode node,
        List<LayoutNode> children,
        float availableInnerWidth,
        float availableInnerHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        Direction direction,
        FlexDirection mainAxis,
        bool performLayout,
        int depth,
        uint generationCount,
        bool minContentWidth,
        bool minContentHeight)
    {
        var totalOuterFlexBasis = 0.0f;
        LayoutNode? singleFlexChild = null;
        var sizingModeMainDim = Axis.IsRow(mainAxis) ? widthSizingMode : heightSizingMode;
        var childrenBaselineSensitive = node.Layout.BaselineSensitive || node.IsBaselineContainer;

        if (sizingModeMainDim == SizingMode.StretchFit)
        {
            foreach (var child in children)
            {
                if (child.IsNodeFlexible())
                {
                    if (singleFlexChild is not null || Num.InexactEquals(child.ResolveFlexGrow(), 0.0f) || Num.InexactEquals(child.ResolveFlexShrink(), 0.0f))
                    {
                        singleFlexChild = null;
                        break;
                    }
                    else
                    {
                        singleFlexChild = child;
                    }
                }
            }
        }

        foreach (var child in children)
        {
            child.ProcessDimensions();
            if (child.Style.Display == Display.None)
            {
                ZeroOutLayoutRecursively(child);
                child.HasNewLayout = true;
                child.SetDirty(false);
                continue;
            }

            child.Layout.BaselineSensitive = childrenBaselineSensitive && !child.Style.IsOutOfFlow;

            if (performLayout)
            {
                var childDirection = child.ResolveDirection(direction);
                child.SetPosition(childDirection, availableInnerWidth, availableInnerHeight);
            }

            if (child.Style.IsOutOfFlow)
            {
                continue;
            }

            if (child == singleFlexChild)
            {
                var previousFlexBasis = child.Layout.ComputedFlexBasis;
                child.Layout.ComputedFlexBasisGeneration = generationCount;
                child.Layout.ComputedFlexBasis = 0;
                if (Num.IsDefined(child.ResolveFlexBasis(
                    direction,
                    mainAxis,
                    Axis.IsRow(mainAxis) ? availableInnerWidth : availableInnerHeight,
                    availableInnerWidth
                )))
                {
                    NoteFlexBasisChange(child, previousFlexBasis);
                }
            }
            else
            {
                ComputeFlexBasisForChild(
                    node,
                    child,
                    availableInnerWidth,
                    widthSizingMode,
                    availableInnerHeight,
                    availableInnerWidth,
                    availableInnerHeight,
                    heightSizingMode,
                    direction,
                    depth,
                    generationCount,
                    minContentWidth,
                    minContentHeight
                );
            }

            totalOuterFlexBasis += child.Layout.ComputedFlexBasis + child.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth);
        }

        return totalOuterFlexBasis;
    }

    private static float DistributeFreeSpaceSecondPass(
        FlexLine flexLine,
        LayoutNode node,
        FlexDirection mainAxis,
        FlexDirection crossAxis,
        Direction direction,
        float ownerWidth,
        float mainAxisOwnerSize,
        float availableInnerMainDim,
        float availableInnerCrossDim,
        float availableInnerWidth,
        float availableInnerHeight,
        bool mainAxisOverflows,
        SizingMode sizingModeCrossDim,
        bool performLayout,
        int depth,
        uint generationCount,
        bool minContentCross,
        MeasureScope childScope)
    {
        float childFlexBasis;
        float flexShrinkScaledFactor;
        float flexGrowFactor;
        float deltaFreeSpace = 0;
        var isMainAxisRow = Axis.IsRow(mainAxis);
        var isNodeFlexWrap = node.Style.FlexWrap != Wrap.NoWrap;

        foreach (var currentLineChild in flexLine.ItemsInFlow)
        {
            childFlexBasis = BoundAxisWithinMinAndMax(currentLineChild, direction, mainAxis, currentLineChild.Layout.ComputedFlexBasis, mainAxisOwnerSize, ownerWidth);
            var updatedMainSize = childFlexBasis;

            if (Num.IsDefined(flexLine.Layout.RemainingFreeSpace) && flexLine.Layout.RemainingFreeSpace < 0)
            {
                flexShrinkScaledFactor = -currentLineChild.ResolveFlexShrink() * childFlexBasis;

                if (flexShrinkScaledFactor != 0)
                {
                    float childSize;

                    if (Num.IsDefined(flexLine.Layout.TotalFlexShrinkScaledFactors) && flexLine.Layout.TotalFlexShrinkScaledFactors == 0)
                    {
                        childSize = childFlexBasis + flexShrinkScaledFactor;
                    }
                    else
                    {
                        childSize = childFlexBasis + (flexLine.Layout.RemainingFreeSpace / flexLine.Layout.TotalFlexShrinkScaledFactors) * flexShrinkScaledFactor;
                    }

                    updatedMainSize = BoundAxis(currentLineChild, mainAxis, direction, childSize, availableInnerMainDim, availableInnerWidth);
                }
            }
            else if (Num.IsDefined(flexLine.Layout.RemainingFreeSpace) && flexLine.Layout.RemainingFreeSpace > 0)
            {
                flexGrowFactor = currentLineChild.ResolveFlexGrow();

                if (!float.IsNaN(flexGrowFactor) && flexGrowFactor != 0)
                {
                    updatedMainSize = BoundAxis(currentLineChild, mainAxis, direction,
                        childFlexBasis + flexLine.Layout.RemainingFreeSpace / flexLine.Layout.TotalFlexGrowFactors * flexGrowFactor, availableInnerMainDim, availableInnerWidth);
                }
            }

            deltaFreeSpace += updatedMainSize - childFlexBasis;

            var marginMain = currentLineChild.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth);
            var marginCross = currentLineChild.Style.ComputeMarginForAxis(crossAxis, availableInnerWidth);

            var childCrossSize = Num.Undefined;
            var childMainSize = updatedMainSize + marginMain;
            SizingMode childCrossSizingMode;
            var childMainSizingMode = SizingMode.StretchFit;

            var childStyle = currentLineChild.Style;
            if (Num.IsDefined(childStyle.AspectRatio))
            {
                childCrossSize = isMainAxisRow ? (childMainSize - marginMain) / childStyle.AspectRatio : (childMainSize - marginMain) * childStyle.AspectRatio;
                childCrossSizingMode = SizingMode.StretchFit;

                childCrossSize += marginCross;
            }
            else if (!float.IsNaN(availableInnerCrossDim)
                && !currentLineChild.HasDefiniteLength(Axis.DimensionOf(crossAxis), availableInnerCrossDim)
                && sizingModeCrossDim == SizingMode.StretchFit
                && !(isNodeFlexWrap && mainAxisOverflows)
                && Axis.ResolveChildAlignment(node, currentLineChild) == Align.Stretch
                && !currentLineChild.Style.FlexStartMarginIsAuto(crossAxis, direction)
                && !currentLineChild.Style.FlexEndMarginIsAuto(crossAxis, direction))
            {
                childCrossSize = availableInnerCrossDim;
                childCrossSizingMode = SizingMode.StretchFit;
            }
            else if (!currentLineChild.HasDefiniteLength(Axis.DimensionOf(crossAxis), availableInnerCrossDim))
            {
                childCrossSize = availableInnerCrossDim;
                childCrossSizingMode = Num.IsUndefined(childCrossSize) ? (minContentCross ? SizingMode.MinContent : SizingMode.MaxContent) : SizingMode.FitContent;
            }
            else
            {
                childCrossSize = currentLineChild.GetResolvedDimension(direction, Axis.DimensionOf(crossAxis), availableInnerCrossDim, availableInnerWidth) + marginCross;
                var isLoosePercentageMeasurement = currentLineChild.GetProcessedDimension(Axis.DimensionOf(crossAxis)).Unit == Unit.Percent && sizingModeCrossDim != SizingMode.StretchFit;
                childCrossSizingMode = Num.IsUndefined(childCrossSize) || isLoosePercentageMeasurement ? SizingMode.MaxContent : SizingMode.StretchFit;
            }

            ConstrainMaxSizeForMode(currentLineChild, direction, mainAxis, availableInnerMainDim, availableInnerWidth, ref childMainSizingMode, ref childMainSize);
            ConstrainMaxSizeForMode(currentLineChild, direction, crossAxis, availableInnerCrossDim, availableInnerWidth, ref childCrossSizingMode, ref childCrossSize);

            var requiresStretchLayout = !currentLineChild.HasDefiniteLength(Axis.DimensionOf(crossAxis), availableInnerCrossDim)
                && Axis.ResolveChildAlignment(node, currentLineChild) == Align.Stretch
                && !currentLineChild.Style.FlexStartMarginIsAuto(crossAxis, direction)
                && !currentLineChild.Style.FlexEndMarginIsAuto(crossAxis, direction);

            var childWidth = isMainAxisRow ? childMainSize : childCrossSize;
            var childHeight = !isMainAxisRow ? childMainSize : childCrossSize;

            var childWidthSizingMode = isMainAxisRow ? childMainSizingMode : childCrossSizingMode;
            var childHeightSizingMode = !isMainAxisRow ? childMainSizingMode : childCrossSizingMode;

            var isLayoutPass = performLayout && !requiresStretchLayout;

            CalculateLayoutInternal(currentLineChild, childWidth, childHeight, node.Layout.Direction, childWidthSizingMode, childHeightSizingMode, availableInnerWidth, availableInnerHeight,
                isLayoutPass, depth, generationCount, childScope);

            node.Layout.HadOverflow = node.Layout.HadOverflow || currentLineChild.Layout.HadOverflow;
        }

        return deltaFreeSpace;
    }

    private static void DistributeFreeSpaceFirstPass(
        FlexLine flexLine,
        Direction direction,
        FlexDirection mainAxis,
        float ownerWidth,
        float mainAxisOwnerSize,
        float availableInnerMainDim,
        float availableInnerWidth)
    {
        float flexShrinkScaledFactor;
        float flexGrowFactor;
        float baseMainSize;
        float boundMainSize;
        float deltaFreeSpace = 0;

        foreach (var currentLineChild in flexLine.ItemsInFlow)
        {
            var childFlexBasis = BoundAxisWithinMinAndMax(currentLineChild, direction, mainAxis, currentLineChild.Layout.ComputedFlexBasis, mainAxisOwnerSize, ownerWidth);

            if (flexLine.Layout.RemainingFreeSpace < 0)
            {
                flexShrinkScaledFactor = -currentLineChild.ResolveFlexShrink() * childFlexBasis;

                if (Num.IsDefined(flexShrinkScaledFactor) && flexShrinkScaledFactor != 0)
                {
                    baseMainSize = childFlexBasis + flexLine.Layout.RemainingFreeSpace / flexLine.Layout.TotalFlexShrinkScaledFactors * flexShrinkScaledFactor;
                    boundMainSize = BoundAxis(currentLineChild, mainAxis, direction, baseMainSize, availableInnerMainDim, availableInnerWidth);
                    if (Num.IsDefined(baseMainSize) && Num.IsDefined(boundMainSize) && baseMainSize != boundMainSize)
                    {
                        deltaFreeSpace += boundMainSize - childFlexBasis;
                        flexLine.Layout.TotalFlexShrinkScaledFactors -= -currentLineChild.ResolveFlexShrink() * currentLineChild.Layout.ComputedFlexBasis;
                    }
                }
            }
            else if (Num.IsDefined(flexLine.Layout.RemainingFreeSpace) && flexLine.Layout.RemainingFreeSpace > 0)
            {
                flexGrowFactor = currentLineChild.ResolveFlexGrow();

                if (Num.IsDefined(flexGrowFactor) && flexGrowFactor != 0)
                {
                    baseMainSize = childFlexBasis + flexLine.Layout.RemainingFreeSpace / flexLine.Layout.TotalFlexGrowFactors * flexGrowFactor;
                    boundMainSize = BoundAxis(currentLineChild, mainAxis, direction, baseMainSize, availableInnerMainDim, availableInnerWidth);

                    if (Num.IsDefined(baseMainSize) && Num.IsDefined(boundMainSize) && baseMainSize != boundMainSize)
                    {
                        deltaFreeSpace += boundMainSize - childFlexBasis;
                        flexLine.Layout.TotalFlexGrowFactors -= flexGrowFactor;
                    }
                }
            }
        }

        flexLine.Layout.RemainingFreeSpace -= deltaFreeSpace;
    }

    private static void ResolveFlexibleLength(
        LayoutNode node,
        FlexLine flexLine,
        FlexDirection mainAxis,
        FlexDirection crossAxis,
        Direction direction,
        float ownerWidth,
        float mainAxisOwnerSize,
        float availableInnerMainDim,
        float availableInnerCrossDim,
        float availableInnerWidth,
        float availableInnerHeight,
        bool mainAxisOverflows,
        SizingMode sizingModeCrossDim,
        bool performLayout,
        int depth,
        uint generationCount,
        bool minContentCross,
        MeasureScope childScope)
    {
        var originalFreeSpace = flexLine.Layout.RemainingFreeSpace;

        DistributeFreeSpaceFirstPass(flexLine, direction, mainAxis, ownerWidth, mainAxisOwnerSize, availableInnerMainDim, availableInnerWidth);

        var distributedFreeSpace = DistributeFreeSpaceSecondPass(flexLine, node, mainAxis, crossAxis, direction, ownerWidth, mainAxisOwnerSize, availableInnerMainDim, availableInnerCrossDim,
            availableInnerWidth, availableInnerHeight, mainAxisOverflows, sizingModeCrossDim, performLayout, depth, generationCount, minContentCross, childScope);

        flexLine.Layout.RemainingFreeSpace = originalFreeSpace - distributedFreeSpace;
    }

    private static void JustifyMainAxis(
        LayoutNode node,
        FlexLine flexLine,
        FlexDirection mainAxis,
        FlexDirection crossAxis,
        Direction direction,
        SizingMode sizingModeMainDim,
        SizingMode sizingModeCrossDim,
        float mainAxisOwnerSize,
        float ownerWidth,
        float availableInnerMainDim,
        float availableInnerCrossDim,
        float availableInnerWidth,
        bool performLayout,
        bool mainAxisOnly)
    {
        var style = node.Style;

        var leadingPaddingAndBorderMain = node.Style.ComputeFlexStartPaddingAndBorder(mainAxis, direction, ownerWidth);
        var trailingPaddingAndBorderMain = node.Style.ComputeFlexEndPaddingAndBorder(mainAxis, direction, ownerWidth);

        var gap = node.Style.ComputeGapForAxis(mainAxis, availableInnerMainDim);

        if (sizingModeMainDim == SizingMode.FitContent && flexLine.Layout.RemainingFreeSpace > 0)
        {
            if (style.GetMinDimension(Axis.DimensionOf(mainAxis)).IsDefined
                && Num.IsDefined(style.ResolvedMinDimension(direction, Axis.DimensionOf(mainAxis), mainAxisOwnerSize, ownerWidth)))
            {
                var minAvailableMainDim = style.ResolvedMinDimension(direction, Axis.DimensionOf(mainAxis), mainAxisOwnerSize, ownerWidth) - leadingPaddingAndBorderMain - trailingPaddingAndBorderMain;
                var occupiedSpaceByChildNodes = availableInnerMainDim - flexLine.Layout.RemainingFreeSpace;
                flexLine.Layout.RemainingFreeSpace = Num.MaxOrDefined(0.0f, minAvailableMainDim - occupiedSpaceByChildNodes);
            }
            else
            {
                flexLine.Layout.RemainingFreeSpace = 0;
            }
        }

        float leadingMainDim = 0;
        float betweenMainDim = gap;
        var justifyContent = flexLine.Layout.RemainingFreeSpace >= 0 ? node.Style.JustifyContent : Axis.FallbackAlignment(node.Style.JustifyContent);

        if (flexLine.NumberOfAutoMargins == 0)
        {
            switch (justifyContent)
            {
                case Justify.Center:
                    leadingMainDim = flexLine.Layout.RemainingFreeSpace / 2;
                    break;
                case Justify.FlexEnd:
                    leadingMainDim = flexLine.Layout.RemainingFreeSpace;
                    break;
                case Justify.SpaceBetween:
                    if (flexLine.ItemsInFlow.Count > 1)
                    {
                        betweenMainDim += flexLine.Layout.RemainingFreeSpace / (flexLine.ItemsInFlow.Count - 1);
                    }
                    break;
                case Justify.SpaceEvenly:
                    leadingMainDim = flexLine.Layout.RemainingFreeSpace / (flexLine.ItemsInFlow.Count + 1);
                    betweenMainDim += leadingMainDim;
                    break;
                case Justify.SpaceAround:
                    leadingMainDim = 0.5f * flexLine.Layout.RemainingFreeSpace / flexLine.ItemsInFlow.Count;
                    betweenMainDim += leadingMainDim * 2;
                    break;
                case Justify.FlexStart:
                case Justify.Stretch:
                    break;
            }
        }

        flexLine.Layout.MainDimension = leadingPaddingAndBorderMain + leadingMainDim;
        flexLine.Layout.CrossDimension = 0;

        float maxAscentForCurrentLine = 0;
        float maxDescentForCurrentLine = 0;
        var isNodeBaselineLayout = !mainAxisOnly && IsBaselineLayout(node);
        var lastChild = flexLine.ItemsInFlow.Count > 0 ? flexLine.ItemsInFlow[^1] : null;

        foreach (var child in flexLine.ItemsInFlow)
        {
            ref var childLayout = ref child.Layout;
            if (child.Style.FlexStartMarginIsAuto(mainAxis, direction) && flexLine.Layout.RemainingFreeSpace > 0.0f)
            {
                flexLine.Layout.MainDimension += flexLine.Layout.RemainingFreeSpace / flexLine.NumberOfAutoMargins;
            }

            if (performLayout)
            {
                child.Layout.SetPosition(Axis.FlexStartEdge(mainAxis), childLayout.Position(Axis.FlexStartEdge(mainAxis)) + flexLine.Layout.MainDimension);
            }

            if (child != lastChild)
            {
                flexLine.Layout.MainDimension += betweenMainDim;
            }

            if (child.Style.FlexEndMarginIsAuto(mainAxis, direction) && flexLine.Layout.RemainingFreeSpace > 0.0f)
            {
                flexLine.Layout.MainDimension += flexLine.Layout.RemainingFreeSpace / flexLine.NumberOfAutoMargins;
            }

            var canSkipFlex = !performLayout && sizingModeCrossDim == SizingMode.StretchFit;
            if (canSkipFlex)
            {
                flexLine.Layout.MainDimension += child.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth) + childLayout.ComputedFlexBasis;
                flexLine.Layout.CrossDimension = availableInnerCrossDim;
            }
            else
            {
                flexLine.Layout.MainDimension += child.DimensionWithMargin(mainAxis, availableInnerWidth);

                if (isNodeBaselineLayout)
                {
                    var ascent = CalculateBaseline(child) + child.Style.ComputeFlexStartMargin(FlexDirection.Column, direction, availableInnerWidth);
                    var descent = child.Layout.MeasuredDimension(Dimension.Height) + child.Style.ComputeMarginForAxis(FlexDirection.Column, availableInnerWidth) - ascent;

                    maxAscentForCurrentLine = Num.MaxOrDefined(maxAscentForCurrentLine, ascent);
                    maxDescentForCurrentLine = Num.MaxOrDefined(maxDescentForCurrentLine, descent);
                }
                else
                {
                    flexLine.Layout.CrossDimension = Num.MaxOrDefined(flexLine.Layout.CrossDimension, child.DimensionWithMargin(crossAxis, availableInnerWidth));
                }
            }
        }

        flexLine.Layout.MainDimension += trailingPaddingAndBorderMain;

        if (isNodeBaselineLayout)
        {
            flexLine.Layout.CrossDimension = maxAscentForCurrentLine + maxDescentForCurrentLine;
        }
    }

    [ThreadStatic] private static Stack<FlexLine>? _flexLinePool;
    [ThreadStatic] private static int _pooledFlexLineItems;
    private const int MaxPooledFlexLines = 128;
    private const int MaxPooledFlexLineItems = 4096;
    private const int MaxTotalPooledFlexLineItems = 32768;

    private static FlexLine RentFlexLine()
    {
        _flexLinePool ??= new Stack<FlexLine>();
        if (_flexLinePool.Count == 0)
        {
            return new FlexLine();
        }

        var line = _flexLinePool.Pop();
        _pooledFlexLineItems -= line.ItemsInFlow.Capacity;
        return line;
    }

    private static void ReturnFlexLine(FlexLine line)
    {
        _flexLinePool ??= new Stack<FlexLine>();
        var retain = line.ItemsInFlow.Capacity <= MaxPooledFlexLineItems;
        line.Reset();
        if (retain && _flexLinePool.Count < MaxPooledFlexLines && _pooledFlexLineItems + line.ItemsInFlow.Capacity <= MaxTotalPooledFlexLineItems)
        {
            _flexLinePool.Push(line);
            _pooledFlexLineItems += line.ItemsInFlow.Capacity;
        }
    }

    internal static MeasureScope Compute(
        LayoutNode node,
        float availableWidth,
        float availableHeight,
        Direction ownerDirection,
        Direction direction,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight,
        bool performLayout,
        int depth,
        uint generationCount,
        float marginAxisRow,
        float marginAxisColumn,
        int childCount,
        MeasureScope scope)
    {
        var minContentWidth = widthSizingMode == SizingMode.MinContent;
        var minContentHeight = heightSizingMode == SizingMode.MinContent;
        if (minContentWidth)
        {
            widthSizingMode = SizingMode.MaxContent;
        }

        if (minContentHeight)
        {
            heightSizingMode = SizingMode.MaxContent;
        }

        var childBuffer = RentList();
        var children = node.GetLayoutChildren(childBuffer);

        var mainAxis = Axis.ResolveDirection(node.Style.FlexDirection, direction);
        var crossAxis = Axis.ResolveCrossDirection(mainAxis, direction);
        var isMainAxisRow = Axis.IsRow(mainAxis);
        var isNodeFlexWrap = node.Style.FlexWrap != Wrap.NoWrap;

        var mainDimension = isMainAxisRow ? MeasureScope.Width : MeasureScope.Height;
        var crossDimension = isMainAxisRow ? MeasureScope.Height : MeasureScope.Width;

        var mainAxisOnly = !performLayout && scope == mainDimension;
        var computedScope = mainAxisOnly && (isMainAxisRow ? heightSizingMode : widthSizingMode) != SizingMode.StretchFit
            ? mainDimension
            : MeasureScope.Both;
        var childScope = mainAxisOnly ? mainDimension : crossDimension;

        var mainAxisOwnerSize = isMainAxisRow ? ownerWidth : ownerHeight;
        var crossAxisOwnerSize = isMainAxisRow ? ownerHeight : ownerWidth;

        var paddingAndBorderAxisMain = PaddingAndBorderForAxis(node, mainAxis, direction, ownerWidth);
        var paddingAndBorderAxisCross = PaddingAndBorderForAxis(node, crossAxis, direction, ownerWidth);
        var leadingPaddingAndBorderCross = node.Style.ComputeFlexStartPaddingAndBorder(crossAxis, direction, ownerWidth);

        var sizingModeMainDim = isMainAxisRow ? widthSizingMode : heightSizingMode;
        var sizingModeCrossDim = isMainAxisRow ? heightSizingMode : widthSizingMode;

        var paddingAndBorderAxisRow = isMainAxisRow ? paddingAndBorderAxisMain : paddingAndBorderAxisCross;
        var paddingAndBorderAxisColumn = isMainAxisRow ? paddingAndBorderAxisCross : paddingAndBorderAxisMain;

        var availableInnerWidth = CalculateAvailableInnerDimension(
            node,
            direction,
            Dimension.Width,
            availableWidth - marginAxisRow,
            paddingAndBorderAxisRow,
            ownerWidth,
            ownerWidth
        );
        var availableInnerHeight = CalculateAvailableInnerDimension(
            node,
            direction,
            Dimension.Height,
            availableHeight - marginAxisColumn,
            paddingAndBorderAxisColumn,
            ownerHeight,
            ownerWidth
        );

        var availableInnerMainDim = isMainAxisRow ? availableInnerWidth : availableInnerHeight;
        var availableInnerCrossDim = isMainAxisRow ? availableInnerHeight : availableInnerWidth;

        float totalMainDim = 0;
        totalMainDim += ComputeFlexBasisForChildren(
            node,
            children,
            availableInnerWidth,
            availableInnerHeight,
            widthSizingMode,
            heightSizingMode,
            direction,
            mainAxis,
            performLayout,
            depth,
            generationCount,
            minContentWidth,
            minContentHeight
        );

        if (childCount > 1)
        {
            totalMainDim += node.Style.ComputeGapForAxis(mainAxis, availableInnerMainDim) * (childCount - 1);
        }

        var mainAxisOverflows = (sizingModeMainDim != SizingMode.MaxContent) && totalMainDim > availableInnerMainDim;

        if (isNodeFlexWrap && mainAxisOverflows && sizingModeMainDim == SizingMode.FitContent)
        {
            sizingModeMainDim = SizingMode.StretchFit;
        }

        int startOfLineIndex = 0;

        int lineCount = 0;

        float totalLineCrossDim = 0;

        var crossAxisGap = node.Style.ComputeGapForAxis(crossAxis, availableInnerCrossDim);

        float maxLineMainDim = 0;

        var flexLine = RentFlexLine();

        for (; startOfLineIndex < children.Count; lineCount++)
        {
            CalculateFlexLine(
                flexLine,
                node,
                ownerDirection,
                ownerWidth,
                mainAxisOwnerSize,
                availableInnerWidth,
                availableInnerMainDim,
                children,
                ref startOfLineIndex,
                lineCount
            );

            var canSkipFlex = !performLayout && sizingModeCrossDim == SizingMode.StretchFit;

            var sizeBasedOnContent = false;
            if (sizingModeMainDim != SizingMode.StretchFit)
            {
                var style = node.Style;
                var minInnerWidth = style.ResolvedMinDimension(direction, Dimension.Width, ownerWidth, ownerWidth) - paddingAndBorderAxisRow;
                var maxInnerWidth = style.ResolvedMaxDimension(direction, Dimension.Width, ownerWidth, ownerWidth) - paddingAndBorderAxisRow;
                var minInnerHeight = style.ResolvedMinDimension(direction, Dimension.Height, ownerHeight, ownerWidth) - paddingAndBorderAxisColumn;
                var maxInnerHeight = style.ResolvedMaxDimension(direction, Dimension.Height, ownerHeight, ownerWidth) - paddingAndBorderAxisColumn;

                var minInnerMainDim = isMainAxisRow ? minInnerWidth : minInnerHeight;
                var maxInnerMainDim = isMainAxisRow ? maxInnerWidth : maxInnerHeight;

                if (Num.IsDefined(minInnerMainDim) && flexLine.SizeConsumed < minInnerMainDim)
                {
                    availableInnerMainDim = minInnerMainDim;
                }
                else if (Num.IsDefined(maxInnerMainDim) && flexLine.SizeConsumed > maxInnerMainDim)
                {
                    availableInnerMainDim = maxInnerMainDim;
                }
                else
                {
                    if ((Num.IsDefined(flexLine.Layout.TotalFlexGrowFactors) && flexLine.Layout.TotalFlexGrowFactors == 0)
                        || (Num.IsDefined(node.ResolveFlexGrow()) && node.ResolveFlexGrow() == 0))
                    {
                        availableInnerMainDim = flexLine.SizeConsumed;
                    }

                    sizeBasedOnContent = true;
                }
            }

            if (!sizeBasedOnContent && Num.IsDefined(availableInnerMainDim))
            {
                flexLine.Layout.RemainingFreeSpace = availableInnerMainDim - flexLine.SizeConsumed;
            }
            else if (flexLine.SizeConsumed < 0)
            {
                flexLine.Layout.RemainingFreeSpace = -flexLine.SizeConsumed;
            }

            if (!canSkipFlex)
            {
                ResolveFlexibleLength(
                    node,
                    flexLine,
                    mainAxis,
                    crossAxis,
                    direction,
                    ownerWidth,
                    mainAxisOwnerSize,
                    availableInnerMainDim,
                    availableInnerCrossDim,
                    availableInnerWidth,
                    availableInnerHeight,
                    mainAxisOverflows,
                    sizingModeCrossDim,
                    performLayout,
                    depth,
                    generationCount,
                    isMainAxisRow ? minContentHeight : minContentWidth,
                    childScope
                );
            }

            node.Layout.HadOverflow = node.Layout.HadOverflow || (flexLine.Layout.RemainingFreeSpace < 0);

            JustifyMainAxis(
                node,
                flexLine,
                mainAxis,
                crossAxis,
                direction,
                sizingModeMainDim,
                sizingModeCrossDim,
                mainAxisOwnerSize,
                ownerWidth,
                availableInnerMainDim,
                availableInnerCrossDim,
                availableInnerWidth,
                performLayout,
                mainAxisOnly
            );

            var containerCrossAxis = availableInnerCrossDim;
            if (sizingModeCrossDim == SizingMode.MaxContent || sizingModeCrossDim == SizingMode.FitContent)
            {
                containerCrossAxis = BoundAxis(node, crossAxis, direction, flexLine.Layout.CrossDimension + paddingAndBorderAxisCross, crossAxisOwnerSize, ownerWidth) - paddingAndBorderAxisCross;
            }

            if (!isNodeFlexWrap && sizingModeCrossDim == SizingMode.StretchFit)
            {
                flexLine.Layout.CrossDimension = availableInnerCrossDim;
            }

            if (!isNodeFlexWrap)
            {
                flexLine.Layout.CrossDimension = BoundAxis(node, crossAxis, direction, flexLine.Layout.CrossDimension + paddingAndBorderAxisCross, crossAxisOwnerSize, ownerWidth) - paddingAndBorderAxisCross;
            }

            if (performLayout)
            {
                foreach (var child in flexLine.ItemsInFlow)
                {
                    var leadingCrossDim = leadingPaddingAndBorderCross;

                    var alignItem = Axis.ResolveChildAlignment(node, child);

                    if (alignItem == Align.Stretch && !child.Style.FlexStartMarginIsAuto(crossAxis, direction) && !child.Style.FlexEndMarginIsAuto(crossAxis, direction))
                    {
                        if (!child.HasDefiniteLength(Axis.DimensionOf(crossAxis), availableInnerCrossDim))
                        {
                            var childMainSize = child.Layout.MeasuredDimension(Axis.DimensionOf(mainAxis));
                            var childStyle = child.Style;
                            var childCrossSize = Num.IsDefined(childStyle.AspectRatio)
                                ? child.Style.ComputeMarginForAxis(crossAxis, availableInnerWidth) + (isMainAxisRow ? childMainSize / childStyle.AspectRatio : childMainSize * childStyle.AspectRatio)
                                : flexLine.Layout.CrossDimension;

                            childMainSize += child.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth);

                            var childMainSizingMode = SizingMode.StretchFit;
                            var childCrossSizingMode = SizingMode.StretchFit;
                            ConstrainMaxSizeForMode(child, direction, mainAxis, availableInnerMainDim, availableInnerWidth, ref childMainSizingMode, ref childMainSize);
                            ConstrainMaxSizeForMode(child, direction, crossAxis, availableInnerCrossDim, availableInnerWidth, ref childCrossSizingMode, ref childCrossSize);

                            var childWidth = isMainAxisRow ? childMainSize : childCrossSize;
                            var childHeight = !isMainAxisRow ? childMainSize : childCrossSize;

                            var alignContent = node.Style.AlignContent;
                            var crossAxisDoesNotGrow = alignContent != Align.Stretch && isNodeFlexWrap;
                            var childWidthSizingMode = Num.IsUndefined(childWidth) || (!isMainAxisRow && crossAxisDoesNotGrow) ? SizingMode.MaxContent : SizingMode.StretchFit;
                            var childHeightSizingMode = Num.IsUndefined(childHeight) || (isMainAxisRow && crossAxisDoesNotGrow) ? SizingMode.MaxContent : SizingMode.StretchFit;

                            CalculateLayoutInternal(child, childWidth, childHeight, direction, childWidthSizingMode, childHeightSizingMode, availableInnerWidth, availableInnerHeight, true, depth, generationCount);
                        }
                    }
                    else
                    {
                        var remainingCrossDim = containerCrossAxis - child.DimensionWithMargin(crossAxis, availableInnerWidth);

                        if (child.Style.FlexStartMarginIsAuto(crossAxis, direction) && child.Style.FlexEndMarginIsAuto(crossAxis, direction))
                        {
                            leadingCrossDim += Num.MaxOrDefined(0.0f, remainingCrossDim / 2);
                        }
                        else if (child.Style.FlexEndMarginIsAuto(crossAxis, direction))
                        {
                        }
                        else if (child.Style.FlexStartMarginIsAuto(crossAxis, direction))
                        {
                            leadingCrossDim += Num.MaxOrDefined(0.0f, remainingCrossDim);
                        }
                        else if (alignItem == Align.FlexStart)
                        {
                        }
                        else if (alignItem == Align.Center)
                        {
                            leadingCrossDim += remainingCrossDim / 2;
                        }
                        else
                        {
                            leadingCrossDim += remainingCrossDim;
                        }
                    }

                    child.Layout.SetPosition(Axis.FlexStartEdge(crossAxis), child.Layout.Position(Axis.FlexStartEdge(crossAxis)) + totalLineCrossDim + leadingCrossDim);
                }
            }

            var appliedCrossGap = lineCount != 0 ? crossAxisGap : 0.0f;
            totalLineCrossDim += flexLine.Layout.CrossDimension + appliedCrossGap;
            maxLineMainDim = Num.MaxOrDefined(maxLineMainDim, flexLine.Layout.MainDimension);
        }

        ReturnFlexLine(flexLine);

        if (performLayout && (isNodeFlexWrap || IsBaselineLayout(node)))
        {
            float leadPerLine = 0;
            var currentLead = leadingPaddingAndBorderCross;
            float extraSpacePerLine = 0;

            var unclampedCrossDim = sizingModeCrossDim == SizingMode.StretchFit
                ? availableInnerCrossDim + paddingAndBorderAxisCross
                : node.HasDefiniteLength(Axis.DimensionOf(crossAxis), crossAxisOwnerSize)
                    ? node.GetResolvedDimension(direction, Axis.DimensionOf(crossAxis), crossAxisOwnerSize, ownerWidth)
                    : totalLineCrossDim + paddingAndBorderAxisCross;

            var innerCrossDim = BoundAxis(node, crossAxis, direction, unclampedCrossDim, ownerHeight, ownerWidth) - paddingAndBorderAxisCross;

            var remainingAlignContentDim = innerCrossDim - totalLineCrossDim;

            var alignContent = remainingAlignContentDim >= 0 ? node.Style.AlignContent : Axis.FallbackAlignment(node.Style.AlignContent);

            switch (alignContent)
            {
                case Align.FlexEnd:
                    currentLead += remainingAlignContentDim;
                    break;
                case Align.Center:
                    currentLead += remainingAlignContentDim / 2;
                    break;
                case Align.Stretch:
                    extraSpacePerLine = remainingAlignContentDim / lineCount;
                    break;
                case Align.SpaceAround:
                    currentLead += remainingAlignContentDim / (2 * lineCount);
                    leadPerLine = remainingAlignContentDim / lineCount;
                    break;
                case Align.SpaceEvenly:
                    currentLead += remainingAlignContentDim / (lineCount + 1);
                    leadPerLine = remainingAlignContentDim / (lineCount + 1);
                    break;
                case Align.SpaceBetween:
                    if (lineCount > 1)
                    {
                        leadPerLine = remainingAlignContentDim / (lineCount - 1);
                    }
                    break;
                case Align.Auto:
                case Align.FlexStart:
                case Align.Baseline:
                    break;
            }

            int endIndex = 0;
            for (int i = 0; i < lineCount; i++)
            {
                var startIndex = endIndex;
                var ii = startIndex;

                float lineHeight = 0;
                float maxAscentForCurrentLine = 0;
                float maxDescentForCurrentLine = 0;
                for (; ii < children.Count; ii++)
                {
                    var child = children[ii];
                    if (child.Style.Display == Display.None)
                    {
                        continue;
                    }

                    if (!child.Style.IsOutOfFlow)
                    {
                        if (child.LineIndex != i)
                        {
                            break;
                        }

                        if (child.IsLayoutDimensionDefined(crossAxis))
                        {
                            lineHeight = Num.MaxOrDefined(
                                lineHeight,
                                child.Layout.MeasuredDimension(Axis.DimensionOf(crossAxis))
                                    + child.Style.ComputeMarginForAxis(crossAxis, availableInnerWidth)
                            );
                        }

                        if (Axis.ResolveChildAlignment(node, child) == Align.Baseline)
                        {
                            var ascent = CalculateBaseline(child) + child.Style.ComputeFlexStartMargin(FlexDirection.Column, direction, availableInnerWidth);
                            var descent = child.Layout.MeasuredDimension(Dimension.Height) + child.Style.ComputeMarginForAxis(FlexDirection.Column, availableInnerWidth) - ascent;
                            maxAscentForCurrentLine = Num.MaxOrDefined(maxAscentForCurrentLine, ascent);
                            maxDescentForCurrentLine = Num.MaxOrDefined(maxDescentForCurrentLine, descent);
                            lineHeight = Num.MaxOrDefined(lineHeight, maxAscentForCurrentLine + maxDescentForCurrentLine);
                        }
                    }
                }
                endIndex = ii;
                currentLead += i != 0 ? crossAxisGap : 0;
                lineHeight += extraSpacePerLine;

                for (ii = startIndex; ii < endIndex; ii++)
                {
                    var child = children[ii];
                    if (child.Style.Display == Display.None)
                    {
                        continue;
                    }

                    if (!child.Style.IsOutOfFlow)
                    {
                        switch (Axis.ResolveChildAlignment(node, child))
                        {
                            case Align.FlexStart:
                                child.Layout.SetPosition(Axis.FlexStartEdge(crossAxis), currentLead + child.Style.ComputeFlexStartPosition(crossAxis, direction, availableInnerWidth));
                                break;

                            case Align.FlexEnd:
                                child.Layout.SetPosition(
                                    Axis.FlexStartEdge(crossAxis),
                                    currentLead
                                        + lineHeight
                                        - child.Style.ComputeFlexEndMargin(crossAxis, direction, availableInnerWidth)
                                        - child.Layout.MeasuredDimension(Axis.DimensionOf(crossAxis))
                                );
                                break;

                            case Align.Center:
                                {
                                    var childHeight = child.Layout.MeasuredDimension(Axis.DimensionOf(crossAxis));
                                    child.Layout.SetPosition(Axis.FlexStartEdge(crossAxis), currentLead + (lineHeight - childHeight) / 2);
                                    break;
                                }

                            case Align.Stretch:
                                {
                                    child.Layout.SetPosition(Axis.FlexStartEdge(crossAxis), currentLead + child.Style.ComputeFlexStartMargin(crossAxis, direction, availableInnerWidth));

                                    if (!child.HasDefiniteLength(Axis.DimensionOf(crossAxis), availableInnerCrossDim))
                                    {
                                        var childWidth = isMainAxisRow
                                            ? (child.Layout.MeasuredDimension(Dimension.Width) + child.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth))
                                            : leadPerLine + lineHeight;

                                        var childHeight = !isMainAxisRow
                                            ? (child.Layout.MeasuredDimension(Dimension.Height) + child.Style.ComputeMarginForAxis(crossAxis, availableInnerWidth))
                                            : leadPerLine + lineHeight;

                                        if (!(Num.InexactEquals(childWidth, child.Layout.MeasuredDimension(Dimension.Width))
                                            && Num.InexactEquals(childHeight, child.Layout.MeasuredDimension(Dimension.Height))))
                                        {
                                            CalculateLayoutInternal(
                                                child,
                                                childWidth,
                                                childHeight,
                                                direction,
                                                SizingMode.StretchFit,
                                                SizingMode.StretchFit,
                                                availableInnerWidth,
                                                availableInnerHeight,
                                                true,
                                                depth,
                                                generationCount
                                            );
                                        }
                                    }
                                    break;
                                }

                            case Align.Baseline:
                                child.Layout.SetPosition(PhysicalEdge.Top,
                                    currentLead + maxAscentForCurrentLine - CalculateBaseline(child) + child.Style.ComputeFlexStartPosition(FlexDirection.Column, direction, availableInnerCrossDim));
                                break;

                            case Align.Auto:
                            case Align.SpaceBetween:
                            case Align.SpaceAround:
                            case Align.SpaceEvenly:
                                break;
                        }
                    }
                }

                currentLead = currentLead + leadPerLine + lineHeight;
            }
        }

        node.Layout.SetMeasuredDimension(Dimension.Width, BoundAxis(node, FlexDirection.Row, direction, availableWidth - marginAxisRow, ownerWidth, ownerWidth));
        node.Layout.SetMeasuredDimension(Dimension.Height, BoundAxis(node, FlexDirection.Column, direction, availableHeight - marginAxisColumn, ownerHeight, ownerWidth));

        if (sizingModeMainDim == SizingMode.MaxContent || (node.Style.Overflow != Overflow.Scroll && sizingModeMainDim == SizingMode.FitContent))
        {
            node.Layout.SetMeasuredDimension(Axis.DimensionOf(mainAxis), BoundAxis(node, mainAxis, direction, maxLineMainDim, mainAxisOwnerSize, ownerWidth));
        }
        else if (sizingModeMainDim == SizingMode.FitContent && node.Style.Overflow == Overflow.Scroll)
        {
            node.Layout.SetMeasuredDimension(Axis.DimensionOf(mainAxis), Num.MaxOrDefined(
                Num.MinOrDefined(availableInnerMainDim + paddingAndBorderAxisMain, BoundAxisWithinMinAndMax(node, direction, mainAxis, maxLineMainDim, mainAxisOwnerSize, ownerWidth)),
                paddingAndBorderAxisMain));
        }

        if (computedScope != MeasureScope.Both)
        {
            node.Layout.SetMeasuredDimension(Axis.DimensionOf(crossAxis), Num.Undefined);
        }
        else if (sizingModeCrossDim == SizingMode.MaxContent || (node.Style.Overflow != Overflow.Scroll && sizingModeCrossDim == SizingMode.FitContent))
        {
            node.Layout.SetMeasuredDimension(Axis.DimensionOf(crossAxis), BoundAxis(node, crossAxis, direction, totalLineCrossDim + paddingAndBorderAxisCross, crossAxisOwnerSize, ownerWidth));
        }
        else if (sizingModeCrossDim == SizingMode.FitContent && node.Style.Overflow == Overflow.Scroll)
        {
            node.Layout.SetMeasuredDimension(Axis.DimensionOf(crossAxis), Num.MaxOrDefined(
                Num.MinOrDefined(
                    availableInnerCrossDim + paddingAndBorderAxisCross,
                    BoundAxisWithinMinAndMax(
                        node,
                        direction,
                        crossAxis,
                        totalLineCrossDim + paddingAndBorderAxisCross,
                        crossAxisOwnerSize,
                        ownerWidth
                    )
                ),
                paddingAndBorderAxisCross));
        }

        if (performLayout && node.Style.FlexWrap == Wrap.WrapReverse)
        {
            foreach (var child in children)
            {
                if (!child.Style.IsOutOfFlow)
                {
                    child.Layout.SetPosition(Axis.FlexStartEdge(crossAxis),
                        node.Layout.MeasuredDimension(Axis.DimensionOf(crossAxis)) - child.Layout.Position(Axis.FlexStartEdge(crossAxis)) - child.Layout.MeasuredDimension(Axis.DimensionOf(crossAxis)));
                }
            }
        }

        if (performLayout)
        {
            var needsMainTrailingPos = Axis.NeedsTrailingPosition(mainAxis);
            var needsCrossTrailingPos = Axis.NeedsTrailingPosition(crossAxis);

            if (needsMainTrailingPos || needsCrossTrailingPos)
            {
                foreach (var child in children)
                {
                    if (child.Style.Display == Display.None || child.Style.IsOutOfFlow)
                    {
                        continue;
                    }

                    if (needsMainTrailingPos)
                    {
                        Axis.SetChildTrailingPosition(node, child, mainAxis);
                    }

                    if (needsCrossTrailingPos)
                    {
                        Axis.SetChildTrailingPosition(node, child, crossAxis);
                    }
                }
            }

            if (node.Style.PositionType != PositionType.Static || node.AlwaysFormsContainingBlock || depth == 1)
            {
                LayoutAbsoluteDescendants(
                    node,
                    node,
                    isMainAxisRow ? sizingModeMainDim : sizingModeCrossDim,
                    direction,
                    depth,
                    generationCount,
                    0.0f,
                    0.0f,
                    availableInnerWidth,
                    availableInnerHeight
                );
            }
        }

        ReturnList(childBuffer);
        return computedScope;
    }
}
