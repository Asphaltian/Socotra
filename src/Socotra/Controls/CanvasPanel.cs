namespace Socotra;

/// <summary>
/// A surface the user can pan with the middle mouse button and zoom with the wheel, around the mouse. Draw on it in
/// <see cref="Panel.OnDraw"/> with <see cref="CanvasToScreen"/>, or put ordinary panels in <see cref="Content"/> and
/// they move and scale with the view. Canvas coordinates are yours to choose: set what's in view with <see cref="SetView"/>.
/// </summary>
/// <example>
/// <code>
/// // A node graph area showing canvas units 0 to 1000 across, with boxes placed in canvas coordinates
/// var canvas = new CanvasPanel { Parent = editor };
/// canvas.SetView(Vector2.Zero, new Vector2(1000, 600));
/// var node = canvas.Content.Add.Panel("node");
/// node.Style.Set("position: absolute; left: 100px; top: 50px; width: 120px; height: 60px;");
/// </code>
/// </example>
[StyleSheet.Inline("canvaspanel", ".canvaspanel { position: relative; overflow: hidden; pointer-events: all; } .canvas-selection-box { position: absolute; pointer-events: none; z-index: 100; border: 1px solid white; background-color: rgba(255,255,255,0.08); border-radius: 0; }")]
public partial class CanvasPanel : Panel
{
    private bool _checkFocus;
    private Panel? _content;
    private Panel? _contentViewport;
    private (Vector2 Min, Vector2 Max, Rect Viewport, float DpiScale, bool YAxisUp, bool PreserveAspectRatio)? _lastContentView;
    private Vector2 _panMouse;
    private Vector2? _zoomSpan;

    /// <summary>Makes a canvas showing 0 to 1 on both axes.</summary>
    public CanvasPanel()
    {
        AddClass("canvaspanel");
        AcceptsFocus = true;
        CanDragScroll = false;
    }

    /// <summary>The smallest canvas coordinates in view.</summary>
    public Vector2 ViewMin { get; private set; } = Vector2.Zero;

    /// <summary>The largest canvas coordinates in view.</summary>
    public Vector2 ViewMax { get; private set; } = Vector2.One;

    /// <summary>Makes canvas Y go up the screen, like a graph, instead of down.</summary>
    public bool YAxisUp { get; set; }

    /// <summary>Keeps the canvas from stretching: the view is scaled evenly and shows extra space around it to fill the panel. Turn it off for graphs whose axes scale separately. On by default.</summary>
    public bool PreserveAspectRatio { get; set; } = true;

    /// <summary>How far in the user can zoom: the smallest span of canvas units the view can show.</summary>
    public float MinimumViewSpan { get; set; } = 0.000001f;

    /// <summary>How far out the user can zoom: the largest span of canvas units the view can show.</summary>
    public float MaximumViewSpan { get; set; } = 1000000000;

    /// <summary>Whether the user is panning with the middle mouse button right now.</summary>
    public bool IsPanning { get; private set; }

    /// <summary>
    /// Ordinary panels placed in canvas coordinates. They lay out, take focus and react to input as usual, and the whole
    /// lot moves and scales with the view. It's 1000 × 1000 pixels unless you restyle it, and panels can sit outside that
    /// too. It's only made the first time you use it.
    /// </summary>
    public Panel Content
    {
        get
        {
            if (_content is not null)
            {
                return _content;
            }

            _contentViewport = AddChild<Panel>("canvas-viewport");
            _contentViewport.Style.Position = PositionMode.Absolute;
            _contentViewport.Style.Overflow = OverflowMode.Hidden;
            _contentViewport.CanDragScroll = false;
            _content = _contentViewport.AddChild<Panel>("canvas-content");
            _content.Style.Position = PositionMode.Absolute;
            _content.Style.Left = 0;
            _content.Style.Top = 0;
            _content.Style.Width = 1000;
            _content.Style.Height = 1000;
            _content.Style.Overflow = OverflowMode.Visible;
            _content.Style.TransformOriginX = 0;
            _content.Style.TransformOriginY = 0;
            _content.CanDragScroll = false;
            UpdateContentTransform();
            return _content;
        }
    }

