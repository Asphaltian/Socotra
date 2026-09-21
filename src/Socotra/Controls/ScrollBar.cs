namespace Socotra;

/// <summary>
/// The scrollbar a scrolling panel shows along one axis. Panels make their own, so you style them rather than create them:
/// set <c>scrollbar-width</c>, <c>scrollbar-color</c> and <c>scrollbar-gutter</c> on the scrolling panel, or target
/// <c>scrollbar</c> and <c>scrollbar &gt; .thumb</c> in your stylesheet.
/// </summary>
/// <example>
/// <code>
/// .list
/// {
///     overflow-y: scroll;
///     scrollbar-width: thin;
///     scrollbar-color: #62d98b transparent; // thumb, then track
/// }
/// </code>
/// </example>
[StyleSheet.Inline("scrollbar", Styles)]
public sealed class ScrollBar : Panel
{
    /// <summary>How thick <c>scrollbar-width: auto</c> is, in CSS pixels.</summary>
    public const float AutoThickness = 12;

    /// <summary>How thick <c>scrollbar-width: thin</c> is, in CSS pixels.</summary>
    public const float ThinThickness = 8;

    private const float MinThumbLength = 20;
    private const float ThumbInset = 0.15f;

    private const string Styles = """
        scrollbar
        {
            position: absolute;
            pointer-events: all;
            opacity: 0.5;
            transition: opacity 0.15s ease-out;
            cursor: default;
        }

        scrollbar.visible, scrollbar:hover, scrollbar.dragging { opacity: 1; }

        scrollbar > .thumb
        {
            position: absolute;
            background-color: #ffffff80;
            border-radius: 100px;
            opacity: 0.7;
            transition: opacity 0.15s ease-out;
        }

        scrollbar > .thumb:hover, scrollbar.dragging > .thumb { opacity: 1; }
        """;

    private readonly bool _vertical;
    private readonly Panel _thumb;
    private bool? _shown;
    private float _thickness;
    private float _cornerInset;
    private float _thumbPosition;
    private float _thumbLength;
    private Color? _thumbColor;
    private Color? _trackColor;
    private Rect _laidOutClip;
    private bool _pressedThumb;
    private bool _dragging;
    private float _dragStartOffset;

    internal ScrollBar(bool vertical)
    {
        _vertical = vertical;
        ElementName = "scrollbar";
        AddClass(vertical ? "vertical" : "horizontal");
        _thumb = new Panel();
        _thumb.AddClass("thumb");
        AddChild(_thumb);
    }

    /// <summary>Whether it scrolls up and down rather than sideways.</summary>
    public bool IsVertical => _vertical;

    /// <inheritdoc/>
    public override bool WantsDrag => true;

    private Panel? Owner => Parent;

    internal static float Thickness(Length? width, float scale) => width is not { } w
        ? 0
        : MathF.Max(0, MathF.Round(w.Unit == LengthUnit.Auto ? AutoThickness * scale : w.GetPixels(0)));

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        if (Owner is not { ComputedStyle: { } style } owner)
        {
            return;
        }

        var thickness = Thickness(style.ScrollbarWidth, ScaleToScreen);
        var shown = (_vertical ? owner.HasScrollY : owner.HasScrollX) && thickness > 0;
        if (_shown != shown)
        {
            _shown = shown;
            Style.Display = shown ? DisplayMode.Flex : DisplayMode.None;
            SetNeedsPreLayout();
        }

        if (!shown)
        {
            return;
        }

        if (_thickness != thickness)
        {
            _thickness = thickness;
            var thumbInset = Length.Pixels(MathF.Round(thickness * ThumbInset) / ScaleToScreen);
            if (_vertical)
            {
                Style.Width = Length.Pixels(thickness / ScaleToScreen);
                _thumb.Style.Left = thumbInset;
                _thumb.Style.Right = thumbInset;
            }
            else
            {
                Style.Height = Length.Pixels(thickness / ScaleToScreen);
                _thumb.Style.Top = thumbInset;
                _thumb.Style.Bottom = thumbInset;
            }

            SetNeedsPreLayout();
            _thumb.SetNeedsPreLayout();
        }

