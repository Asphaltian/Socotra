namespace Socotra;

public readonly ref partial struct Painter
{
    internal float InheritedOpacity => GetActiveContext().InheritedOpacity;

    internal BlendMode InheritedBlendMode => GetActiveContext().InitialBlendMode;

    private PainterBatcher Output => GetActiveContext().Batcher;

    internal void Flush() => Output.Flush();

    internal void SetViewport(Rect bounds) => Output.SetViewport(bounds);

    internal DestinationScope WithDestination(Rect bounds, float scale, float opacity, BlendMode blendMode, Matrix4x4 transform) =>
        new(this, bounds, scale, opacity, blendMode, transform);

    internal ContentScope WithContentOrigin(Vector2 origin) => new(GetActiveContext(), origin);

    internal DestinationClipScope ClipDestination(Rect rect, BorderRadii radii, Matrix4x4 transform) => new(GetActiveContext(), rect, radii, transform);

    internal TargetScope Target(Rect bounds) => new(this, bounds);

    internal void FilterBackdrop(Rect rect, Filter filter, BorderRadii radii) =>
        Output.AddBackdrop(new BackdropData(rect, filter, radii, InheritedOpacity), InheritedBlendMode);

    internal ref struct ContentScope
    {
        private readonly Matrix4x4 _origin;
        private Context? _context;

        public ContentScope(Context context, Vector2 origin)
        {
            _context = context;
            _origin = context.BaseTransform;
            context.ResetDrawingState(context.InitialBlendMode);
            context.BaseTransform = Matrix4x4.CreateTranslation(new Vector3(origin, 0));
        }

        public readonly Painter Painter => new(_context!);

        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            _context.ResetDrawingState(_context.InitialBlendMode);
            _context.BaseTransform = _origin;
            _context = null;
        }
    }

    internal ref struct DestinationScope
    {
        private readonly PainterBatcher _output;
        private readonly Matrix4x4 _transform;
        private readonly Rect _bounds;
        private readonly float _scale;
        private readonly float _opacity;
        private readonly Matrix4x4 _baseTransform;
        private readonly BlendMode _blendMode;
        private Context? _context;

        public DestinationScope(Painter painter, Rect bounds, float scale, float opacity, BlendMode blendMode, Matrix4x4 transform)
        {
            var context = painter.GetActiveContext();
            _context = context;
            _output = context.Batcher;
            _transform = _output.Destination.Transform;
            _bounds = context.Bounds;
            _scale = context.ScaleToScreen;
            _opacity = context.InheritedOpacity;
            _baseTransform = context.BaseTransform;
            _blendMode = context.InitialBlendMode;
            context.ResetDrawingState(blendMode);
            context.Bounds = bounds;
            context.ScaleToScreen = scale;
            context.InheritedOpacity = opacity;
            context.BaseTransform = Matrix4x4.Identity;
            context.DestinationDepth++;
            _output.Destination.Transform = transform;
        }

        public readonly Painter Painter => new(_context!);

        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            _output.Destination.Transform = _transform;
            _context.ResetDrawingState(_blendMode);
            _context.Bounds = _bounds;
            _context.ScaleToScreen = _scale;
            _context.InheritedOpacity = _opacity;
            _context.BaseTransform = _baseTransform;
            _context.DestinationDepth--;
            _context = null;
        }
    }

    internal ref struct DestinationClipScope
    {
        private readonly long _recording;
        private readonly int _index;
        private Context? _context;

        public DestinationClipScope(Context context, Rect rect, BorderRadii radii, Matrix4x4 transform)
        {
            _context = context;
            _recording = context.Recording;
            _index = context.Batcher.PushClip(rect, radii, transform);
        }

        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            if (_context.IsActive && _context.Recording == _recording)
            {
                _context.Batcher.PopClip(_index);
            }

            _context = null;
        }
    }

    internal ref struct TargetScope
    {
        private readonly long _recording;
        private readonly int _index;
        private Context? _context;

        public TargetScope(Painter painter, Rect bounds)
        {
            var context = painter.GetActiveContext();
            _context = context;
            _recording = context.Recording;
            var output = context.Batcher;
            output.Flush();
            _index = output.PushTarget();
            Layer = output.List.NextLayer();
            output.List.Commands.Add(new BeginLayerCommand(Layer, (int)MathF.Ceiling(bounds.Width), (int)MathF.Ceiling(bounds.Height)));
            output.Destination.Layered = true;
            output.Destination.LayerMatrix = output.Destination.Transform.Inverted * Matrix4x4.CreateTranslation(new Vector3(-bounds.Position, 0));
        }

        public int Layer { get; }

        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            var context = _context;
            _context = null;
            if (!context.IsActive || context.Recording != _recording)
            {
                return;
            }

            var output = context.Batcher;
            try
            {
                output.Flush();
            }
            finally
            {
                output.PopTarget(_index);
                output.List.Commands.Add(new EndLayerCommand());
            }
        }
    }
}
