namespace Socotra;

public readonly ref partial struct Painter
{
    internal static partial class Path
    {
        internal sealed class Data
        {
            internal static int MaxPrimitiveCount => Math.Min(PainterBatcher.MaxBufferElements<PathPrimitive>(), (PainterBatcher.MaxBufferElements<PathNode>() + 1) / 2);

            internal ShapeInstance Shape { get; }
            internal Data? AlignmentMask { get; }
            readonly PathPrimitive[] _primitives;
            readonly PathNode[] _nodes;
            internal ReadOnlySpan<PathPrimitive> Primitives => _primitives;
            internal ReadOnlySpan<PathNode> Nodes => _nodes;

            internal Data(ShapeInstance shape, ReadOnlySpan<PathPrimitive> primitives, Data? alignmentMask = null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(primitives.Length, MaxPrimitiveCount);
                Shape = shape;
                AlignmentMask = alignmentMask;
                _primitives = primitives.ToArray();
                _nodes = new PathNode[checked(Math.Max(0, primitives.Length * 2 - 1))];
                if (primitives.IsEmpty)
                {
                    return;
                }

                Span<BoundsEntry> entries = primitives.Length <= 128 ? stackalloc BoundsEntry[primitives.Length] : new BoundsEntry[primitives.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i] = new(shape.Kind == ShapeKind.PolygonPath ? SegmentBounds(primitives[i].A)
                                        : PrimitiveBounds(primitives[i], shape.Circle.Z * 0.5f), i);
                }

                int nodeCount = 0;
                Build(entries, _nodes, ref nodeCount);
            }

            readonly record struct BoundsEntry(Rect Bounds, int Primitive);

            readonly struct BoundsComparer(bool horizontal) : IComparer<BoundsEntry>
            {
                public int Compare(BoundsEntry a, BoundsEntry b) => horizontal
                    ? a.Bounds.Center.X.CompareTo(b.Bounds.Center.X)
                    : a.Bounds.Center.Y.CompareTo(b.Bounds.Center.Y);
            }

            static void Build(Span<BoundsEntry> entries, Span<PathNode> nodes, ref int nodeCount)
            {
                var min = entries[0].Bounds.Position;
                var max = entries[0].Bounds.BottomRight;
                for (int i = 1; i < entries.Length; i++)
                {
                    min = Vector2.Min(min, entries[i].Bounds.Position);
                    max = Vector2.Max(max, entries[i].Bounds.BottomRight);
                }
                int index = nodeCount++;
                if (entries.Length > 1)
                {
                    entries.Sort(new BoundsComparer(max.X - min.X > max.Y - min.Y));
                    int left = entries.Length / 2;
                    Build(entries[..left], nodes, ref nodeCount);
                    Build(entries[left..], nodes, ref nodeCount);
                }
                nodes[index] = new PathNode
                {
                    Bounds = new Vector4(min.X, min.Y, max.X, max.Y),
                    Next = nodeCount,
                    Primitive = entries.Length == 1 ? entries[0].Primitive : -1,
                };
            }
        }
    }
}
