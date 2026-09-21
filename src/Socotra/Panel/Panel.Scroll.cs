namespace Socotra;

public partial class Panel
{
    private const float WheelSpeed = 20;
    private const float ScrollSmoothTime = 0.5f;

    private Vector2 _scrollVelocityVelocity;
    private Vector2 _laidOutScrollOffset;

    /// <summary>How fast the panel is scrolling on its own, like after a flick or a turn of the mouse wheel.</summary>
    public Vector2 ScrollVelocity { get; set; }

    /// <summary>
    /// Keeps the panel scrolled to the bottom as its content grows, like a chat log. Scrolling up lets go of the bottom,
    /// and scrolling back down catches it again.
    /// </summary>
    public bool PreferScrollToBottom { get; set; }

    /// <summary>Whether the panel is scrolled all the way to the bottom and staying there. See <see cref="PreferScrollToBottom"/>.</summary>
    public bool IsScrollAtBottom { get; private set; }

    internal bool IsScrollAxisReversed => ComputedStyle is { } style
        && (style.JustifyContent == Justify.FlexEnd || style.FlexDirection is FlexDirection.RowReverse or FlexDirection.ColumnReverse);

    private Vector2 ScrollBounceLimit => Vector2.Min(Box.Rect.Size * 0.2f, new Vector2(150) * ScaleToScreen);

    /// <summary>
    /// Called when the mouse wheel turns over this panel. By default it scrolls the panel if it can, and passes what's left
    /// to the parent.
    /// </summary>
    /// <param name="value">How far the wheel turned, in notches. Positive values scroll down and right.</param>
    public virtual void OnMouseWheel(Vector2 value)
    {
        TryScroll(value, out var remaining);
        if (remaining != Vector2.Zero)
        {
            Parent?.OnMouseWheel(remaining);
        }
    }

    /// <summary>Scrolls the panel as if the mouse wheel turned by <paramref name="value"/> notches. Returns false if it can't scroll that way.</summary>
    public bool TryScroll(Vector2 value) => TryScroll(value, out _);

    /// <summary>Jumps straight to the bottom. Returns false if the panel can't scroll up and down.</summary>
    public bool TryScrollToBottom()
    {
        if (ComputedStyle is null || !HasScrollY)
        {
            return false;
        }

        ScrollOffset = ScrollOffset with { Y = ScrollSize.Y };
        IsScrollAtBottom = true;
        StopScrollVelocity();
        return true;
    }

    /// <summary>Jumps to <paramref name="offset"/>, kept within how far the panel can scroll.</summary>
    public void ScrollTo(Vector2 offset)
    {
        var min = IsScrollAxisReversed ? -ScrollSize : Vector2.Zero;
        var max = IsScrollAxisReversed ? Vector2.Zero : ScrollSize;
        offset = Vector2.Clamp(offset, min, max);
        StopScrollVelocity();
        IsScrollAtBottom = offset.Y >= ScrollSize.Y;
        if (ScrollOffset != offset)
        {
            ScrollOffset = offset;
        }
    }

    /// <summary>Scrolls the panel the least amount that brings <paramref name="rect"/>, on screen, into view. Returns false if it didn't need to.</summary>
    public bool ScrollIntoView(Rect rect)
    {
        if (ComputedStyle is not { } style || (style.OverflowX != OverflowMode.Scroll && style.OverflowY != OverflowMode.Scroll))
        {
            return false;
        }

        var view = Box.RectInner;
        var clip = ContentClipRect;
        view.Left = MathF.Max(view.Left, clip.Left);
        view.Right = MathF.Min(view.Right, clip.Right);
        var offset = ScrollOffset;
        rect -= offset - _laidOutScrollOffset;

        if (style.OverflowY == OverflowMode.Scroll)
        {
            if (rect.Bottom > view.Bottom)
            {
                offset.Y += MathF.Ceiling(rect.Bottom - view.Bottom);
            }

            if (rect.Top < view.Top)
            {
                offset.Y -= MathF.Ceiling(view.Top - rect.Top);
            }
        }

        if (style.OverflowX == OverflowMode.Scroll)
        {
            if (rect.Right > view.Right)
            {
                offset.X += MathF.Ceiling(rect.Right - view.Right);
            }

            if (rect.Left < view.Left)
            {
                offset.X -= MathF.Ceiling(view.Left - rect.Left);
            }
        }

        if (offset == ScrollOffset)
        {
            return false;
        }

        StopScrollVelocity();
        ScrollOffset = offset;
        return true;
    }

