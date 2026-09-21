namespace Socotra;

public partial class Panel
{
    private bool _needsPreLayout = true;
    private bool _laidOut;
    private bool _needsFinalLayout = true;
    private int _finalLayoutHash;
    private Vector2 _scrollOffset;

    internal PanelLayout LayoutTree { get; }

    internal bool NeedsPreLayout => _needsPreLayout;

    /// <summary>Where the panel is on screen, in pixels.</summary>
    public Box Box { get; } = new();

    /// <summary>The panel's opacity, multiplied by its ancestors'.</summary>
    public float Opacity { get; private set; } = 1;

    /// <summary>How many screen pixels one CSS pixel is, from the root panel's scale.</summary>
    public float ScaleToScreen { get; private set; } = 1;

    /// <summary>How many CSS pixels one screen pixel is: one over <see cref="ScaleToScreen"/>.</summary>
    public float ScaleFromScreen => 1 / ScaleToScreen;

    /// <summary>
    /// This panel's own CSS <c>transform</c>, as a matrix that takes a point from its parent to where it lands on the
    /// untransformed panel. Null when it has no transform.
    /// </summary>
    public Matrix4x4? LocalMatrix { get; internal set; }

    /// <summary>
    /// The CSS transforms of this panel and its ancestors combined, as a matrix that takes a point on screen to where it
    /// lands on the untransformed panel. Null when none of them have a transform. <see cref="ScreenPositionToPanelPosition"/>
    /// does this for you.
    /// </summary>
    public Matrix4x4? GlobalMatrix { get; private set; }

    internal Matrix4x4? GlobalMatrixInverted { get; private set; }

    internal Matrix4x4 TransformMatrix { get; set; } = Matrix4x4.Identity;

    /// <summary>How far the panel's content is scrolled.</summary>
    public Vector2 ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            if (_scrollOffset == value)
            {
                return;
            }

