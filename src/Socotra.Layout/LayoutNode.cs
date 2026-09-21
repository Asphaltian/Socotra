using System.Collections.ObjectModel;

namespace Socotra.Layout;

internal delegate LayoutSize MeasureFunc(
    LayoutNode node,
    float width,
    MeasureMode widthMode,
    float height,
    MeasureMode heightMode);

internal delegate float BaselineFunc(LayoutNode node, float width, float height);

internal sealed class LayoutNode
{
    private static uint _currentGenerationCount;

    internal static uint NextGeneration() => Interlocked.Increment(ref _currentGenerationCount);

    private static readonly List<LayoutNode> EmptyChildren = new();
    private List<LayoutNode>? _children;
    private ReadOnlyCollection<LayoutNode>? _readOnlyChildren;
    private LayoutResults _layout = LayoutResults.Create();
    private LayoutNode? _owner;
    private MeasureFunc? _measureFunc;
    private IInlineContent? _inlineContent;
    private BaselineFunc? _baselineFunc;
    private bool _isReferenceBaseline;
    private bool _alwaysFormsContainingBlock;
    private bool _isDirty = true;
    private int _contentsChildrenCount;
    private int _baselineUsers;
    private int _reuseBlockers;
    private int _fixedNodes;
    private bool _isFixed;
    private bool _usesBaseline;
    private bool _blocksReuse;
    private bool _alignsSelfByBaseline;
    private bool _alignsItemsByBaseline;
    private bool _usesPercent;
    private int _baselineSelfChildren;
    private StyleLength _processedWidth = StyleLength.Undefined;
    private StyleLength _processedHeight = StyleLength.Undefined;

    public LayoutNode()
    {
        Style = new LayoutStyle(this);
    }

    public LayoutStyle Style { get; }

    internal ref LayoutResults Layout => ref _layout;

    public object? Context { get; set; }

    public bool HasNewLayout { get; set; } = true;

    public bool IsDirty => _isDirty;

    public bool IsReferenceBaseline
    {
        get => _isReferenceBaseline;
        set
        {
            if (_isReferenceBaseline == value)
            {
                return;
            }

            _isReferenceBaseline = value;
            MarkDirtyAndPropagate();
        }
    }

    public bool AlwaysFormsContainingBlock
    {
        get => _alwaysFormsContainingBlock;
        set
        {
            if (_alwaysFormsContainingBlock == value)
            {
                return;
            }

            _alwaysFormsContainingBlock = value;
            MarkDirtyAndPropagate();
        }
    }

    public Action<LayoutNode>? DirtiedCallback { get; set; }

    internal int LineIndex { get; set; }

    public LayoutNode? Owner => _owner;

    public IReadOnlyList<LayoutNode> Children => _children is null
        ? Array.Empty<LayoutNode>()
        : _readOnlyChildren ??= _children.AsReadOnly();

    public int ChildCount => _children?.Count ?? 0;
    public LayoutNode GetChild(int index) => ChildrenOrEmpty[index];

    internal List<LayoutNode> ChildList => ChildrenOrEmpty;

    private List<LayoutNode> ChildrenOrEmpty => _children ?? EmptyChildren;

    public void AddChild(LayoutNode child) => InsertChild(child, ChildCount);