    /// <summary>
    /// Tells the panel how big its scrollable content is, in screen pixels, including the part that's in view. Call it from
    /// <see cref="FinalLayoutChildren"/> when you place content yourself.
    /// </summary>
    protected virtual void ConstrainScrolling(Vector2 size)
    {
        if (IsDragScrolling)
        {
            return;
        }

        size -= Box.Rect.Size;
        var heightChange = size.Y - ScrollSize.Y;
        ScrollSize = new Vector2(MathF.Round(size.X), MathF.Round(size.Y));

        var style = ComputedStyle!;
        if (style.OverflowX != OverflowMode.Scroll && style.OverflowY != OverflowMode.Scroll)
        {
            ScrollOffset = Vector2.Zero;
            StopScrollVelocity();
            return;
        }

        var deltaTime = TimeDelta;
        var offset = ScrollOffset + (ScrollVelocity * deltaTime * 60);
        IsScrollAtBottom = offset.Y + ScrollVelocity.Y >= size.Y;
        if (ScrollVelocity.Y > 0 && IsScrollAtBottom)
        {
            offset.Y += heightChange;
        }

        var min = IsScrollAxisReversed ? -ScrollSize : Vector2.Zero;
        var max = IsScrollAxisReversed ? Vector2.Zero : ScrollSize;
        var settle = Math.Clamp(deltaTime * 100, 0, 1);
        offset = Vector2.Lerp(offset, Vector2.Clamp(offset, min, max), settle);

        var limit = ScrollBounceLimit;
        if (style.OverscrollBehaviorX == OverscrollBehavior.None)
        {
            limit.X = 0;
        }

        if (style.OverscrollBehaviorY == OverscrollBehavior.None)
        {
            limit.Y = 0;
        }

        var constrained = Vector2.Clamp(offset, min - limit, max + limit);
        var velocity = ScrollVelocity;
        if ((constrained.X <= min.X - limit.X && velocity.X < 0) || (constrained.X >= max.X + limit.X && velocity.X > 0))
        {
            velocity.X = 0;
            _scrollVelocityVelocity.X = 0;
        }

        if ((constrained.Y <= min.Y - limit.Y && velocity.Y < 0) || (constrained.Y >= max.Y + limit.Y && velocity.Y > 0))
        {
            velocity.Y = 0;
            _scrollVelocityVelocity.Y = 0;
        }

        ScrollVelocity = velocity;
        if (ScrollOffset != constrained)
        {
            ScrollOffset = constrained;
        }
    }

    internal void StopScrollVelocity()
    {
        ScrollVelocity = Vector2.Zero;
        _scrollVelocityVelocity = Vector2.Zero;
    }

    internal void ScrollAncestorsIntoView()
    {
        foreach (var ancestor in Ancestors)
        {
            ancestor.ScrollIntoView(Box.Rect);
        }
    }

    private void AddScrollVelocity()
    {
        if (ScrollVelocity.LengthSquared() <= 1e-8f)
        {
            ScrollVelocity = Vector2.Zero;
            return;
        }

        var deltaTime = TimeDelta;
        var velocity = SmoothDamp(ScrollVelocity, ref _scrollVelocityVelocity, ScrollSmoothTime, deltaTime);
        ScrollVelocity = new Vector2(MathF.Abs(velocity.X) < 0.01f ? 0 : velocity.X, MathF.Abs(velocity.Y) < 0.01f ? 0 : velocity.Y);
        SetNeedsFinalLayout();
    }

