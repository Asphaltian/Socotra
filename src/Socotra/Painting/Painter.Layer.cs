namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Groups what you draw next so it fades, blurs, recolors or is masked as a whole. Dispose the returned scope to
    /// finish the group.
    /// </summary>
    /// <param name="bounds">The area the group covers. Anything drawn outside it is cut off.</param>
    /// <param name="opacity">The group's opacity, from 0 to 1.</param>
    /// <param name="filter">Effects for the whole group, like a blur.</param>
    /// <param name="mask">An image that hides parts of the group.</param>
    /// <example>
    /// <code>
    /// // two overlapping circles fading together, without the overlap showing through
    /// using (painter.BeginLayer(new Rect(0, 0, 200, 100), opacity: 0.5f))
    /// {
    ///     painter.Fill = Color.White;
    ///     painter.Circle(new Vector2(70, 50), 40);
    ///     painter.Circle(new Vector2(130, 50), 40);
    /// }
    /// </code>
    /// </example>
    public LayerScope BeginLayer(Rect bounds, float opacity = 1, Filter? filter = null, Mask? mask = null)
    {
        if (!ValidBounds(bounds))
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity));
        }

        var effects = filter ?? new Filter();
        effects.Validate();
        if (mask is { } m && (!ValidBounds(m.Rect) || !float.IsFinite(m.Rotation) || !Enum.IsDefined(m.Mode) || !Enum.IsDefined(m.Repeat) || !Enum.IsDefined(m.Sampling)))
        {
            throw new ArgumentOutOfRangeException(nameof(mask));
        }

        return new LayerScope(ActiveContext, bounds, opacity, effects, mask);
    }

    /// <summary>A group started with <see cref="BeginLayer"/>. Dispose it to draw the group; dispose nested groups in reverse order.</summary>
    public ref struct LayerScope
    {
        private readonly State _state;
        private readonly long _recording;
        private readonly PainterBatcher.Target _destination;
        private readonly Rect _bounds;
        private readonly Matrix4x4 _baseTransform;
        private readonly float _inheritedOpacity;
        private readonly int _layer;
        private readonly int? _previousLayer;
        private readonly Rect _layerBounds;
        private readonly Filter _filter;
        private readonly Mask? _mask;
        private readonly float _opacity;
        private Context? _context;

        internal LayerScope(Context context, Rect bounds, float opacity, Filter filter, Mask? mask)
        {
            _context = context;
            _recording = context.Recording;
            _state = context.State;
            _bounds = context.Bounds;
            _baseTransform = context.BaseTransform;
            _inheritedOpacity = context.InheritedOpacity;
            _opacity = opacity;
            _layerBounds = bounds;
            _filter = filter;
            _mask = mask;

            var output = context.Batcher;
            output.Flush();
            _destination = output.Destination;
            _layer = output.List.NextLayer();
            _previousLayer = context.ActiveLayer;
            context.ActiveLayer = _layer;
            output.List.Commands.Add(new BeginLayerCommand(_layer, (int)MathF.Ceiling(bounds.Width), (int)MathF.Ceiling(bounds.Height)));
            output.Destination = new PainterBatcher.Target { Layered = true };
            context.Bounds = bounds;
            context.BaseTransform = Matrix4x4.CreateTranslation(-bounds.Left, -bounds.Top, 0);
            context.InheritedOpacity = 1;
            context.State.Transform = Matrix4x4.Identity;
            context.State.Opacity = 1;
            context.State.OverrideBlendMode = BlendMode.Normal;
            context.State.ClipIndex = -1;
        }

        /// <summary>Finishes the group and puts the drawing state back to how it was before <see cref="BeginLayer"/>.</summary>
        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            var owner = _context;
            _context = null;
            if (owner.Recording != _recording)
            {
                return;
            }

            if (!owner.IsActive || owner.ActiveLayer != _layer)
            {
                throw new InvalidOperationException("Dispose layers in reverse order before the painter finishes.");
            }

            var output = owner.Batcher;
            try
            {
                output.Flush();
            }
            finally
            {
                output.List.Commands.Add(new EndLayerCommand());
                output.Destination = _destination;
                owner.ActiveLayer = _previousLayer;
                owner.Bounds = _bounds;
                owner.BaseTransform = _baseTransform;
                owner.InheritedOpacity = _inheritedOpacity;
                owner.State = _state;
            }

            if (_state.HasArea && _state.Opacity * _inheritedOpacity * _opacity > 0)
            {
                owner.Painter.CompositeLayer(_layer, _layerBounds, _filter, _mask, _opacity);
            }
        }
    }
}
