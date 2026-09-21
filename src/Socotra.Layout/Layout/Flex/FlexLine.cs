namespace Socotra.Layout;

internal struct FlexLineRunningLayout
{
    public float TotalFlexGrowFactors;

    public float TotalFlexShrinkScaledFactors;

    public float RemainingFreeSpace;

    public float MainDimension;

    public float CrossDimension;
}

internal sealed class FlexLine
{
    public readonly List<LayoutNode> ItemsInFlow = new();

    public float SizeConsumed;

    public int NumberOfAutoMargins;

    public FlexLineRunningLayout Layout;

    public void Reset()
    {
        ItemsInFlow.Clear();
        SizeConsumed = 0;
        NumberOfAutoMargins = 0;
        Layout = default;
    }
}

internal static partial class FlexLayout
{
    private static void CalculateFlexLine(
        FlexLine flexLine,
        LayoutNode node,
        Direction ownerDirection,
        float ownerWidth,
        float mainAxisOwnerSize,
        float availableInnerWidth,
        float availableInnerMainDim,
        List<LayoutNode> children,
        ref int childIndex,
        int lineCount)
    {
        flexLine.Reset();

        float sizeConsumed = 0.0f;
        float totalFlexGrowFactors = 0.0f;
        float totalFlexShrinkScaledFactors = 0.0f;
        int numberOfAutoMargins = 0;
        LayoutNode? firstElementInLine = null;

        float sizeConsumedIncludingMinConstraint = 0;
        var direction = node.ResolveDirection(ownerDirection);
        var mainAxis = Axis.ResolveDirection(node.Style.FlexDirection, direction);
        var isNodeFlexWrap = node.Style.FlexWrap != Wrap.NoWrap;
        var gap = node.Style.ComputeGapForAxis(mainAxis, availableInnerMainDim);

        for (; childIndex < children.Count; childIndex++)
        {
            var child = children[childIndex];
            if (child.Style.Display == Display.None || child.Style.IsOutOfFlow)
            {
                continue;
            }

            firstElementInLine ??= child;

            if (child.Style.FlexStartMarginIsAuto(mainAxis, ownerDirection))
            {
                numberOfAutoMargins++;
            }

            if (child.Style.FlexEndMarginIsAuto(mainAxis, ownerDirection))
            {
                numberOfAutoMargins++;
            }

            child.LineIndex = lineCount;
            var childMarginMainAxis = child.Style.ComputeMarginForAxis(mainAxis, availableInnerWidth);
            var childLeadingGapMainAxis = child == firstElementInLine ? 0.0f : gap;
            var flexBasisWithMinAndMaxConstraints = LayoutAlgorithm.BoundAxisWithinMinAndMax(
                child,
                direction,
                mainAxis,
                child.Layout.ComputedFlexBasis,
                mainAxisOwnerSize,
                ownerWidth
            );

            if (sizeConsumedIncludingMinConstraint
                + flexBasisWithMinAndMaxConstraints
                + childMarginMainAxis
                + childLeadingGapMainAxis > availableInnerMainDim
                && isNodeFlexWrap
                && flexLine.ItemsInFlow.Count > 0)
            {
                break;
            }

            sizeConsumedIncludingMinConstraint += flexBasisWithMinAndMaxConstraints + childMarginMainAxis + childLeadingGapMainAxis;
            sizeConsumed += flexBasisWithMinAndMaxConstraints + childMarginMainAxis + childLeadingGapMainAxis;

            if (child.IsNodeFlexible())
            {
                totalFlexGrowFactors += child.ResolveFlexGrow();

                totalFlexShrinkScaledFactors += -child.ResolveFlexShrink() * child.Layout.ComputedFlexBasis;
            }

            flexLine.ItemsInFlow.Add(child);
        }

        if (totalFlexGrowFactors > 0 && totalFlexGrowFactors < 1)
        {
            totalFlexGrowFactors = 1;
        }

        if (totalFlexShrinkScaledFactors > 0 && totalFlexShrinkScaledFactors < 1)
        {
            totalFlexShrinkScaledFactors = 1;
        }

        flexLine.SizeConsumed = sizeConsumed;
        flexLine.NumberOfAutoMargins = numberOfAutoMargins;
        flexLine.Layout.TotalFlexGrowFactors = totalFlexGrowFactors;
        flexLine.Layout.TotalFlexShrinkScaledFactors = totalFlexShrinkScaledFactors;
    }
}
