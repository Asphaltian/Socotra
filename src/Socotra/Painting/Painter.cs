namespace Socotra;

/// <summary>
/// Draws shapes, images and text. Set <see cref="Fill"/>, <see cref="Stroke"/> or <see cref="TextStyle"/>, then call a
/// drawing method. Panels get one in <see cref="Panel.OnDraw"/>; to draw without panels, start one with <see cref="Begin(DrawList, Rect)"/>.
/// </summary>
/// <example>
/// <code>
/// public override void OnDraw(Painter painter)
/// {
///     // a rounded card with a 2px border, the size of the panel
///     painter.Fill = Color.Black.WithAlpha(0.6f);
///     painter.Stroke = Stroke.Solid(Color.White, 2);
///     painter.Rect(painter.Bounds, 8);
///
///     // a title centered on it
///     painter.TextStyle = new TextStyle("Poppins", 20).WithAlignment(TextFlag.Center);
///     painter.Text("Paused", painter.Bounds);
/// }
/// </code>
/// </example>
public readonly ref partial struct Painter
{
    internal static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    internal static bool IsFinite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);

    private readonly Context? _context;
    private readonly long _recording;
    private readonly bool _ownsContext;

    internal Painter(Context context, bool ownsContext = false)
    {
        _context = context;
        _recording = context.Recording;
        _ownsContext = ownsContext;
    }

    /// <summary>The area you're drawing into. In <see cref="Panel.OnDraw"/> it starts at 0, 0 and is the panel's size.</summary>
    public Rect Bounds => GetActiveContext().Bounds;

    internal Context ActiveContext
    {
        get
        {
            var context = GetActiveContext();
            context.InitializeState();
            return context;
        }
    }

    /// <summary>
    /// Starts drawing into <paramref name="list"/> over <paramref name="bounds"/>, replacing what it held. Dispose the
    /// painter when you're done, then hand the list to your renderer.
    /// </summary>
    /// <example>
    /// <code>
    /// var list = new DrawList();
    /// using (var painter = Painter.Begin(list, new Rect(0, 0, width, height)))
    /// {
    ///     // start from black, then draw a white circle in the middle
    ///     painter.Clear(Color.Black);
    ///     painter.Fill = Color.White;
    ///     painter.Circle(new Vector2(width, height) / 2, 50);
    /// }
    /// // list is ready to draw, e.g. with VulkanRenderer.Record
    /// </code>
    /// </example>
    public static Painter Begin(DrawList list, Rect bounds)
    {
        list.Reset(bounds.Size);
        return list.PainterContext.Begin(bounds);
    }

    /// <summary>
    /// Starts drawing into <paramref name="texture"/>, made with <see cref="Texture.CreateRenderTarget"/>. What's already in
    /// it stays unless you call <see cref="Clear"/>. Dispose the painter when you're done; the drawing shows up the next
    /// time a renderer draws something that uses the texture.
    /// </summary>
    /// <example>
    /// <code>
    /// // A badge drawn once and shown by any number of panels
    /// var badge = Texture.CreateRenderTarget(64, 64);
    /// using (var painter = Painter.Begin(badge))
    /// {
    ///     painter.Fill = new Color(0.2f, 0.45f, 0.92f);
    ///     painter.Circle(new Vector2(32, 32), 30);
    /// }
    ///
    /// icon.Style.BackgroundImage = badge;
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">The texture wasn't made with <see cref="Texture.CreateRenderTarget"/>.</exception>
    public static Painter Begin(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (!texture.IsRenderTarget)
        {
            throw new InvalidOperationException("Make the texture with Texture.CreateRenderTarget before painting it.");
        }

        var list = new DrawList();
        list.Reset(new Vector2(texture.Width, texture.Height));
        list.PainterContext.Ended = () => texture.AddPaint(list);
        return list.PainterContext.Begin(new Rect(0, 0, texture.Width, texture.Height));
    }

    /// <summary>Finishes a painter you started with <see cref="Begin(DrawList, Rect)"/> or <see cref="Begin(Texture)"/>. You don't need to dispose the one a panel is given.</summary>
    public void Dispose()
    {
        if (_ownsContext && _context is not null && _context.Recording == _recording)
        {
            _context.End();
        }
    }

    /// <summary>
    /// Fills everything drawn so far with <paramref name="color"/>. Throws if you call it inside
    /// <see cref="Panel.OnDraw"/> or a layer; use it on a painter from <see cref="Begin(DrawList, Rect)"/> or <see cref="Begin(Texture)"/>.
    /// </summary>
    public void Clear(Color color)
    {
        var context = GetActiveContext();
        if (!context.CanClear)
        {
            throw new InvalidOperationException("Clear only works on a painter from Begin, outside layers.");
        }

        context.Batcher.Clear(color);
    }

    private Context GetActiveContext()
    {
        if (_context is null || !_context.IsActive || _context.Recording != _recording)
        {
            throw new ObjectDisposedException(nameof(Painter), "The painter has finished.");
        }

        return _context;
    }
}
