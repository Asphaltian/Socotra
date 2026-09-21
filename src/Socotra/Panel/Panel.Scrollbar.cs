namespace Socotra;

public partial class Panel
{
    private ScrollBar? _scrollbarY;
    private ScrollBar? _scrollbarX;

    internal Rect ContentClipRect
    {
        get
        {
            var gutter = LayoutTree.Gutter;
            return gutter.Left == 0 && gutter.Right == 0 ? Box.ClipRect : Box.ClipRect.Shrink(gutter.Left, 0, gutter.Right, 0);
        }
    }

    private Margin ScrollbarGutter
    {
        get
        {
            if (ComputedStyle is not { } style || style.ScrollbarGutter is null or Socotra.ScrollbarGutter.Auto
                || (style.OverflowX != OverflowMode.Scroll && style.OverflowY != OverflowMode.Scroll))
            {
                return default;
            }

            var thickness = ScrollBar.Thickness(style.ScrollbarWidth, ScaleToScreen);
            if (thickness <= 0)
            {
                return default;
            }

            return new Margin(style.ScrollbarGutter == Socotra.ScrollbarGutter.StableBothEdges ? thickness : 0, 0, thickness, 0);
        }
    }

    private int ScrollbarCount
    {
        get
        {
            var count = 0;
            for (int i = ChildrenCount - 1; i >= 0 && _children![i] is ScrollBar; i--)
            {
                count++;
            }

            return count;
        }
    }

    private int LastContentChildIndex => ChildrenCount - 1 - ScrollbarCount;

    private ScrollBar? ScrollbarOverlay(ScrollBar? bar) => bar is { IsDeleted: false } && bar.Parent == this && !bar.IsFixed ? bar : null;

    private void RenderScrollbars(Painter painter)
    {
        var horizontal = ScrollbarOverlay(_scrollbarX);
        var vertical = ScrollbarOverlay(_scrollbarY);
        if (horizontal is null && vertical is null)
        {
            return;
        }

        using (ClipChildren(painter, Box.ClipRect))
        {
            horizontal?.RenderShadow(painter);
            vertical?.RenderShadow(painter);
            horizontal?.Render(painter);
            vertical?.Render(painter);
        }
    }

    private Panel? FindScrollbarAt(Vector2 point, bool needPointerEvents, Func<Panel, bool>? match) =>
        ScrollbarOverlay(_scrollbarY)?.FindVisualPanelAt(point, needPointerEvents, match)
        ?? ScrollbarOverlay(_scrollbarX)?.FindVisualPanelAt(point, needPointerEvents, match);

    private void FinalLayoutScrollbars(Vector2 offset)
    {
        ScrollbarOverlay(_scrollbarX)?.FinalLayout(offset);
        ScrollbarOverlay(_scrollbarY)?.FinalLayout(offset);
    }

    private void UpdateScrollbars()
    {
        if (ComputedStyle is not { } style || this is ScrollBar)
        {
            return;
        }

        var wanted = ScrollBar.Thickness(style.ScrollbarWidth, ScaleToScreen) > 0;
        BuildScrollbar(wanted && HasScrollY, vertical: true, ref _scrollbarY);
        BuildScrollbar(wanted && HasScrollX, vertical: false, ref _scrollbarX);
        if (_scrollbarY is null && _scrollbarX is null)
        {
            return;
        }

        var last = ChildrenCount - 1;
        if (_scrollbarY is { IsDeleted: false })
        {
            SetChildIndex(_scrollbarY, last--);
        }

        if (_scrollbarX is { IsDeleted: false })
        {
            SetChildIndex(_scrollbarX, last);
        }
    }

    private void BuildScrollbar(bool shouldExist, bool vertical, ref ScrollBar? bar)
    {
        if (!shouldExist)
        {
            bar?.Delete();
            bar = null;
            return;
        }

        if (bar is not { IsDeleted: false })
        {
            bar = new ScrollBar(vertical);
            AddChild(bar);
        }
    }
}