    private void UpdateScrollPin()
    {
        if (!PreferScrollToBottom || IsScrollAtBottom || MathF.Abs(ScrollVelocity.Y) > 0.1f)
        {
            return;
        }

        ScrollOffset = ScrollOffset with { Y = ScrollSize.Y };
        IsScrollAtBottom = true;
        ScrollVelocity = ScrollVelocity with { Y = 0 };
        _scrollVelocityVelocity.Y = 0;
    }

    private bool TryScroll(Vector2 value, out Vector2 remaining)
    {
        remaining = value;
        if (ComputedStyle is null)
        {
            return false;
        }

        var add = Vector2.Zero;
        if (ConsumeWheelAxis(value.X, horizontal: true))
        {
            remaining.X = 0;
            add.X = HasScrollX ? value.X * WheelSpeed : 0;
        }

        if (ConsumeWheelAxis(value.Y, horizontal: false))
        {
            remaining.Y = 0;
            add.Y = HasScrollY ? value.Y * WheelSpeed : 0;
        }

        add *= 1 + (ScrollVelocity.Length() / 100);
        var velocity = ScrollVelocity + add;
        var maxVelocity = 1000000 * ScaleToScreen;
        if (add.X != 0)
        {
            velocity.X = Math.Clamp(velocity.X, -maxVelocity, maxVelocity);
        }

        if (add.Y != 0)
        {
            velocity.Y = Math.Clamp(velocity.Y, -maxVelocity, maxVelocity);
        }

        ScrollVelocity = velocity;
        if (add != Vector2.Zero)
        {
            SetNeedsFinalLayout();
        }

        return remaining != value;
    }

    private bool ConsumeWheelAxis(float direction, bool horizontal)
    {
        if (direction == 0 || !IsScrollContainer(horizontal))
        {
            return false;
        }

        if (GetOverscrollBehavior(horizontal) != OverscrollBehavior.Auto || CanScrollInDirection(direction, horizontal))
        {
            return true;
        }

        return FindScrollChainTarget(direction, horizontal) is null && (horizontal ? HasScrollX : HasScrollY);
    }

    private bool IsScrollContainer(bool horizontal) => (horizontal ? ComputedStyle?.OverflowX : ComputedStyle?.OverflowY) == OverflowMode.Scroll;

    private OverscrollBehavior GetOverscrollBehavior(bool horizontal) =>
        (horizontal ? ComputedStyle?.OverscrollBehaviorX : ComputedStyle?.OverscrollBehaviorY) ?? OverscrollBehavior.Auto;

    private bool CanScrollInDirection(float direction, bool horizontal)
    {
        var extent = horizontal ? ScrollSize.X : ScrollSize.Y;
        if (extent <= 0)
        {
            return false;
        }

        var offset = horizontal ? ScrollOffset.X : ScrollOffset.Y;
        var min = IsScrollAxisReversed ? -extent : 0;
        var max = IsScrollAxisReversed ? 0 : extent;
        return direction < 0 ? offset > min : offset < max;
    }

    private Panel? FindScrollChainTarget(float direction, bool horizontal)
    {
        for (var panel = Parent; panel is not null; panel = panel.Parent)
        {
            if (panel.IsScrollContainer(horizontal)
                && (panel.GetOverscrollBehavior(horizontal) != OverscrollBehavior.Auto || panel.CanScrollInDirection(direction, horizontal)))
            {
                return panel;
            }
        }

        return null;
    }

    private static Vector2 SmoothDamp(Vector2 current, ref Vector2 velocity, float smoothTime, float deltaTime)
    {
        if (deltaTime <= 0)
        {
            return current;
        }

        var omega = MathF.Tau / smoothTime;
        var denominator = 1 + (omega * deltaTime);
        velocity = (velocity - (omega * omega * deltaTime * current)) / (denominator * denominator);
        return current + (velocity * deltaTime);
    }
}
