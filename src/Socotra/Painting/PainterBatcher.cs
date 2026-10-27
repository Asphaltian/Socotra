using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Socotra;

internal sealed partial class PainterBatcher(DrawList list)
{
    private readonly Dictionary<Painter.Path.Data, int> _pathLookup = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(int Clip, Matrix4x4 Transform, int Inherited), int> _drawClipLookup = [];
    private readonly List<int> _drawClipStack = [];
    private Target[] _targetStack = [];
    private ClipRestore[] _clipStack = [];
    private int _targetDepth;
    private int _clipDepth;
    private int _batchStart;
    private BlendMode _blendMode;

    public Table<BoxInstance> Boxes { get; } = new();

    public Table<TextInstance> Texts { get; } = new();

    public Table<(Painter.Scissoring Scissor, int Next), ScissorInstance> Scissors { get; } = new();

    public Table<Matrix4x4, Matrix4x4> Transforms { get; } = new();

    public Table<GradientInfo, GradientInstance> Gradients { get; } = new();

    public Table<ShapeInstance, ShapeInstance> Shapes { get; } = new();

    public Table<PathPrimitive> Paths { get; } = new();

    public Table<PathNode> PathNodes { get; } = new();

    public Table<Vector2> PolygonPoints { get; } = new();

    public List<Painter.ClipEntry> DrawClips { get; } = [];

    public DrawList List => list;

    public const int MaxPolygonPoints = 128;

    public static int MaxBufferElements<T>() where T : unmanaged => (int)Math.Min(Array.MaxLength, uint.MaxValue / (long)Unsafe.SizeOf<T>());

    public Target Destination;

    public void Clear()
    {
        InvalidateBackdrop();
        Boxes.Clear();
        Texts.Clear();
        Scissors.Clear();
        Transforms.Clear();
        Gradients.Clear();
        Shapes.Clear();
        Paths.Clear();
        PathNodes.Clear();
        PolygonPoints.Clear();
        _pathLookup.Clear();
        _batchStart = 0;
        _targetDepth = 0;
        _clipDepth = 0;
        Destination = new Target();
        ClearClips();
    }

    public void ClearClips()
    {
        DrawClips.Clear();
        _drawClipLookup.Clear();
    }

    public void Clear(Color color)
    {
        Flush();
        list.Commands.Add(new ClearCommand(color));
    }

    public void SetViewport(Rect bounds)
    {
        Flush();
        Destination.Viewport = bounds;
        Destination.SetScissor(Painter.Scissoring.Single(bounds, BorderRadii.Zero, Matrix4x4.Identity));
    }

    public void Flush()
    {
        FlushBatch();
        InvalidateBackdrop();
    }

    public int PushTarget()
    {
        if (_targetDepth == _targetStack.Length)
        {
            Array.Resize(ref _targetStack, Math.Max(4, _targetDepth * 2));
        }

        _targetStack[_targetDepth] = Destination;
        return _targetDepth++;
    }

    public void PopTarget(int index)
    {
        if (index != _targetDepth - 1)
        {
            throw new InvalidOperationException("Restore paint targets in reverse order.");
        }

        Destination = _targetStack[index];
        _targetDepth = index;
    }

    public int PushClip(Rect rect, BorderRadii radii, Matrix4x4 transform)
    {
        if (_clipDepth == _clipStack.Length)
        {
            Array.Resize(ref _clipStack, Math.Max(8, _clipDepth * 2));
        }

        Destination.SaveClip(ref _clipStack[_clipDepth]);
        Destination.PushClip(rect, radii, transform);
        return _clipDepth++;
    }

    public void PopClip(int index)
    {
        if (index != _clipDepth - 1)
        {
            throw new InvalidOperationException("Restore destination clips in reverse order.");
        }

        Destination.RestoreClip(in _clipStack[index]);
        _clipDepth = index;
    }

    public void Add(in Painter.BoxDescriptor descriptor, Matrix4x4? transform = null, int clipIndex = -1) =>
        Add(descriptor, 1, descriptor.OverrideBlendMode, transform, clipIndex);

    public void Add(in Painter.BoxDescriptor descriptor, float opacity, BlendMode blendMode, Matrix4x4? transform = null, int clipIndex = -1)
    {
        Resolve(descriptor, transform ?? Matrix4x4.Identity, clipIndex, out var instance);
        instance.ApplyOpacity(opacity);
        Append(instance, blendMode);
    }

