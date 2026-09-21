namespace Socotra;

internal sealed partial class PainterBatcher
{
    private readonly List<Rect> _backdropWrites = [];

    public void AddBackdrop(in Painter.BackdropData data, Matrix4x4 localTransform, int localClip, BlendMode blendMode)
    {
        Flush();
        var target = Destination;
        var transform = localTransform * target.Transform;
        var scissor = GetOrAddDrawClip(localClip, target.Transform, GetOrAddScissor(target.Scissor));
        AddBackdrop(data, new QuadPlacement(data.Rect.Grow(1), transform, target.LayerMatrix, scissor, blendMode), reuseGrab: false);
    }

    public void AddBackdrop(in Painter.BackdropData data, BlendMode blendMode)
    {
        var reuseGrab = PrepareBackdrop(data);
        var target = Destination;
        var placement = new QuadPlacement(data.Rect.Grow(1), target.Transform, target.LayerMatrix, GetOrAddScissor(target.Scissor), blendMode);
        AddBackdrop(data, placement, reuseGrab);
    }

    private static bool TryScreenBounds(Rect rect, Matrix4x4 transform, out Rect bounds)
    {
        bounds = default;
        if (transform.M14 != 0 || transform.M24 != 0 || transform.M44 != 1)
        {
            return false;
        }

        var screen = transform.Transform(rect);
        if (!float.IsFinite(screen.Left) || !float.IsFinite(screen.Top) || !float.IsFinite(screen.Right) || !float.IsFinite(screen.Bottom))
        {
            return false;
        }

        bounds = screen;
        return true;
    }

    private void AddBackdrop(in Painter.BackdropData data, QuadPlacement placement, bool reuseGrab)
    {
        var radii = data.Radii.Clamped(data.Rect.Width, data.Rect.Height);
        var tint = data.Filter.Tint.WithAlphaMultiplied(data.Opacity);
        list.Commands.Add(new BackdropCommand(placement, data.Rect, radii, data.Filter, tint, reuseGrab));
    }

    private void InvalidateBackdrop() => _backdropWrites.Clear();

    private bool PrepareBackdrop(in Painter.BackdropData data)
    {
        var viewport = Destination.Viewport;
        if (viewport.Position != Vector2.Zero || viewport.Width <= 0 || viewport.Height <= 0
            || Destination.Layered
            || Destination.LayerMatrix != Matrix4x4.Identity
            || !TryScreenBounds(data.Rect.Grow(1), Destination.Transform, out var bounds))
        {
            Flush();
            return false;
        }

        bool reuse = _backdropWrites.Count > 0;
        foreach (var write in _backdropWrites)
        {
            if (bounds.Overlaps(write))
            {
                reuse = false;
                break;
            }
        }

        if (!reuse)
        {
            Flush();
        }

        _backdropWrites.Add(bounds);
        return reuse;
    }

    private void TrackBackdropWrite(in BoxInstance instance)
    {
        if (_backdropWrites.Count == 0)
        {
            return;
        }

        var rect = new Rect(instance.Rect.X, instance.Rect.Y, instance.Rect.Z, instance.Rect.W).Grow(1);
        if (!TryScreenBounds(rect, Transforms[instance.TransformIndex], out var bounds))
        {
            InvalidateBackdrop();
            return;
        }

        _backdropWrites.Add(bounds);
    }
}
