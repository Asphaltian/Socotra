namespace Socotra;

public partial class Panel
{
    private readonly List<Panel> _renderChildren = [];
    private List<Panel>? _shadowChildren;
    private bool _renderChildrenDirty = true;

    internal bool IsFixed => this is not RootPanel && ComputedStyle?.Position == PositionMode.Fixed && ComputedStyle.Display != DisplayMode.Contents;

    internal Panel? VisualParent => IsFixed ? null : Parent;

    internal Panel VisualRoot
    {
        get
        {
            var panel = this;
            while (panel.VisualParent is { } parent)
            {
                panel = parent;
            }

            return panel;
        }
    }

    internal void DirtyRenderChildren()
    {
        _renderChildrenDirty = true;
        if (FindRootPanel() is { } root)
        {
            root.FixedOverlaysDirty = true;
        }
    }

    private int RenderOrderIndex => SiblingIndex + (ComputedStyle?.ZIndex ?? 0);

    private void SortRenderChildren()
    {
        if (_indexesDirty)
        {
            UpdateChildrenIndexes();
        }

        if (!_renderChildrenDirty)
        {
            return;
        }

        _renderChildren.Clear();
        _renderChildren.AddRange(Children);
        _renderChildren.Sort(static (x, y) =>
        {
            var order = x.RenderOrderIndex.CompareTo(y.RenderOrderIndex);
            return order != 0 ? order : x.SiblingIndex.CompareTo(y.SiblingIndex);
        });

        _shadowChildren?.Clear();
        foreach (var child in _renderChildren)
        {
            if (child.ComputedStyle?.BoxShadow?.Any(static s => !s.Inset && s.Color.A > 0) == true)
            {
                (_shadowChildren ??= []).Add(child);
            }
        }

        _renderChildrenDirty = false;
    }

    internal Panel? FindVisualPanelAt(Vector2 point, bool needPointerEvents, Func<Panel, bool>? match = null)
    {
        if (IsDeleted || ComputedStyle is null || !IsVisible)
        {
            return null;
        }

        point = LocalMatrix is { } local ? local.Transform(point) : point;
        var inside = IsInsideUntransformed(point);
        if (!inside && (ComputedStyle.OverflowX != OverflowMode.Visible || ComputedStyle.OverflowY != OverflowMode.Visible))
        {
            return null;
        }

        if (FindScrollbarAt(point, needPointerEvents, match) is { } scrollbarHit)
        {
            return scrollbarHit;
        }

        SortRenderChildren();
        for (int i = _renderChildren.Count - 1; i >= 0; i--)
        {
            var child = _renderChildren[i];
            if (!child.IsFixed && child is not ScrollBar && child.FindVisualPanelAt(point, needPointerEvents, match) is { } hit)
            {
                return hit;
            }
        }

        return inside && (!needPointerEvents || ComputedStyle.PointerEvents != PointerEvents.None) && (match is null || match(this)) ? this : null;
    }
}

public partial class RootPanel
{
    private readonly List<Panel> _fixedOverlays = [];

    internal bool FixedOverlaysDirty { get; set; } = true;

    internal List<Panel> FixedOverlays
    {
        get
        {
            if (!FixedOverlaysDirty)
            {
                return _fixedOverlays;
            }

            FixedOverlaysDirty = false;
            _fixedOverlays.Clear();
            Collect(this);
            var sorted = _fixedOverlays.OrderBy(static p => p.ComputedStyle?.ZIndex ?? 0).ToArray();
            _fixedOverlays.Clear();
            _fixedOverlays.AddRange(sorted);
            return _fixedOverlays;

            void Collect(Panel panel)
            {
                if (panel.IsDeleted || panel.ComputedStyle?.Display == DisplayMode.None)
                {
                    return;
                }

                if (panel.IsFixed)
                {
                    _fixedOverlays.Add(panel);
                }

                foreach (var child in panel.Children)
                {
                    Collect(child);
                }
            }
        }
    }

    internal Panel? FindFixedPanelAt(Vector2 point, bool needPointerEvents, Func<Panel, bool>? match = null)
    {
        if (!Bounds.IsInside(point))
        {
            return null;
        }

        var overlays = FixedOverlays;
        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            if (overlays[i].FindVisualPanelAt(point, needPointerEvents, match) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }
}
