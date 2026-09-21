namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// What the inside of shapes is painted with: a color, gradient or image. Nothing is filled until you set it.
    /// </summary>
    public Fill Fill
    {
        get => ActiveContext.State.Fill;
        set => ActiveContext.State.Fill = value;
    }

    /// <summary>
    /// The outline drawn around shapes, and what lines, arcs and curves are drawn with. Nothing is stroked until you set it.
    /// </summary>
    public Stroke Stroke
    {
        get => ActiveContext.State.Stroke;
        set
        {
            if (!Enum.IsDefined(value.Alignment))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (!Enum.IsDefined(value.Style))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            ActiveContext.State.Stroke = value;
        }
    }

    /// <summary>
    /// Fades everything you draw from now on, from 0 (invisible) to 1 (as is).
    /// </summary>
    public float Opacity
    {
        get => ActiveContext.State.Opacity;
        set
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            ActiveContext.State.Opacity = Math.Clamp(value, 0, 1);
        }
    }

    /// <summary>
    /// The font, size, color and alignment <see cref="Text"/> and <see cref="MeasureText(string, Vector2)"/> use.
    /// Starts as <see cref="TextStyle.Default"/>.
    /// </summary>
    public TextStyle TextStyle
    {
        get => ActiveContext.State.TextStyle;
        set
        {
            value.Validate();
            ActiveContext.State.TextStyle = value;
        }
    }

    /// <summary>
    /// How what you draw from now on mixes with what's already there. Starts as <see cref="BlendMode.Normal"/>.
    /// </summary>
    public BlendMode BlendMode
    {
        get => ActiveContext.State.OverrideBlendMode;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            ActiveContext.State.OverrideBlendMode = value;
        }
    }

    /// <summary>
    /// Lets you change the fill, stroke, opacity, text style, blend mode, transform and clip for a while. They go back
    /// to how they were when you dispose the scope.
    /// </summary>
    /// <example>
    /// <code>
    /// using (painter.Scope())
    /// {
    ///     // rotated and faded only inside this block
    ///     painter.Rotate(45);
    ///     painter.Opacity = 0.5f;
    ///     painter.Rect(new Rect(0, 0, 20, 20));
    /// }
    /// </code>
    /// </example>
    public StateScope Scope() => new(ActiveContext);

    /// <summary>
    /// Returned by <see cref="Scope"/>. Dispose it to put the drawing state back; dispose nested scopes in reverse order.
    /// </summary>
    public ref struct StateScope
    {
        Painter.Context? _context;
        readonly State _state;
        readonly long _recording;

        internal StateScope(Painter.Context context)
        {
            _context = context;
            _state = context.State;
            _recording = context.Recording;
        }

        /// <summary>Puts the drawing state back to how it was when you called <see cref="Scope"/>.</summary>
        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            if (_context.IsActive && _context.Recording == _recording)
            {
                _context.State = _state;
            }

            _context = null;
        }
    }

    internal struct State
    {
        public float Opacity = 1f;
        public BlendMode OverrideBlendMode = BlendMode.Normal;
        public Matrix4x4 Transform = Matrix4x4.Identity;

        public int ClipIndex = -1;

        public readonly bool HasArea => (double)Transform.M11 * Transform.M22 != (double)Transform.M12 * Transform.M21;

        public Stroke Stroke;
        public Vector4 FillInsets;
        public Texture? FillMask;
        public Rect FillMaskRect;
        public Fill Fill;
        public TextStyle TextStyle = TextStyle.Default;

        public State() { }
    }
}
