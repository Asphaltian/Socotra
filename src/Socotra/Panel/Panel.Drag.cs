namespace Socotra;

public partial class Panel
{
    /// <summary>Whether this panel wants to be dragged when a mouse press on it or its children moves. By default, scrolling panels do, so they can be scrolled by dragging.</summary>
    public virtual bool WantsDrag => (ScrollSize != Vector2.Zero || BlocksScrollChaining) && WantsDragScrolling;

    /// <summary>Set to false to stop the panel scrolling when it's dragged.</summary>
    public bool CanDragScroll { get; set; } = true;

    /// <summary>Whether the panel is being scrolled by dragging right now.</summary>
    public bool IsDragScrolling { get; private set; }

    /// <summary>Whether the panel can scroll sideways.</summary>
    public bool HasScrollX => ScrollSize.X > 0 && ComputedStyle?.OverflowX == OverflowMode.Scroll;

    /// <summary>Whether the panel can scroll up and down.</summary>
    public bool HasScrollY => ScrollSize.Y > 0 && ComputedStyle?.OverflowY == OverflowMode.Scroll;

    /// <summary>Whether dragging the panel scrolls it: <see cref="CanDragScroll"/> is on and it has <c>overflow: scroll</c>.</summary>
    protected virtual bool WantsDragScrolling =>
        CanDragScroll && (ComputedStyle?.OverflowX == OverflowMode.Scroll || ComputedStyle?.OverflowY == OverflowMode.Scroll);

    /// <summary>Called when a drag of this panel starts. By default it starts drag scrolling.</summary>
    protected virtual void OnDragStart(DragEvent e)
    {
        if (e.Target != this || (ScrollSize == Vector2.Zero && !BlocksScrollChaining) || !WantsDragScrolling)
        {
            return;
        }

        StopScrollVelocity();
        e.StopPropagation();
        IsDragScrolling = true;
    }

    /// <summary>Called when a drag of this panel ends. By default it lets a drag scroll carry on with the mouse's speed.</summary>
    protected virtual void OnDragEnd(DragEvent e)
    {
        IsDragScrolling = false;
        if (e.Target != this || ScrollSize == Vector2.Zero || !WantsDragScrolling)
        {
            return;
        }

        var delta = (FindRootPanel()?.Input.CursorVelocity ?? Vector2.Zero) * -6;
        ScrollVelocity += new Vector2(HasScrollX ? delta.X : 0, HasScrollY ? delta.Y : 0);
        _scrollVelocityVelocity = Vector2.Zero;
        SetNeedsFinalLayout();
        e.StopPropagation();
    }

    /// <summary>Called as a drag of this panel moves. By default it scrolls the panel with the mouse.</summary>
    protected virtual void OnDrag(DragEvent e)
    {
        if (e.Target != this || (ScrollSize == Vector2.Zero && !BlocksScrollChaining) || !WantsDragScrolling)
        {
            return;
        }

        e.StopPropagation();
        var delta = e.LocalGrabPosition - e.LocalPosition;
        ApplyDragScroll(new Vector2(IsScrollContainer(true) ? delta.X : 0, IsScrollContainer(false) ? delta.Y : 0));
    }

    /// <summary>Called when a panel being dragged moves onto this one.</summary>
    protected virtual void OnDragEnter(PanelEvent e)
    {
    }

    /// <summary>Called when a panel being dragged leaves this one, or a drag from outside the app stops hovering it.</summary>
    protected virtual void OnDragLeave(PanelEvent e)
    {
    }

    /// <summary>
    /// Called when something dragged is let go over this panel. Files and text dragged in from outside the app arrive as a
    /// <see cref="DropEvent"/>: while one hovers, set its <see cref="DropEvent.Action"/> to say whether you'd take it.
    /// </summary>
    protected virtual void OnDrop(PanelEvent e)
    {
    }

    internal Panel? FindDragTarget() => WantsDrag ? this : Parent?.FindDragTarget();

    private bool BlocksScrollChaining =>
        (IsScrollContainer(true) && GetOverscrollBehavior(true) != OverscrollBehavior.Auto)
        || (IsScrollContainer(false) && GetOverscrollBehavior(false) != OverscrollBehavior.Auto);

    private void ApplyDragScroll(Vector2 delta)
    {
        var min = IsScrollAxisReversed ? -ScrollSize : Vector2.Zero;
        var max = IsScrollAxisReversed ? Vector2.Zero : ScrollSize;
        var target = ScrollOffset + delta;
        var overshoot = target - Vector2.Clamp(target, min, max);

        if (overshoot.X != 0 && GetOverscrollBehavior(true) == OverscrollBehavior.Auto && FindScrollChainTarget(overshoot.X, true) is { } parentX)
        {
            parentX.StopScrollVelocity();
            parentX.ApplyDragScroll(new Vector2(overshoot.X, 0));
            target.X -= overshoot.X;
        }

        if (overshoot.Y != 0 && GetOverscrollBehavior(false) == OverscrollBehavior.Auto && FindScrollChainTarget(overshoot.Y, false) is { } parentY)
        {
            parentY.StopScrollVelocity();
            parentY.ApplyDragScroll(new Vector2(0, overshoot.Y));
            target.Y -= overshoot.Y;
        }

        if (!HasScrollX || GetOverscrollBehavior(true) == OverscrollBehavior.None)
        {
            target.X = Math.Clamp(target.X, min.X, max.X);
        }

        if (!HasScrollY || GetOverscrollBehavior(false) == OverscrollBehavior.None)
        {
            target.Y = Math.Clamp(target.Y, min.Y, max.Y);
        }

        var beyond = target - Vector2.Clamp(target, min, max);
        if (beyond.LengthSquared() > 1e-8f)
        {
            const float overDrag = 16;
            var stretch = Easing.EaseOut(Math.Clamp(beyond.Length() / (overDrag * 12), 0, 1));
            target += (beyond.Normal * stretch * overDrag) - beyond;
        }

        ScrollOffset = Vector2.Clamp(target, min - ScrollBounceLimit, max + ScrollBounceLimit);
    }
}