    public void AddSolidShape(Rect bounds, Color color, int shapeIndex, BackgroundClip backgroundClip,
        in Vector4 fillInsets, in Matrix4x4 transform, int clipIndex, BlendMode blendMode)
    {
        BoxInstance instance = default;
        instance.Rect = new Vector4(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        instance.Color = color;
        instance.ShapeIndex = shapeIndex;
        instance.InverseScissorIndex = -1;
        instance.BackgroundClip = (int)backgroundClip;
        instance.BackgroundClipRect = fillInsets;
        ResolveSpatial(ref instance, clipIndex, Destination.ResolveSpatial(this, transform));
        Append(instance, blendMode);
    }

    public void Add(in Painter.ShadowDescriptor descriptor, Matrix4x4? transform = null, int clipIndex = -1)
    {
        Resolve(descriptor, transform ?? Matrix4x4.Identity, clipIndex, out var instance);
        Append(instance, descriptor.Inset ? descriptor.OverrideBlendMode : BlendMode.Normal);
    }

    public void Add(in Painter.OutlineDescriptor descriptor, Matrix4x4? transform = null, int clipIndex = -1)
    {
        Resolve(descriptor, transform ?? Matrix4x4.Identity, clipIndex, out var instance);
        Append(instance, descriptor.OverrideBlendMode);
    }

    public int GetOrAddScissor(in Painter.Scissoring scissor, int next = -1)
    {
        if (scissor.IsEmpty)
        {
            return next;
        }

        return Scissors.TryGet((scissor, next), out var index) ? index : Scissors.Add((scissor, next), ScissorInstance.From(scissor, next));
    }

    public int GetOrAddDrawClip(int index, Matrix4x4 parentTransform, int inherited)
    {
        _drawClipStack.Clear();
        int next = inherited;
        while (index >= 0)
        {
            if (_drawClipLookup.TryGetValue((index, parentTransform, inherited), out next))
            {
                break;
            }

            _drawClipStack.Add(index);
            index = DrawClips[index].Parent;
            next = inherited;
        }

        if (_drawClipStack.Count == 0)
        {
            return next;
        }

        var inverse = parentTransform.Inverted;
        for (int i = _drawClipStack.Count - 1; i >= 0; i--)
        {
            index = _drawClipStack[i];
            var clip = DrawClips[index];
            next = GetOrAddScissor(Painter.Scissoring.Single(clip.Rect, clip.Radii, inverse * clip.Transform), next);
            _drawClipLookup.Add((index, parentTransform, inherited), next);
        }

        return next;
    }

    public int GetOrAddGradient(in GradientInfo gradient) =>
        Gradients.TryGet(gradient, out var index) ? index : Gradients.Add(gradient, GradientInstance.From(in gradient));

    public int GetOrAddShape(in ShapeInstance shape)
    {
        if (shape.Kind == ShapeKind.None)
        {
            return -1;
        }

        return Shapes.GetOrAdd(shape, shape);
    }

    public int AddShape(in ShapeInstance shape) => Shapes.Add(shape);

    public int AddPath(ShapeInstance shape, ReadOnlySpan<PathPrimitive> primitives, int maskIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(primitives.Length, Painter.Path.Data.MaxPrimitiveCount);
        int nodeCount = checked(Math.Max(0, primitives.Length * 2 - 1));
        if (maskIndex != 0)
        {
            shape.PolygonCount = maskIndex;
        }

        shape.PathOffset = Paths.Count;
        shape.PathCount = primitives.Length;
        shape.PathNodeOffset = PathNodes.Count;
        shape.PathNodeCount = nodeCount;
        Paths.AddRange(primitives);
        Painter.Path.Data.BuildNodes(shape, primitives, PathNodes.Append(nodeCount));
        return Shapes.Add(shape);
    }

    public int AddPolygonPoints(ReadOnlySpan<Vector2> points, Vector2 origin)
    {
        int offset = PolygonPoints.Count;
        var destination = PolygonPoints.Append(points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            destination[i] = points[i] - origin;
        }

        return AddShape(new ShapeInstance
        {
            Kind = ShapeKind.PolygonPath,
            PathOffset = offset,
            PathCount = points.Length,
            PathNodeCount = -1,
        });
    }

    public int GetOrAddPath(Painter.Path.Data path)
    {
        if (_pathLookup.TryGetValue(path, out var existing))
        {
            return existing;
        }

        var maskIndex = path.AlignmentMask is { } mask ? GetOrAddPath(mask) + 1 : 0;
        var shape = path.Shape;
        if (maskIndex != 0)
        {
            shape.PolygonCount = maskIndex;
        }

        shape.PathOffset = Paths.Count;
        shape.PathCount = path.Primitives.Length;
        Paths.AddRange(path.Primitives);
        shape.PathNodeOffset = PathNodes.Count;
        shape.PathNodeCount = path.Nodes.Length;
        PathNodes.AddRange(path.Nodes);
        var index = Shapes.Add(shape);
        _pathLookup.Add(path, index);
        return index;
    }

    public int GetOrAddTransform(Matrix4x4 transform) =>
        Transforms.TryGet(transform, out var index) ? index : Transforms.Add(transform, transform);

    private void FlushBatch()
    {
        int count = Boxes.Count - _batchStart;
        if (count == 0)
        {
            return;
        }

        list.Commands.Add(new BoxesCommand(_batchStart, count, _blendMode, Destination.LayerMatrix));
        _batchStart = Boxes.Count;
    }

    private void Append(in BoxInstance instance, BlendMode blendMode)
    {
        if (_blendMode != blendMode)
        {
            FlushBatch();
        }

        _blendMode = blendMode;
        Boxes.Add(instance);
        TrackBackdropWrite(instance);
    }

    internal struct ClipRestore
    {
        public Rect TopRect;
        public int Count;
        public bool Invert;
        public int? Index;
    }

    internal readonly record struct Spatial(Matrix4x4 Transform, int ScissorIndex, int TransformIndex);

    internal struct Target
    {
        public Rect Viewport;
        public Painter.Scissoring Scissor;
        public Matrix4x4 LayerMatrix = Matrix4x4.Identity;
        public bool Layered;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _localTransform;
        private Matrix4x4 _resolvedTransform;
        private int? _transformIndex;
        private int? _scissorIndex;

        public Target()
        {
        }

        public Matrix4x4 Transform
        {
            readonly get => _transform;
            set
            {
                if (_transform == value)
                {
                    return;
                }

                _transform = value;
                _transformIndex = null;
            }
        }

        public void SetScissor(in Painter.Scissoring scissor)
        {
            if (Scissor.Equals(in scissor))
            {
                return;
            }

            Scissor = scissor;
            _scissorIndex = null;
        }

        public void PushClip(Rect rect, BorderRadii radii, Matrix4x4 transform)
        {
            Scissor.Push(rect, radii, transform);
            _scissorIndex = null;
        }

        public readonly void SaveClip(ref ClipRestore previous)
        {
            previous.Count = Scissor.Count;
            previous.Invert = Scissor.Invert;
            previous.Index = _scissorIndex;
            if (Scissor.Count > 0)
            {
                previous.TopRect = Scissor.Clips[Scissor.Count - 1].Rect;
            }
        }

        public void RestoreClip(in ClipRestore previous)
        {
            Scissor.Count = previous.Count;
            Scissor.Invert = previous.Invert;
            if (previous.Count > 0)
            {
                Scissor.Clips[previous.Count - 1].Rect = previous.TopRect;
            }

            _scissorIndex = previous.Index;
        }

        public Spatial ResolveSpatial(PainterBatcher batcher, in Matrix4x4 localTransform)
        {
            _scissorIndex ??= batcher.GetOrAddScissor(Scissor);
            if (!_transformIndex.HasValue || _localTransform != localTransform)
            {
                _localTransform = localTransform;
                _resolvedTransform = localTransform == Matrix4x4.Identity ? _transform : localTransform * _transform;
                _transformIndex = batcher.GetOrAddTransform(_resolvedTransform);
            }

            return new Spatial(_resolvedTransform, _scissorIndex.Value, _transformIndex.Value);
        }
    }

    internal class Table<T>
        where T : unmanaged
    {
        private readonly List<T> _items = [];

        public int Count => _items.Count;

        public T this[int index] => _items[index];

        public ReadOnlySpan<T> Span => CollectionsMarshal.AsSpan(_items);

        public int Add(in T item)
        {
            Append(1)[0] = item;
            return _items.Count - 1;
        }

        public void AddRange(ReadOnlySpan<T> items) => _items.AddRange(items);

        public Span<T> Append(int count)
        {
            int start = _items.Count;
            CollectionsMarshal.SetCount(_items, checked(start + count));
            return CollectionsMarshal.AsSpan(_items).Slice(start, count);
        }

        public virtual void Clear() => _items.Clear();
    }

    internal sealed class Table<TKey, T> : Table<T>
        where TKey : notnull
        where T : unmanaged
    {
        private readonly Dictionary<TKey, int> _lookup = [];

        public bool TryGet(in TKey key, out int index) => _lookup.TryGetValue(key, out index);

        public int GetOrAdd(in TKey key, in T item)
        {
            ref int index = ref CollectionsMarshal.GetValueRefOrAddDefault(_lookup, key, out bool exists);
            if (!exists)
            {
                index = Add(item);
            }

            return index;
        }

        public int Add(in TKey key, in T item)
        {
            var index = Add(item);
            _lookup.Add(key, index);
            return index;
        }

        public override void Clear()
        {
            base.Clear();
            _lookup.Clear();
        }
    }
}
