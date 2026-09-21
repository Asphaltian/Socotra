using System.Runtime.CompilerServices;

namespace Socotra.Layout;

internal static partial class LayoutAlgorithm
{
    internal static void NoteFlexBasisChange(LayoutNode child, float previousFlexBasis)
    {
        if (Num.IsUndefined(previousFlexBasis) || Num.OptionalEquals(previousFlexBasis, child.Layout.ComputedFlexBasis))
        {
            return;
        }

        for (var node = child.Owner; node is not null; node = node.Owner)
        {
            ref var layout = ref node.Layout;
            for (int i = 0; i < layout.NextCachedMeasurementsIndex; i++)
            {
                layout.CachedMeasurements[i].ContentBased = false;
            }
        }
    }

    [ThreadStatic] private static int _nonContentMeasurements;

    internal static bool CalculateLayoutInternal(
        LayoutNode node,
        float availableWidth,
        float availableHeight,
        Direction ownerDirection,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight,
        bool performLayout,
        int depth,
        uint generationCount,
        MeasureScope scope = MeasureScope.Both)
    {
        ref var layout = ref node.Layout;

        depth++;

        var needToVisitNode = (node.IsDirty && layout.GenerationCount != generationCount)
            || layout.LastOwnerDirection != ownerDirection;

        if (!needToVisitNode && layout.GenerationCount != generationCount && node.Style.Display == Display.Flex)
        {
            var style = node.Style;
            needToVisitNode = style.MinWidth.Resolve(ownerWidth) > style.MaxWidth.Resolve(ownerWidth)
                || style.MinHeight.Resolve(ownerHeight) > style.MaxHeight.Resolve(ownerHeight);
        }

        if (needToVisitNode)
        {
            InvalidateCaches(ref layout);
        }

        if (scope != MeasureScope.Both)
        {
            if (performLayout || node.ChildCount == 0 || layout.BaselineSensitive || node.NeedsExactPasses)
            {
                scope = MeasureScope.Both;
            }
            else if (IsAnsweredByAvailableSize(node, scope, widthSizingMode, heightSizingMode, generationCount))
            {
                MeasureFromAvailableSize(node, availableWidth, availableHeight, ownerDirection, widthSizingMode, heightSizingMode, ownerWidth, ownerHeight);
                layout.LastOwnerDirection = ownerDirection;
                layout.GenerationCount = generationCount;
                return true;
            }
        }

        int cachedResults = -1;

        if (node.HasMeasureFunc)
        {
            cachedResults = FindCachedMeasureFuncResult(node, ref layout, availableWidth, availableHeight, widthSizingMode, heightSizingMode, ownerWidth, ownerHeight);
        }
        else if (performLayout)
        {
            ref var cachedLayout = ref layout.CachedLayout;
            if (Num.InexactEquals(cachedLayout.AvailableWidth, availableWidth)
                && Num.InexactEquals(cachedLayout.AvailableHeight, availableHeight)
                && Num.InexactEquals(cachedLayout.OwnerWidth, ownerWidth)
                && Num.InexactEquals(cachedLayout.OwnerHeight, ownerHeight)
                && cachedLayout.WidthSizingMode == widthSizingMode
                && cachedLayout.HeightSizingMode == heightSizingMode)
            {
                cachedResults = -2;
            }
        }
        else if (layout.NextCachedMeasurementsIndex > 0)
        {
            for (int i = 0; i < layout.NextCachedMeasurementsIndex; i++)
            {
                ref var cachedMeasurement = ref layout.CachedMeasurements[i];
                if ((cachedMeasurement.Scope == MeasureScope.Both || cachedMeasurement.Scope == scope)
                    && Num.InexactEquals(cachedMeasurement.AvailableWidth, availableWidth)
                    && Num.InexactEquals(cachedMeasurement.AvailableHeight, availableHeight)
                    && Num.InexactEquals(cachedMeasurement.OwnerWidth, ownerWidth)
                    && Num.InexactEquals(cachedMeasurement.OwnerHeight, ownerHeight)
                    && cachedMeasurement.WidthSizingMode == widthSizingMode
                    && cachedMeasurement.HeightSizingMode == heightSizingMode)
                {
                    cachedResults = i;
                    break;
                }
            }

            if (cachedResults == -1 && scope != MeasureScope.Both)
            {
                cachedResults = TryReuseContainerMeasurement(node, ref layout, availableWidth, availableHeight, widthSizingMode, heightSizingMode, ownerWidth, ownerHeight, scope);
            }
        }

        if (!needToVisitNode && cachedResults != -1)
        {
            ref var cached = ref (cachedResults == -2 ? ref layout.CachedLayout : ref layout.CachedMeasurements[cachedResults]);
            layout.SetMeasuredDimension(Dimension.Width, cached.ComputedWidth);
            layout.SetMeasuredDimension(Dimension.Height, cached.ComputedHeight);
            layout.HadOverflow = cached.HadOverflow;

            if (!cached.ContentBased)
            {
                _nonContentMeasurements++;
            }
        }
        else
        {
            var nonContentBefore = _nonContentMeasurements;
            var computedScope = CalculateLayoutImpl(
                node,
                availableWidth,
                availableHeight,
                ownerDirection,
                widthSizingMode,
                heightSizingMode,
                ownerWidth,
                ownerHeight,
                performLayout,
                depth,
                generationCount,
                scope
            );

            layout.LastOwnerDirection = ownerDirection;

            if (cachedResults == -1)
            {
                if (layout.NextCachedMeasurementsIndex == LayoutResults.MaxCachedMeasurements)
                {
                    layout.NextCachedMeasurementsIndex = 0;
                }

                ref var newCacheEntry = ref layout.CachedLayout;
                if (!performLayout)
                {
                    newCacheEntry = ref layout.CachedMeasurements[layout.NextCachedMeasurementsIndex];
                    layout.NextCachedMeasurementsIndex++;
                }

                newCacheEntry.AvailableWidth = availableWidth;
                newCacheEntry.AvailableHeight = availableHeight;
                newCacheEntry.OwnerWidth = ownerWidth;
                newCacheEntry.OwnerHeight = ownerHeight;
                newCacheEntry.WidthSizingMode = widthSizingMode;
                newCacheEntry.HeightSizingMode = heightSizingMode;
                newCacheEntry.ComputedWidth = layout.MeasuredDimension(Dimension.Width);
                newCacheEntry.ComputedHeight = layout.MeasuredDimension(Dimension.Height);
                newCacheEntry.Scope = computedScope;
                newCacheEntry.ContentBased = _nonContentMeasurements == nonContentBefore;
                newCacheEntry.HadOverflow = layout.HadOverflow;
            }
        }

        if (performLayout)
        {
            node.Layout.SetDimension(Dimension.Width, node.Layout.MeasuredDimension(Dimension.Width));
            node.Layout.SetDimension(Dimension.Height, node.Layout.MeasuredDimension(Dimension.Height));

            node.HasNewLayout = true;
            node.SetDirty(false);
        }

        layout.GenerationCount = generationCount;

        return needToVisitNode || cachedResults == -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FindCachedMeasureFuncResult(
        LayoutNode node,
        ref LayoutResults layout,
        float availableWidth,
        float availableHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight)
    {
        var marginAxisRow = node.Style.ComputeMarginForAxis(FlexDirection.Row, ownerWidth);
        var marginAxisColumn = node.Style.ComputeMarginForAxis(FlexDirection.Column, ownerWidth);

        ref var cachedLayout = ref layout.CachedLayout;
        if ((!node.Style.UsesPercentages || OwnerSizesMatch(ref cachedLayout, ownerWidth, ownerHeight)) && CanUseCachedMeasurement(
            widthSizingMode,
            availableWidth,
            heightSizingMode,
            availableHeight,
            cachedLayout.WidthSizingMode,
            cachedLayout.AvailableWidth,
            cachedLayout.HeightSizingMode,
            cachedLayout.AvailableHeight,
            cachedLayout.ComputedWidth,
            cachedLayout.ComputedHeight,
            marginAxisRow,
            marginAxisColumn
        ))
        {
            return -2;
        }

        for (int i = 0; i < layout.NextCachedMeasurementsIndex; i++)
        {
            ref var cachedMeasurement = ref layout.CachedMeasurements[i];
            if ((!node.Style.UsesPercentages || OwnerSizesMatch(ref cachedMeasurement, ownerWidth, ownerHeight)) && CanUseCachedMeasurement(
                widthSizingMode,
                availableWidth,
                heightSizingMode,
                availableHeight,
                cachedMeasurement.WidthSizingMode,
                cachedMeasurement.AvailableWidth,
                cachedMeasurement.HeightSizingMode,
                cachedMeasurement.AvailableHeight,
                cachedMeasurement.ComputedWidth,
                cachedMeasurement.ComputedHeight,
                marginAxisRow,
                marginAxisColumn
            ))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool OwnerSizesMatch(ref CachedMeasurement cached, float ownerWidth, float ownerHeight)
    {
        return Num.InexactEquals(cached.OwnerWidth, ownerWidth) && Num.InexactEquals(cached.OwnerHeight, ownerHeight);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TryReuseContainerMeasurement(
        LayoutNode node,
        ref LayoutResults layout,
        float availableWidth,
        float availableHeight,
        SizingMode widthSizingMode,
        SizingMode heightSizingMode,
        float ownerWidth,
        float ownerHeight,
        MeasureScope scope)
    {
        if (node.SubtreeBlocksMeasureReuse
            || (widthSizingMode != SizingMode.FitContent && heightSizingMode != SizingMode.FitContent))
        {
            return -1;
        }

        var marginAxisRow = node.Style.ComputeMarginForAxis(FlexDirection.Row, ownerWidth);
        var marginAxisColumn = node.Style.ComputeMarginForAxis(FlexDirection.Column, ownerWidth);
        if ((widthSizingMode == SizingMode.FitContent && availableWidth - marginAxisRow <= 0.0f)
            || (heightSizingMode == SizingMode.FitContent && availableHeight - marginAxisColumn <= 0.0f))
        {
            return -1;
        }

        for (int i = 0; i < layout.NextCachedMeasurementsIndex; i++)
        {
            ref var cachedMeasurement = ref layout.CachedMeasurements[i];
            if ((cachedMeasurement.Scope != MeasureScope.Both && cachedMeasurement.Scope != scope)
                || !OwnerSizesMatch(ref cachedMeasurement, ownerWidth, ownerHeight)
                || !CanReuseContainerMeasurement(
                    widthSizingMode,
                    availableWidth,
                    heightSizingMode,
                    availableHeight,
                    ref cachedMeasurement,
                    marginAxisRow,
                    marginAxisColumn,
                    cachedMeasurement.ContentBased
                ))
            {
                continue;
            }

            if (layout.NextCachedMeasurementsIndex == LayoutResults.MaxCachedMeasurements)
            {
                layout.NextCachedMeasurementsIndex = 0;
            }
            var slot = layout.NextCachedMeasurementsIndex++;
            ref var entry = ref layout.CachedMeasurements[slot];
            if (slot != i)
            {
                entry.ComputedWidth = cachedMeasurement.ComputedWidth;
                entry.ComputedHeight = cachedMeasurement.ComputedHeight;
                entry.Scope = cachedMeasurement.Scope;
                entry.ContentBased = cachedMeasurement.ContentBased;
                entry.HadOverflow = cachedMeasurement.HadOverflow;
            }
            entry.AvailableWidth = availableWidth;
            entry.AvailableHeight = availableHeight;
            entry.OwnerWidth = ownerWidth;
            entry.OwnerHeight = ownerHeight;
            entry.WidthSizingMode = widthSizingMode;
            entry.HeightSizingMode = heightSizingMode;
            return slot;
        }

        return -1;
    }

    private static void InvalidateCaches(ref LayoutResults layout)
    {
        layout.NextCachedMeasurementsIndex = 0;
        layout.CachedLayout.AvailableWidth = -1;
        layout.CachedLayout.AvailableHeight = -1;
        layout.CachedLayout.OwnerWidth = -1;
        layout.CachedLayout.OwnerHeight = -1;
        layout.CachedLayout.WidthSizingMode = SizingMode.MaxContent;
        layout.CachedLayout.HeightSizingMode = SizingMode.MaxContent;
        layout.CachedLayout.ComputedWidth = -1;
        layout.CachedLayout.ComputedHeight = -1;
    }

    private static bool SizeIsExactAndMatchesOldMeasuredSize(SizingMode sizeMode, float size, float lastSize, float lastComputedSize)
    {
        return sizeMode == SizingMode.StretchFit && Num.InexactEquals(size, lastComputedSize)
            && (Num.IsUndefined(lastSize) || lastComputedSize <= lastSize || Num.InexactEquals(lastComputedSize, lastSize));
    }

    private static bool OldSizeIsMaxContentAndStillFits(SizingMode sizeMode, float size, SizingMode lastSizeMode, float lastComputedSize)
    {
        return sizeMode == SizingMode.FitContent
            && lastSizeMode == SizingMode.MaxContent
            && (size >= lastComputedSize || Num.InexactEquals(size, lastComputedSize));
    }

    private static bool NewSizeIsStricterAndStillValid(
        SizingMode sizeMode,
        float size,
        SizingMode lastSizeMode,
        float lastSize,
        float lastComputedSize)
    {
        return lastSizeMode == SizingMode.FitContent
            && sizeMode == SizingMode.FitContent
            && Num.IsDefined(lastSize)
            && Num.IsDefined(size)
            && Num.IsDefined(lastComputedSize)
            && lastSize > size
            && (lastComputedSize <= size || Num.InexactEquals(size, lastComputedSize));
    }

    internal static bool CanUseCachedMeasurement(
        SizingMode widthMode, float availableWidth, SizingMode heightMode, float availableHeight,
        SizingMode lastWidthMode, float lastAvailableWidth, SizingMode lastHeightMode, float lastAvailableHeight,
        float lastComputedWidth, float lastComputedHeight, float marginRow, float marginColumn)
    {
        if ((Num.IsDefined(lastComputedHeight) && lastComputedHeight < 0) || (Num.IsDefined(lastComputedWidth) && lastComputedWidth < 0))
        {
            return false;
        }

        var hasSameWidthSpec = lastWidthMode == widthMode && Num.InexactEquals(lastAvailableWidth, availableWidth);
        var hasSameHeightSpec = lastHeightMode == heightMode && Num.InexactEquals(lastAvailableHeight, availableHeight);

        var widthIsCompatible = hasSameWidthSpec
            || SizeIsExactAndMatchesOldMeasuredSize(widthMode, availableWidth - marginRow, lastAvailableWidth - marginRow, lastComputedWidth)
            || OldSizeIsMaxContentAndStillFits(widthMode, availableWidth - marginRow, lastWidthMode, lastComputedWidth)
            || NewSizeIsStricterAndStillValid(widthMode, availableWidth - marginRow, lastWidthMode, lastAvailableWidth - marginRow, lastComputedWidth);

        var heightIsCompatible = hasSameHeightSpec
            || SizeIsExactAndMatchesOldMeasuredSize(heightMode, availableHeight - marginColumn, lastAvailableHeight - marginColumn, lastComputedHeight)
            || OldSizeIsMaxContentAndStillFits(heightMode, availableHeight - marginColumn, lastHeightMode, lastComputedHeight)
            || NewSizeIsStricterAndStillValid(heightMode, availableHeight - marginColumn, lastHeightMode, lastAvailableHeight - marginColumn, lastComputedHeight);

        return widthIsCompatible && heightIsCompatible;
    }

    internal static bool CanReuseContainerMeasurement(
        SizingMode widthMode,
        float availableWidth,
        SizingMode heightMode,
        float availableHeight,
        ref CachedMeasurement cached,
        float marginRow,
        float marginColumn,
        bool contentBased)
    {
        return ContainerAxisIsCompatible(
            widthMode,
            availableWidth,
            cached.WidthSizingMode,
            cached.AvailableWidth,
            cached.ComputedWidth,
            marginRow,
            contentBased
        ) && ContainerAxisIsCompatible(
            heightMode,
            availableHeight,
            cached.HeightSizingMode,
            cached.AvailableHeight,
            cached.ComputedHeight,
            marginColumn,
            contentBased
        );
    }

    private static bool ContainerAxisIsCompatible(
        SizingMode mode,
        float available,
        SizingMode lastMode,
        float lastAvailable,
        float lastComputed,
        float margin,
        bool contentBased)
    {
        if (lastMode == mode && Num.InexactEquals(lastAvailable, available))
        {
            return true;
        }

        if (!contentBased || mode != SizingMode.FitContent || Num.IsUndefined(lastComputed))
        {
            return false;
        }

        var size = available - margin;
        if (lastMode == SizingMode.MaxContent)
        {
            return size >= lastComputed || Num.InexactEquals(size, lastComputed);
        }

        if (lastMode == SizingMode.FitContent)
        {
            var lastSize = lastAvailable - margin;
            return lastSize > size && (lastComputed <= size || Num.InexactEquals(size, lastComputed));
        }

        return false;
    }
}