    /// <summary>Whether the user can pan and zoom right now. By default, not while a <see cref="Handle"/> is being dragged.</summary>
    protected virtual bool CanNavigate => !Descendants.OfType<Handle>().Any(x => x.IsDragging);

    /// <summary>The part of the panel the canvas shows in, in pixels from its top left corner. The whole panel by default.</summary>
    protected virtual Rect Viewport => new(Vector2.Zero, Box.Rect.Size);

    private Vector2 ViewScale
    {
        get
        {
            var scale = new Vector2(Math.Max(1, Viewport.Width), Math.Max(1, Viewport.Height)) / (ViewMax - ViewMin);
            return PreserveAspectRatio ? new Vector2(Math.Min(scale.X, scale.Y)) : scale;
        }
    }

    /// <summary>Shows canvas coordinates <paramref name="min"/> to <paramref name="max"/>, fitted into the panel.</summary>
    /// <exception cref="ArgumentException">The bounds aren't finite, or <paramref name="max"/> isn't greater than <paramref name="min"/> on both axes.</exception>
    public void SetView(Vector2 min, Vector2 max)
    {
        var size = max - min;
        if (!Painter.IsFinite(min) || !Painter.IsFinite(max) || !Painter.IsFinite(size) || size.X <= 0 || size.Y <= 0)
        {
            throw new ArgumentException("Canvas bounds must be finite and increasing.");
        }

        _zoomSpan = null;
        ViewMin = min;
        ViewMax = max;
        ConstrainView();
        UpdateContentTransform();
    }

    /// <summary>Where canvas point <paramref name="point"/> is on the panel, in pixels from its top left corner, the same space <see cref="Panel.OnDraw"/> and <see cref="Panel.MousePosition"/> use.</summary>
    public Vector2 CanvasToScreen(Vector2 point)
    {
        var offset = (point - ((ViewMin + ViewMax) * 0.5f)) * ViewScale;
        if (YAxisUp)
        {
            offset.Y = -offset.Y;
        }

        return Viewport.Position + (Viewport.Size * 0.5f) + offset;
    }

    /// <summary>The canvas point under <paramref name="point"/>, given in pixels from the panel's top left corner.</summary>
    public Vector2 ScreenToCanvas(Vector2 point)
    {
        var offset = (point - Viewport.Position - (Viewport.Size * 0.5f)) / ViewScale;
        if (YAxisUp)
        {
            offset.Y = -offset.Y;
        }

        return ((ViewMin + ViewMax) * 0.5f) + offset;
    }

    /// <summary>Moves the view as if the user dragged the canvas by <paramref name="screenDelta"/> pixels.</summary>
    public void Pan(Vector2 screenDelta)
    {
        var delta = ScreenToCanvas(Vector2.Zero) - ScreenToCanvas(screenDelta);
        var zoomSpan = _zoomSpan;
        SetView(ViewMin + delta, ViewMax + delta);
        _zoomSpan = zoomSpan;
    }

    /// <summary>Zooms by <paramref name="factor"/>, keeping the canvas point under <paramref name="screenAnchor"/> where it is. Above 1 zooms out, below 1 zooms in.</summary>
    public void ZoomAt(Vector2 screenAnchor, float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0)
        {
            return;
        }

        bool independentLimit = !PreserveAspectRatio && NavigationBounds.HasValue && ConstrainHorizontalNavigation != ConstrainVerticalNavigation;
        var size = (independentLimit ? _zoomSpan ?? (ViewMax - ViewMin) : ViewMax - ViewMin) * factor;
        if (!Painter.IsFinite(size) || size.X < MinimumViewSpan || size.X > MaximumViewSpan || size.Y < MinimumViewSpan || size.Y > MaximumViewSpan)
        {
            return;
        }

