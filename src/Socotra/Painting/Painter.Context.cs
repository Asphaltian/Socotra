namespace Socotra;

public readonly ref partial struct Painter
{
    internal sealed class Context(PainterBatcher batcher)
    {
        public readonly List<TextInstance> TextInstances = [];
        public State State;
        public bool HasState;
        public BlendMode InitialBlendMode;
        public int DestinationDepth;
        public int? ActiveLayer;
        public float ScaleToScreen = 1;
        public float InheritedOpacity = 1;
        public Rect Bounds;
        public Matrix4x4 BaseTransform = Matrix4x4.Identity;
        public Action? Ended;
        private bool _active;
        private int _thread;

        public PainterBatcher Batcher => batcher;

        public long Recording { get; private set; }

        public bool IsActive => _active && _thread == Environment.CurrentManagedThreadId;

        public bool CanClear => ActiveLayer is null && DestinationDepth == 0;

        public Painter Painter => IsActive ? new Painter(this) : throw new ObjectDisposedException(nameof(Painter), "The paint context has ended.");

        public void InitializeState()
        {
            if (HasState)
            {
                return;
            }

            State = new State { OverrideBlendMode = InitialBlendMode };
            HasState = true;
        }

        public void ResetDrawingState(BlendMode blendMode)
        {
            HasState = false;
            InitialBlendMode = blendMode;
        }

        public Painter Begin(Rect bounds)
        {
            if (_active)
            {
                throw new InvalidOperationException("This draw list is already being painted.");
            }

            ResetDrawingState(BlendMode.Normal);
            InitializeState();
            Recording++;
            ScaleToScreen = 1;
            InheritedOpacity = 1;
            Bounds = bounds;
            BaseTransform = Matrix4x4.Identity;
            _thread = Environment.CurrentManagedThreadId;
            _active = true;
            return new Painter(this, ownsContext: true);
        }

        public void End()
        {
            if (!_active)
            {
                return;
            }

            if (_thread != Environment.CurrentManagedThreadId)
            {
                throw new InvalidOperationException("End painting on the thread that began it.");
            }

            if (ActiveLayer is not null)
            {
                throw new InvalidOperationException("Dispose layers before ending painting.");
            }

            _active = false;
            batcher.Flush();
            ClearState();
            Ended?.Invoke();
        }

        public void Reset()
        {
            _active = false;
            Recording++;
            DestinationDepth = 0;
            ActiveLayer = null;
            ClearState();
        }

        private void ClearState()
        {
            State = default;
            HasState = false;
        }
    }
}