        if (_thumbColor != style.ScrollbarThumbColor)
        {
            _thumbColor = style.ScrollbarThumbColor;
            _thumb.Style.BackgroundColor = _thumbColor;
            _thumb.SetNeedsPreLayout();
        }

        if (_trackColor != style.ScrollbarTrackColor)
        {
            _trackColor = style.ScrollbarTrackColor;
            Style.BackgroundColor = _trackColor;
            SetNeedsPreLayout();
        }

        var inset = (_vertical ? owner.HasScrollX : owner.HasScrollY) ? thickness : 0;
        if (_cornerInset != inset || _laidOutClip != owner.Box.ClipRect)
        {
            _cornerInset = inset;
            SetNeedsFinalLayout();
        }

        UpdateThumb(owner);
        SetClass("visible", _dragging || owner.IsDragScrolling || owner.ScrollVelocity != Vector2.Zero);
        SetClass("dragging", _dragging);
    }

    /// <inheritdoc/>
    protected override void OnLayout(ref Rect rect)
    {
        if (Owner is not { } owner)
        {
            return;
        }

        var clip = owner.Box.ClipRect;
        _laidOutClip = clip;
        rect = _vertical
            ? new Rect(clip.Right - _thickness, clip.Top, _thickness, clip.Height - _cornerInset)
            : new Rect(clip.Left, clip.Bottom - _thickness, clip.Width - _cornerInset, _thickness);
    }

    /// <inheritdoc/>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        if (e.MouseButton != MouseButtons.Left)
        {
            return;
        }

        _pressedThumb = e.Target == _thumb;
        if (_pressedThumb || Owner is not { } owner)
        {
            return;
        }

        var page = Axis(owner.Box.Rect.Size) * 0.9f;
        ScrollBy(owner, Axis(MousePosition) > _thumbPosition ? page : -page);
        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnDragStart(DragEvent e)
    {
        if (!_pressedThumb || Owner is not { } owner)
        {
            return;
        }

        _dragging = true;
        _dragStartOffset = Axis(owner.ScrollOffset);
        owner.ScrollTo(owner.ScrollOffset);
        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnDrag(DragEvent e)
    {
        if (!_dragging || Owner is not { } owner)
        {
            return;
        }

        var free = Axis(Box.Rect.Size) - _thumbLength;
        if (free <= 0)
        {
            return;
        }

        var target = _dragStartOffset + (Axis(MousePosition - e.LocalGrabPosition) * Axis(owner.ScrollSize) / free);
        var offset = owner.ScrollOffset;
        owner.ScrollTo(_vertical ? offset with { Y = target } : offset with { X = target });
        e.StopPropagation();
    }

    /// <inheritdoc/>
    protected override void OnDragEnd(DragEvent e)
    {
        _dragging = false;
        _pressedThumb = false;
    }

    internal static bool Owns(Panel? panel) => panel is ScrollBar || panel?.Parent is ScrollBar;

    private float Axis(Vector2 v) => _vertical ? v.Y : v.X;

    private void UpdateThumb(Panel owner)
    {
        var track = Axis(Box.Rect.Size);
        var viewport = Axis(owner.Box.Rect.Size);
        var range = Axis(owner.ScrollSize);
        if (track <= 0 || viewport <= 0 || range <= 0)
        {
            return;
        }

        var length = MathF.Min(track, MathF.Max(MinThumbLength * ScaleToScreen, track * viewport / (viewport + range)));
        var offset = Axis(owner.ScrollOffset) + (owner.IsScrollAxisReversed ? range : 0);
        var position = (track - length) * Math.Clamp(offset / range, 0, 1);
        if (_thumbLength == length && _thumbPosition == position)
        {
            return;
        }

        _thumbLength = length;
        _thumbPosition = position;
        var start = Length.Pixels(position / ScaleToScreen);
        var size = Length.Pixels(length / ScaleToScreen);
        if (_vertical)
        {
            _thumb.Style.Top = start;
            _thumb.Style.Height = size;
        }
        else
        {
            _thumb.Style.Left = start;
            _thumb.Style.Width = size;
        }

        _thumb.SetNeedsPreLayout();
    }

    private void ScrollBy(Panel owner, float delta)
    {
        var offset = owner.ScrollOffset;
        owner.ScrollTo(_vertical ? offset with { Y = offset.Y + delta } : offset with { X = offset.X + delta });
    }
}