            _scrollOffset = value;
            SetNeedsFinalLayout();
        }
    }

    /// <summary>How far the content can scroll, beyond what fits.</summary>
    public Vector2 ScrollSize { get; private set; }

    /// <summary>Called when the panel has been placed. Change <paramref name="rect"/> to move or resize it, for example to keep a popup on screen.</summary>
    protected virtual void OnLayout(ref Rect rect)
    {
    }

    internal void SetNeedsPreLayout()
    {
        if (_needsPreLayout)
        {
            return;
        }

        _needsPreLayout = true;
        _needsFinalLayout = true;
        Parent?.SetNeedsPreLayout();
    }

    internal void SetNeedsFinalLayout()
    {
        if (_needsFinalLayout)
        {
            return;
        }

        _needsFinalLayout = true;
        Parent?.SetNeedsFinalLayout();
    }

    internal virtual void PreLayout(LayoutCascade cascade)
    {
        if (!_needsPreLayout && !cascade.SelectorChanged && !cascade.ParentChanged && !LayoutTree.ReferenceSizeChanged)
        {
            return;
        }

        _needsPreLayout = false;
        if (_indexesDirty)
        {
            UpdateChildrenIndexes();
        }

        if (StyleParent is { } styleParent && styleParent != Parent)
        {
            cascade.ParentStyles = styleParent.ComputedStyle;
        }

        ComputedStyle = Style.BuildFinal(ref cascade, out bool changed);
        if (IsFixed)
        {
            cascade.ClipBackgroundToText = false;
        }

        cascade.ParentStyles = ComputedStyle;
        ScaleToScreen = cascade.Scale;
        ResolveFontSize(cascade.Scale);
        var referenceChanged = LayoutTree.ReferenceSizeChanged;
        if (changed || referenceChanged || (cascade.ParentChanged && _paintCache.InheritedStylesChanged(this)))
        {
            _paintCache.Invalidate(this);
        }

        if (changed)
        {
            Parent?.DirtyRenderChildren();
        }
        Opacity = ComputedStyle.Opacity!.Value * (Parent?.Opacity ?? 1);
        UpdateVisibility();
        LayoutTree.Gutter = ScrollbarGutter;

        var styleChanged = changed || cascade.SelectorChanged || cascade.ParentChanged;
        if (styleChanged || referenceChanged || !LayoutTree.Initialized)
        {
            LayoutTree.Apply(ComputedStyle);
        }

        if (styleChanged)
        {
            OnStyleChanged();
        }

        UpdateOrder();
        if ((_laidOut && !IsVisibleSelf) || _children is not { Count: > 0 })
        {
            LayoutTree.PrepareInlineContent();
            return;
        }

        cascade.ParentChanged |= changed;
        cascade.ClipBackgroundToText |= ComputedStyle.BackgroundClip == BackgroundClip.Text;
        foreach (var child in _children)
        {
            child.PreLayout(cascade);
        }

        SortChildrenOrder();
        LayoutTree.PrepareInlineContent();
    }

    private void ResolveFontSize(float scale)
    {
        var parentFontSize = Parent?.ComputedStyle?.FontSize?.GetPixels(0) ?? Length.InitialFontSize * scale;
        Length.CurrentFontSize = parentFontSize;
        var fontSize = ComputedStyle!.FontSize!.Value.GetPixels(parentFontSize);
        ComputedStyle.FontSize = Length.Pixels(fontSize);
        Length.CurrentFontSize = fontSize;
        (this as RootPanel)?.PushRootValues();
    }

    /// <summary>Called when the panel's styles change, like when a class is added or it's hovered.</summary>
    protected virtual void OnStyleChanged()
    {
    }

    internal virtual void FinalLayout(Vector2 offset)
    {
        if (ComputedStyle is null)
        {
            return;
        }

        if (IsFixed && FindRootPanel() is { } root)
        {
            offset = root.Bounds.Position;
        }

        if (LayoutTree.ReferenceSizeChanged)
        {
            SetNeedsPreLayout();
        }

        Length.CurrentFontSize = ComputedStyle.FontSize!.Value.GetPixels(0);

        var hash = HashCode.Combine(offset, ScrollOffset, ScrollVelocity, ComputedStyle.Transform, Opacity, ComputedStyle.Display);
        if (hash == _finalLayoutHash && !_needsFinalLayout && !LayoutTree.HasNewLayout)
        {
            if (_paintCache.NeedsUpdate(this))
            {
                _paintCache.Update(this);
                if (ComputedStyle.Display != DisplayMode.None)
                {
                    FinalLayoutChildren(Box.Rect.Position - _laidOutScrollOffset);
                    FinalLayoutScrollbars(Box.Rect.Position - _laidOutScrollOffset);
                }
            }

            return;
        }

        _needsFinalLayout = false;
        _finalLayoutHash = hash;
        LayoutTree.ClearNewLayout();

        Box.Margin = LayoutTree.Margin;
        Box.Padding = LayoutTree.Padding;
        var border = LayoutTree.Border;
        var gutter = LayoutTree.Gutter;
        Box.Border = new Margin(border.Left - gutter.Left, border.Top, border.Right - gutter.Right, border.Bottom);
        var rect = LayoutTree.Rect + offset;
        OnLayout(ref rect);
        Box.Rect = rect.Floor();
        Box.RectOuter = rect.Grow(Box.Margin).Floor();
        Box.RectInner = rect.Shrink(Box.Padding).Floor();
        Box.ClipRect = rect.Shrink(Box.Border).Floor();
        _paintCache.Update(this);

        if (HasIntro)
        {
            Switch(PseudoClass.Intro, false);
        }

        if (ComputedStyle.Display == DisplayMode.None)
        {
            return;
        }

        if (!_laidOut && PreferScrollToBottom)
        {
            IsScrollAtBottom = true;
        }

        var wasScrollAtBottom = IsScrollAtBottom;
        _laidOutScrollOffset = new Vector2(MathF.Round(ScrollOffset.X), MathF.Round(ScrollOffset.Y));
        FinalLayoutChildren(Box.Rect.Position - _laidOutScrollOffset);
        FinalLayoutScrollbars(Box.Rect.Position - _laidOutScrollOffset);
        LayoutTree.FinalizeInlineContent();
        if (wasScrollAtBottom)
        {
            UpdateScrollPin();
        }

        _laidOut = true;
    }

    /// <summary>
    /// Places the panel's children. Override it to place content yourself, like the rows of a long list, and call
    /// <see cref="ConstrainScrolling"/> with the size of everything there is to scroll through.
    /// </summary>
    /// <param name="offset">Where the content's top left corner is on screen, including the scroll offset.</param>
    protected virtual void FinalLayoutChildren(Vector2 offset)
    {
        if (_children is null)
        {
            return;
        }

        foreach (var child in _children)
        {
            if (!child.IsFixed && child is not ScrollBar)
            {
                child.FinalLayout(offset);
            }
        }

        if (ComputedStyle!.OverflowX != OverflowMode.Scroll && ComputedStyle.OverflowY != OverflowMode.Scroll)
        {
            ScrollOffset = Vector2.Zero;
            return;
        }

        var rect = Box.Rect - ScrollOffset;
        Rect? content = null;
        foreach (var child in _children)
        {
            if (child is not ScrollBar && child.TryGetLayoutRect(out var childRect))
            {
                content = content is { } found ? Union(found, childRect) : childRect;
            }
        }

        if (content is { } extent)
        {
            extent.Right += Box.Padding.Right + LayoutTree.Gutter.Right;
            extent.Bottom += Box.Padding.Bottom;
            rect = Union(rect, extent);
        }

        ConstrainScrolling(rect.Size);
    }

    private static Rect Union(Rect a, Rect b)
    {
        a.Add(b);
        return a;
    }

    private bool TryGetLayoutRect(out Rect rect)
    {
        rect = default;
        if (!IsVisible || IsFixed)
        {
            return false;
        }

        if (ComputedStyle?.Display != DisplayMode.Contents)
        {
            rect = Box.RectOuter;
            return true;
        }

        var found = false;
        foreach (var child in Children)
        {
            if (child.TryGetLayoutRect(out var childRect))
            {
                rect = found ? Union(rect, childRect) : childRect;
                found = true;
            }
        }

        return found;
    }

    internal void SetGlobalMatrix(Matrix4x4? matrix, Matrix4x4? inverted)
    {
        GlobalMatrix = matrix;
        GlobalMatrixInverted = inverted;
    }
}

/// <summary>Where a panel is on screen, in screen pixels. Read it in <see cref="Panel.OnLayout"/> or after an update.</summary>
public sealed class Box
{
    /// <summary>The panel's edges: its border box, including padding and border but not margin.</summary>
    public Rect Rect { get; internal set; }

    /// <summary>The panel including its margin.</summary>
    public Rect RectOuter { get; internal set; }

    /// <summary>Where content goes: inside the border and padding.</summary>
    public Rect RectInner { get; internal set; }

    /// <summary>Inside the border. Children are clipped to this when <c>overflow</c> hides them.</summary>
    public Rect ClipRect { get; internal set; }

    /// <summary>The margin widths.</summary>
    public Margin Margin { get; internal set; }

    /// <summary>The padding widths.</summary>
    public Margin Padding { get; internal set; }

    /// <summary>The border widths.</summary>
    public Margin Border { get; internal set; }
}
