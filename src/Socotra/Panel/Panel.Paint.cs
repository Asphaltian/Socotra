namespace Socotra;

public partial class Panel
{
    internal Matrix4x4 RenderTransform => GlobalMatrixInverted ?? Matrix4x4.Identity;

    internal void Render(Painter painter)
    {
        if (ComputedStyle is null || !IsVisible)
        {
            return;
        }

        using var scope = EnterDestination(painter);
        painter = scope.Painter;

        var layered = HasPanelLayer;
        var layer = layered ? painter.Target(PanelLayerBounds) : default;
        try
        {
            if (!LayoutTree.IsInlineParticipant)
            {
                if (HasBackdropFilter)
                {
                    painter.FilterBackdrop(Box.Rect, _paintCache.Backdrop, _paintCache.OuterRadii);
                }

                if (layered || this is RootPanel)
                {
                    DrawShadows(inset: false, painter);
                }

                DrawBackground(painter);
                DrawContent(painter);
                DrawShadows(inset: true, painter);
                DrawOutline(painter);
            }

            RenderChildren(painter);
        }
        finally
        {
            if (layered)
            {
                var target = layer.Layer;
                layer.Dispose();
                DrawLayer(painter, target);
            }
        }
    }

    internal void RenderShadow(Painter painter)
    {
        if (ComputedStyle is not { BoxShadow.Count: > 0 } || !IsVisible || HasPanelLayer || LayoutTree.IsInlineParticipant)
        {
            return;
        }

        using var scope = EnterDestination(painter);
        DrawShadows(inset: false, scope.Painter);
    }

    internal Painter.DestinationClipScope ClipChildren(Painter painter, Rect clipRect) =>
        _paintCache.ClipsChildren ? painter.ClipDestination(clipRect, _paintCache.InnerRadii, _paintCache.ClipTransform) : default;

    private Painter.DestinationScope EnterDestination(Painter painter) =>
        painter.WithDestination(_paintCache.Bounds, ScaleToScreen, painter.InheritedOpacity * _paintCache.Opacity,
            _paintCache.Blend ?? painter.InheritedBlendMode, RenderTransform);

    private void DrawContent(Painter painter)
    {
        if (!_hasDrawCallback && !LayoutTree.HasInlineContent)
        {
            return;
        }

        using var content = painter.WithContentOrigin(Box.Rect.Position);
        try
        {
            if (_hasDrawCallback)
            {
                OnDraw(content.Painter);
            }

            LayoutTree.DrawInlineContent(content.Painter);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private void RenderChildren(Painter painter)
    {
        if (!HasChildren)
        {
            return;
        }

        SortRenderChildren();
        using (ClipChildren(painter, _paintCache.ContentClipRect))
        {
            foreach (var child in _shadowChildren ?? [])
            {
                if (!child.IsFixed)
                {
                    child.RenderShadow(painter);
                }
            }

            foreach (var child in _renderChildren)
            {
                if (!child.IsFixed && child is not ScrollBar)
                {
                    child.Render(painter);
                }
            }
        }

        RenderScrollbars(painter);
    }

    private void DrawOutline(Painter painter)
    {
        if (_paintCache.OutlineColor.A > 0 && _paintCache.OutlineWidth > 0)
        {
            painter.Outline(Box.Rect, _paintCache.OutlineColor, _paintCache.OutlineWidth, _paintCache.OuterRadii, _paintCache.OutlineOffset);
        }
    }

    private void DrawBackground(Painter painter)
    {
        if (!HasBackground || (!_paintCache.Background.HasFill && !_paintCache.HasBorder))
        {
            return;
        }

        ref readonly var descriptor = ref _paintCache.Background.Descriptor;
        if (!_paintCache.ClipsBackgroundToText)
        {
            painter.Rect(in descriptor);
            return;
        }

        if (_paintCache.HasBorder)
        {
            painter.Rect(descriptor with
            {
                Color = Color.Transparent,
                BackgroundImage = null,
                BackgroundGradient = default,
                BackgroundTint = Color.Transparent,
                BackgroundBlendMode = BlendMode.Normal,
                BackgroundClip = BackgroundClip.BorderBox,
                BackgroundClipInset = default,
            });
        }

        if (!_paintCache.Background.HasFill)
        {
            return;
        }

        var visualRoot = VisualRoot;
        foreach (var label in Descendants.Prepend(this).OfType<Label>())
        {
            if (!label.IsVisible || label.VisualRoot != visualRoot || !label.GetTextMask(out var texture, out var rect))
            {
                continue;
            }

            painter.Rect(descriptor with
            {
                BackgroundClip = BackgroundClip.Text,
                BackgroundClipInset = default,
                TextMask = texture,
                TextMaskRect = new Vector4(rect.Left - descriptor.Rect.Left, rect.Top - descriptor.Rect.Top, rect.Width, rect.Height),
                Stroke = default,
                BorderImage = default,
            });
        }
    }

    private Vector4 GetBackgroundClipInset(BackgroundClip clip)
    {
        if (clip is BackgroundClip.BorderBox or BackgroundClip.Text)
        {
            return Vector4.Zero;
        }

        var inset = clip == BackgroundClip.ContentBox ? Box.Border + Box.Padding : Box.Border;
        return new Vector4(inset.Left, inset.Top, inset.Right, inset.Bottom);
    }

    private void DrawShadows(bool inset, Painter painter)
    {
        var shadows = ComputedStyle!.BoxShadow;
        if (shadows is not { Count: > 0 })
        {
            return;
        }

        var rect = inset ? Box.ClipRect : Box.Rect;
        var radii = inset ? _paintCache.InnerRadii : _paintCache.OuterRadii;
        foreach (var shadow in shadows)
        {
            if (shadow.Inset == inset && shadow.Color.A > 0)
            {
                painter.RectShadow(rect, radii, shadow.Color, shadow.Blur, shadow.Spread, new Vector2(shadow.OffsetX, shadow.OffsetY), inset);
            }
        }
    }
}
