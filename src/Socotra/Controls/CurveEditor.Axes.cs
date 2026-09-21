namespace Socotra;

public partial class CurveEditor
{
    private Rect? _navigationBounds;

    /// <summary>The smallest time and value in view, in the curve's own units. Panning and zooming never change the curves.</summary>
    public Vector2 ViewMin => ViewUnits(_canvas.ViewMin);

    /// <summary>The largest time and value in view, in the curve's own units.</summary>
    public Vector2 ViewMax => ViewUnits(_canvas.ViewMax);

    /// <summary>
    /// How far the user can pan and zoom, in the curve's own units, or null for no limit. Only the time limits apply; the
    /// view can move freely up and down. The limits follow the curve when its ranges change.
    /// </summary>
    public Rect? NavigationBounds
    {
        get => _navigationBounds is { } rect ? new Rect(ViewUnits(rect.Position), ViewUnits(rect.Position + rect.Size) - ViewUnits(rect.Position)) : null;
        set
        {
            Rect? normalized = null;
            if (value is { } rect)
            {
                var (origin, span) = Units();
                normalized = new Rect((rect.Position - origin) / span, rect.Size / span);
            }

            _canvas.NavigationBounds = normalized;
            _navigationBounds = normalized;
        }
    }

    /// <summary>Shows times and values from <paramref name="min"/> to <paramref name="max"/>, in the curve's own units.</summary>
    public void SetViewBounds(Vector2 min, Vector2 max)
    {
        var (origin, span) = Units();
        _canvas.SetView((min - origin) / span, (max - origin) / span);
    }

    private static (Vector2 Origin, Vector2 Span) UnitsOf(Curve curve) =>
        (new Vector2(curve.TimeRange.X, curve.ValueRange.X), new Vector2(curve.TimeRange.Y - curve.TimeRange.X, curve.ValueRange.Y - curve.ValueRange.X));

    private static Vector2 Remap(Curve from, Curve to, Vector2 point)
    {
        var (fromOrigin, fromSpan) = UnitsOf(from);
        var (toOrigin, toSpan) = UnitsOf(to);
        return (fromOrigin + (point * fromSpan) - toOrigin) / toSpan;
    }

    private (Vector2 Origin, Vector2 Span) Units() => UnitsOf(_curves[0]);

    private Vector2 ViewUnits(Vector2 point)
    {
        var (origin, span) = Units();
        return origin + (point * span);
    }

    private void EditAxisRange(float value, bool horizontal, bool maximum)
    {
        var reference = horizontal ? _curves[0].TimeRange : _curves[0].ValueRange;
        float delta = value - (maximum ? reference.Y : reference.X);
        if (!float.IsFinite(delta))
        {
            return;
        }

        var curves = (Curve[])_curves.Clone();
        for (int i = 0; i < curves.Length; i++)
        {
            var range = horizontal ? curves[i].TimeRange : curves[i].ValueRange;
            if (maximum)
            {
                range.Y += delta;
            }
            else
            {
                range.X += delta;
            }

            if (!Painter.IsFinite(range) || !float.IsFinite(range.Y - range.X) || range.Y <= range.X)
            {
                return;
            }

            if (horizontal)
            {
                curves[i].UpdateTimeRange(range, false);
            }
            else
            {
                curves[i].UpdateValueRange(range, false);
            }
        }

        var min = ViewMin;
        var max = ViewMax;
        if (horizontal && maximum)
        {
            max.X += delta;
        }
        else if (horizontal)
        {
            min.X += delta;
        }
        else if (maximum)
        {
            max.Y += delta;
        }
        else
        {
            min.Y += delta;
        }

        Change(curves);
        if (Painter.IsFinite(min) && Painter.IsFinite(max) && min.X < max.X && min.Y < max.Y)
        {
            SetViewBounds(min, max);
        }
        else
        {
            _canvas.SetView(Vector2.Zero, Vector2.One);
        }
    }

    private sealed partial class CurveCanvas
    {
        protected override Vector2 AxisMinimum => new(_editor._curves[0].TimeRange.X, _editor._curves[0].ValueRange.X);

        protected override Vector2 AxisMaximum => new(_editor._curves[0].TimeRange.Y, _editor._curves[0].ValueRange.Y);

        protected override Color AxisColor => _editor.ComputedStyle?.FontColor ?? Color.White;

        public override Vector2 CanvasToAxis(Vector2 point) => _editor.ViewUnits(point);

        public override Vector2 AxisToCanvas(Vector2 point) => (point - AxisMinimum) / (AxisMaximum - AxisMinimum);
    }
}