    public void InsertChild(LayoutNode child, int index)
    {
        ArgumentNullException.ThrowIfNull(child);

        for (LayoutNode? ancestor = this; ancestor is not null; ancestor = ancestor._owner)
        {
            if (ReferenceEquals(ancestor, child))
            {
                throw new InvalidOperationException("Cannot create a cycle in the layout tree.");
            }
        }

        if (child._owner is not null)
        {
            throw new InvalidOperationException("Child already has an owner; it must be removed first.");
        }

        if (HasMeasureFunc)
        {
            throw new InvalidOperationException("Cannot add child: Nodes with measure functions cannot have children.");
        }

        _children ??= new List<LayoutNode>();
        if ((uint)index > (uint)_children.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (child.Style.Display == Display.Contents)
        {
            _contentsChildrenCount++;
        }

        _children.Insert(index, child);
        child._owner = this;
        AddSubtreeCounts(child._baselineUsers, child._reuseBlockers, child._fixedNodes);
        if (child._alignsSelfByBaseline)
        {
            _baselineSelfChildren++;
        }

        MarkDirtyAndPropagate();
    }

    public bool RemoveChild(LayoutNode child)
    {
        if (child is null || _children is null)
        {
            return false;
        }

        int index = _children.IndexOf(child);
        if (index < 0)
        {
            return false;
        }

        RemoveChildAt(index);
        return true;
    }

    public void RemoveChildAt(int index)
    {
        LayoutNode child = ChildrenOrEmpty[index];
        if (child.Style.Display == Display.Contents)
        {
            _contentsChildrenCount--;
        }

        _children!.RemoveAt(index);

        child.Layout.Reset();
        child._owner = null;
        AddSubtreeCounts(-child._baselineUsers, -child._reuseBlockers, -child._fixedNodes);
        if (child._alignsSelfByBaseline)
        {
            _baselineSelfChildren--;
        }

        MarkDirtyAndPropagate();
    }

    public void RemoveAllChildren()
    {
        if (ChildCount == 0)
        {
            return;
        }

        int baselineUsers = 0;
        int reuseBlockers = 0;
        int fixedNodes = 0;
        foreach (LayoutNode child in _children!)
        {
            child.Layout.Reset();
            child._owner = null;
            baselineUsers += child._baselineUsers;
            reuseBlockers += child._reuseBlockers;
            fixedNodes += child._fixedNodes;
        }

        _children!.Clear();
        _contentsChildrenCount = 0;
        _baselineSelfChildren = 0;
        AddSubtreeCounts(-baselineUsers, -reuseBlockers, -fixedNodes);
        MarkDirtyAndPropagate();
    }

    internal bool SubtreeUsesBaseline => _baselineUsers > 0;

    internal bool SubtreeBlocksMeasureReuse => _reuseBlockers > 0;

    internal bool SubtreeHasFixed => _fixedNodes > 0;

    internal bool IsBaselineContainer
    {
        get
        {
            if (_baselineSelfChildren > 0 || _alignsItemsByBaseline)
            {
                return true;
            }

            if (_contentsChildrenCount == 0)
            {
                return false;
            }

            foreach (LayoutNode child in ChildrenOrEmpty)
            {
                if (child.Style.Display == Display.Contents && child.SubtreeUsesBaseline)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal bool HasSingleStickyFlexChild(uint generationCount)
    {
        ref LayoutResults layout = ref _layout;
        if (layout.StickyFlexGeneration == generationCount)
        {
            return layout.HasSingleStickyFlexChild;
        }

        int flexible = 0;
        int sticky = 0;
        foreach (LayoutNode child in ChildrenOrEmpty)
        {
            if (!child.IsNodeFlexible())
            {
                continue;
            }

            flexible++;
            if (!Num.InexactEquals(child.ResolveFlexGrow(), 0.0f)
                && !Num.InexactEquals(child.ResolveFlexShrink(), 0.0f)
                && !child.ProcessFlexBasis().IsAuto)
            {
                sticky++;
            }
        }

        layout.StickyFlexGeneration = generationCount;
        return layout.HasSingleStickyFlexChild = _contentsChildrenCount > 0
            || (flexible == 1 && sticky == 1);
    }

    private void AddSubtreeCounts(int baselineUsers, int reuseBlockers, int fixedNodes = 0)
    {
        if (baselineUsers == 0 && reuseBlockers == 0 && fixedNodes == 0)
        {
            return;
        }

        for (LayoutNode? node = this; node is not null; node = node._owner)
        {
            node._baselineUsers += baselineUsers;
            node._reuseBlockers += reuseBlockers;
            node._fixedNodes += fixedNodes;
        }
    }

    internal bool NeedsExactPasses => _baselineUsers > 0 || _usesPercent;

    internal void OnStyleFlagsChanged()
    {
        bool isFixed = Style.PositionType == PositionType.Fixed;
        if (isFixed != _isFixed)
        {
            AddSubtreeCounts(0, 0, isFixed ? 1 : -1);
            _isFixed = isFixed;
        }

        _alignsItemsByBaseline = Style.AlignItems == Align.Baseline
            || Style.JustifyItems == Align.Baseline;
        _usesPercent = Style.UsesPercentages;

        bool alignsSelf = Style.AlignsSelfByBaseline;
        if (alignsSelf != _alignsSelfByBaseline)
        {
            _alignsSelfByBaseline = alignsSelf;
            if (_owner is not null)
            {
                _owner._baselineSelfChildren += alignsSelf ? 1 : -1;
            }
        }

        bool usesBaseline = Style.UsesBaselineAlignment;
        bool blocksReuse = Style.BlocksMeasureReuse();
        if (usesBaseline == _usesBaseline && blocksReuse == _blocksReuse)
        {
            return;
        }

        AddSubtreeCounts(
            usesBaseline == _usesBaseline ? 0 : usesBaseline ? 1 : -1,
            blocksReuse == _blocksReuse ? 0 : blocksReuse ? 1 : -1);
        _usesBaseline = usesBaseline;
        _blocksReuse = blocksReuse;
    }

    internal bool HasContentsChildren => _contentsChildrenCount > 0;

    internal void OnChildDisplayChanged(bool wasContents, bool isContents)
    {
        if (_owner is null || wasContents == isContents)
        {
            return;
        }

        _owner._contentsChildrenCount += isContents ? 1 : -1;
    }

    internal List<LayoutNode> GetLayoutChildren(List<LayoutNode> buffer)
    {
        if (_contentsChildrenCount == 0)
        {
            return ChildrenOrEmpty;
        }

        buffer.Clear();
        CollectLayoutChildren(this, buffer);
        return buffer;
    }

    private static void CollectLayoutChildren(LayoutNode node, List<LayoutNode> into)
    {
        foreach (LayoutNode child in node.ChildrenOrEmpty)
        {
            if (child.Style.Display == Display.Contents)
            {
                if (child.ChildCount > 0)
                {
                    CollectLayoutChildren(child, into);
                }
            }
            else
            {
                into.Add(child);
            }
        }
    }

    internal int LayoutChildCount
    {
        get
        {
            if (_contentsChildrenCount == 0)
            {
                return ChildCount;
            }

            int count = 0;
            foreach (LayoutNode child in ChildrenOrEmpty)
            {
                if (child.Style.Display == Display.Contents)
                {
                    count += child.LayoutChildCount;
                }
                else
                {
                    count++;
                }
            }

            return count;
        }
    }

    public MeasureFunc? MeasureFunc
    {
        get => _measureFunc;
        set
        {
            if (_measureFunc == value)
            {
                return;
            }

            if (value is not null)
            {
                if (InlineContent is not null)
                {
                    throw new InvalidOperationException("Inline content and leaf measurement are mutually exclusive.");
                }

                if (ChildCount > 0)
                {
                    throw new InvalidOperationException("Cannot set measure function: Nodes with measure functions cannot have children.");
                }
            }

            _measureFunc = value;
            MarkDirtyAndPropagate();
        }
    }

    public bool HasMeasureFunc => _measureFunc is not null;

    public IInlineContent? InlineContent
    {
        get => _inlineContent;
        set
        {
            if (ReferenceEquals(_inlineContent, value))
            {
                return;
            }

            if (value is not null && HasMeasureFunc)
            {
                throw new InvalidOperationException("Inline content and leaf measurement are mutually exclusive.");
            }

            _inlineContent = value;
            MarkDirtyAndPropagate();
        }
    }

    public IReadOnlyList<InlineFragment> InlineFragments { get; internal set; } = Array.Empty<InlineFragment>();

    public BaselineFunc? BaselineFunc
    {
        get => _baselineFunc;
        set
        {
            if (_baselineFunc == value)
            {
                return;
            }

            _baselineFunc = value;
            MarkDirtyAndPropagate();
        }
    }

    public bool HasBaselineFunc => _baselineFunc is not null;

    internal LayoutSize Measure(float availableWidth, MeasureMode widthMode, float availableHeight, MeasureMode heightMode)
    {
        LayoutSize size = _measureFunc!(this, availableWidth, widthMode, availableHeight, heightMode);

        if (Num.IsUndefined(size.Height)
            || size.Height < 0
            || Num.IsUndefined(size.Width)
            || size.Width < 0)
        {
            return new LayoutSize(Num.MaxOrDefined(0.0f, size.Width), Num.MaxOrDefined(0.0f, size.Height));
        }

        return size;
    }

    internal float Baseline(float width, float height) => _baselineFunc!(this, width, height);

    public void MarkDirty() => MarkDirtyAndPropagate();

    internal void MarkDirtyAndPropagate()
    {
        if (_isDirty)
        {
            return;
        }

        SetDirty(true);
        Layout.ComputedFlexBasis = Num.Undefined;
        _owner?.MarkDirtyAndPropagate();
    }

    internal void SetDirty(bool isDirty)
    {
        if (isDirty == _isDirty)
        {
            return;
        }

        _isDirty = isDirty;
        if (isDirty)
        {
            DirtiedCallback?.Invoke(this);
        }
    }

    public void Reset()
    {
        if (ChildCount > 0)
        {
            throw new InvalidOperationException("Cannot reset a node which still has children attached");
        }

        if (_owner is not null)
        {
            throw new InvalidOperationException("Cannot reset a node still attached to an owner");
        }

        DirtiedCallback = null;
        Style.CopyFrom(new LayoutStyle(null));
        Layout.Reset();
        _children = null;
        _readOnlyChildren = null;
        _measureFunc = null;
        _inlineContent = null;
        InlineFragments = Array.Empty<InlineFragment>();
        _baselineFunc = null;
        Context = null;
        HasNewLayout = true;
        _isDirty = true;
        _isReferenceBaseline = false;
        _alwaysFormsContainingBlock = false;
        LineIndex = 0;
        _processedWidth = StyleLength.Undefined;
        _processedHeight = StyleLength.Undefined;
    }

    public void CalculateLayout(float ownerWidth = float.NaN, float ownerHeight = float.NaN, Direction ownerDirection = Direction.LTR)
    {
        LayoutAlgorithm.CalculateLayout(this, ownerWidth, ownerHeight, ownerDirection);
    }

    public float LayoutLeft => Layout.Position(PhysicalEdge.Left);
    public float LayoutTop => Layout.Position(PhysicalEdge.Top);
    public float LayoutWidth => Layout.Dimension(Dimension.Width);
    public float LayoutHeight => Layout.Dimension(Dimension.Height);
    public bool LayoutHadOverflow => Layout.HadOverflow;

    public float LayoutMargin(PhysicalEdge edge) => Layout.Margin(edge);
    public float LayoutBorder(PhysicalEdge edge) => Layout.Border(edge);
    public float LayoutPadding(PhysicalEdge edge) => Layout.Padding(edge);

    internal float DimensionWithMargin(FlexDirection axis, float widthSize)
    {
        return Layout.MeasuredDimension(Axis.DimensionOf(axis))
            + Style.ComputeMarginForAxis(axis, widthSize);
    }

    internal bool IsLayoutDimensionDefined(FlexDirection axis)
    {
        float value = Layout.MeasuredDimension(Axis.DimensionOf(axis));
        return Num.IsDefined(value) && value >= 0.0f;
    }

    internal bool HasDefiniteLength(Dimension dimension, float ownerSize)
    {
        float usedValue = GetProcessedDimension(dimension).Resolve(ownerSize);
        return Num.IsDefined(usedValue) && usedValue >= 0.0f;
    }

    internal StyleLength GetProcessedDimension(Dimension dimension) => dimension == Dimension.Width ? _processedWidth : _processedHeight;

    internal float GetResolvedDimension(Direction direction, Dimension dimension, float referenceLength, float ownerWidth)
    {
        float value = GetProcessedDimension(dimension).Resolve(referenceLength);
        if (Style.BoxSizing == BoxSizing.BorderBox)
        {
            return value;
        }

        float paddingAndBorder = Style.ComputePaddingAndBorderForDimension(direction, dimension, ownerWidth);
        return value + (Num.IsDefined(paddingAndBorder) ? paddingAndBorder : 0.0f);
    }

    internal StyleLength ProcessFlexBasis()
    {
        StyleLength flexBasis = Style.FlexBasis;
        if (flexBasis.Unit != Unit.Auto && flexBasis.Unit != Unit.Undefined)
        {
            return flexBasis;
        }

        return StyleLength.Auto;
    }

    internal float ResolveFlexBasis(Direction direction, FlexDirection flexDirection, float referenceLength, float ownerWidth)
    {
        float value = ProcessFlexBasis().Resolve(referenceLength);
        if (Style.BoxSizing == BoxSizing.BorderBox)
        {
            return value;
        }

        Dimension dim = Axis.DimensionOf(flexDirection);
        float paddingAndBorder = Style.ComputePaddingAndBorderForDimension(direction, dim, ownerWidth);
        return value + (Num.IsDefined(paddingAndBorder) ? paddingAndBorder : 0.0f);
    }

    internal void ProcessDimensions()
    {
        _processedWidth = ProcessDimension(Dimension.Width);
        _processedHeight = ProcessDimension(Dimension.Height);
    }

    private StyleLength ProcessDimension(Dimension dim)
    {
        StyleLength max = Style.GetMaxDimension(dim);
        if (max.IsDefined && StyleLength.InexactEquals(max, Style.GetMinDimension(dim)))
        {
            return max;
        }

        return Style.GetDimension(dim);
    }

    internal Direction ResolveDirection(Direction ownerDirection)
    {
        if (Style.Direction == Direction.Inherit)
        {
            return ownerDirection != Direction.Inherit ? ownerDirection : Direction.LTR;
        }

        return Style.Direction;
    }

    internal float ResolveFlexGrow()
    {
        if (_owner is null)
        {
            return 0.0f;
        }

        if (Num.IsDefined(Style.FlexGrow))
        {
            return Style.FlexGrow;
        }

        if (Num.IsDefined(Style.Flex) && Style.Flex > 0.0f)
        {
            return Style.Flex;
        }

        return LayoutStyle.DefaultFlexGrow;
    }

    internal float ResolveFlexShrink()
    {
        if (_owner is null)
        {
            return 0.0f;
        }

        return Num.UnwrapOrDefault(Style.FlexShrink, LayoutStyle.DefaultFlexShrink);
    }

    internal bool IsNodeFlexible()
    {
        return !Style.IsOutOfFlow
            && (ResolveFlexGrow() != 0 || ResolveFlexShrink() != 0);
    }

    private float RelativePosition(FlexDirection axis, Direction direction, float axisSize)
    {
        if (Style.PositionType == PositionType.Static)
        {
            return 0;
        }

        if (Style.IsInlineStartPositionDefined(axis, direction)
            && !Style.IsInlineStartPositionAuto(axis, direction))
        {
            return Style.ComputeInlineStartPosition(axis, direction, axisSize);
        }

        return -1 * Style.ComputeInlineEndPosition(axis, direction, axisSize);
    }

    internal void SetPosition(Direction direction, float ownerWidth, float ownerHeight)
    {
        Direction directionRespectingRoot = _owner is not null ? direction : Direction.LTR;
        FlexDirection mainAxis = Axis.ResolveDirection(Style.FlexDirection, directionRespectingRoot);
        FlexDirection crossAxis = Axis.ResolveCrossDirection(mainAxis, directionRespectingRoot);

        float relativePositionMain = RelativePosition(
            mainAxis,
            directionRespectingRoot,
            Axis.IsRow(mainAxis) ? ownerWidth : ownerHeight);
        float relativePositionCross = RelativePosition(
            crossAxis,
            directionRespectingRoot,
            Axis.IsRow(mainAxis) ? ownerHeight : ownerWidth);

        PhysicalEdge mainAxisLeadingEdge = Axis.InlineStartEdge(mainAxis, direction);
        PhysicalEdge mainAxisTrailingEdge = Axis.InlineEndEdge(mainAxis, direction);
        PhysicalEdge crossAxisLeadingEdge = Axis.InlineStartEdge(crossAxis, direction);
        PhysicalEdge crossAxisTrailingEdge = Axis.InlineEndEdge(crossAxis, direction);

        Layout.SetPosition(
            mainAxisLeadingEdge,
            Style.ComputeInlineStartMargin(mainAxis, direction, ownerWidth) + relativePositionMain);
        Layout.SetPosition(
            mainAxisTrailingEdge,
            Style.ComputeInlineEndMargin(mainAxis, direction, ownerWidth) + relativePositionMain);
        Layout.SetPosition(
            crossAxisLeadingEdge,
            Style.ComputeInlineStartMargin(crossAxis, direction, ownerWidth) + relativePositionCross);
        Layout.SetPosition(
            crossAxisTrailingEdge,
            Style.ComputeInlineEndMargin(crossAxis, direction, ownerWidth) + relativePositionCross);
    }

    public override string ToString()
    {
        return $"LayoutNode [{LayoutLeft}, {LayoutTop}, {LayoutWidth} x {LayoutHeight}] children={ChildCount}"
            + (Context is not null ? $" ({Context})" : "");
    }
}