        var anchor = ScreenToCanvas(screenAnchor);
        var scale = size / (ViewMax - ViewMin);
        SetView(anchor + ((ViewMin - anchor) * scale), anchor + ((ViewMax - anchor) * scale));
        if (independentLimit)
        {
            _zoomSpan = size;
        }
    }

    /// <summary>Zooms around the mouse.</summary>
    public override void OnMouseWheel(Vector2 value)
    {
        if (CanNavigate && !IsPanning && !IsBoxSelecting && Viewport.IsInside(MousePosition))
        {
            ZoomAt(MousePosition, MathF.Pow(1.15f, Math.Clamp(value.Y != 0 ? value.Y : value.X, -5, 5)));
        }
    }

    /// <inheritdoc/>
    public override void Tick()
    {
        base.Tick();
        ConstrainView();
        UpdateContentTransform();
        if (_checkFocus)
        {
            _checkFocus = false;
            if (!HasFocus && !Descendants.Any(x => x.HasFocus))
            {
                IsPanning = false;
                FinishBoxSelection(true);
            }
        }

        if (IsPanning && !HasActive)
        {
            IsPanning = false;
        }

        if (IsBoxSelecting && (!HasActive || !BoxSelectionEnabled))
        {
            FinishBoxSelection(true);
        }
    }

    /// <summary>Starts a box selection on empty canvas with the left button, or panning with the middle button.</summary>
    protected override void OnMouseDown(MousePanelEvent e)
    {
        if (e.Button == "mouseleft" && BoxSelectionEnabled && CanNavigate && Viewport.IsInside(MousePosition)
            && (e.Target == this || e.Target == _content || e.Target == _contentViewport))
        {
            BeginBoxSelection(e.HasShift);
            e.StopPropagation();
            return;
        }

        if (e.Button != "mousemiddle" || IsBoxSelecting || !CanNavigate || !Viewport.IsInside(MousePosition))
        {
            base.OnMouseDown(e);
            return;
        }

        e.StopPropagation();
        Focus();
        IsPanning = true;
        _panMouse = MousePosition;
    }

    /// <summary>Grows the selection box or pans the view as the mouse moves.</summary>
    protected override void OnMouseMove(MousePanelEvent e)
    {
        if (IsBoxSelecting)
        {
            UpdateBoxSelection();
            e.StopPropagation();
            return;
        }

        if (!IsPanning)
        {
            base.OnMouseMove(e);
            return;
        }

        e.StopPropagation();
        Pan(MousePosition - _panMouse);
        _panMouse = MousePosition;
    }

    /// <summary>Finishes a box selection or a pan.</summary>
    protected override void OnMouseUp(MousePanelEvent e)
    {
        if (IsBoxSelecting && e.Button == "mouseleft")
        {
            UpdateBoxSelection();
            FinishBoxSelection();
            e.StopPropagation();
            return;
        }

        if (!IsPanning || e.Button != "mousemiddle")
        {
            base.OnMouseUp(e);
            return;
        }

        e.StopPropagation();
        IsPanning = false;
    }

    /// <summary>Cancels a box selection when the canvas loses focus.</summary>
    protected override void OnBlur(PanelEvent e)
    {
        if (e.Target == this)
        {
            _checkFocus = true;
            FinishBoxSelection(true);
        }

        base.OnBlur(e);
    }

    private void UpdateContentTransform()
    {
        if (_content is null || _contentViewport is null || _content.IsDeleting || ScaleToScreen <= 0)
        {
            return;
        }

        var viewport = Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var state = (ViewMin, ViewMax, viewport, ScaleToScreen, YAxisUp, PreserveAspectRatio);
        if (_lastContentView == state)
        {
            return;
        }

        _lastContentView = state;
        _contentViewport.Style.Left = viewport.Left / ScaleToScreen;
        _contentViewport.Style.Top = viewport.Top / ScaleToScreen;
        _contentViewport.Style.Width = viewport.Width / ScaleToScreen;
        _contentViewport.Style.Height = viewport.Height / ScaleToScreen;

        var offset = (CanvasToScreen(Vector2.Zero) - viewport.Position) / ScaleToScreen;
        var scale = ViewScale / ScaleToScreen;
        if (YAxisUp)
        {
            scale.Y = -scale.Y;
        }

        var transform = new PanelTransform();
        transform.AddTranslate(offset.X, offset.Y);
        transform.AddScale(new Vector3(scale.X, scale.Y, 1));
        _content.Style.Transform = transform;
    }
}
