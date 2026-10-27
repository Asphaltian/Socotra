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
                BuildNodes(shape, primitives, _nodes);
            }

            internal static void BuildNodes(in ShapeInstance shape, ReadOnlySpan<PathPrimitive> primitives, Span<PathNode> nodes)
            {
                if (primitives.IsEmpty)
                {
                    return;
                }

                int nodeCount = 0;
                Build(shape, primitives, 0, primitives.Length, nodes, ref nodeCount);
            }

            static Vector4 Build(in ShapeInstance shape, ReadOnlySpan<PathPrimitive> primitives, int start, int count, Span<PathNode> nodes, ref int nodeCount)
            {
                int index = nodeCount++;
                Vector4 bounds;
                if (count == 1)
                {
                    var rect = shape.Kind == ShapeKind.PolygonPath ? SegmentBounds(primitives[start].A)
                        : PrimitiveBounds(primitives[start], shape.Circle.Z * 0.5f);
                    bounds = new Vector4(rect.Left, rect.Top, rect.Right, rect.Bottom);
                }
                else
                {
                    int left = count / 2;
                    var a = Build(shape, primitives, start, left, nodes, ref nodeCount);
                    var b = Build(shape, primitives, start + left, count - left, nodes, ref nodeCount);
                    bounds = new Vector4(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.Z, b.Z), MathF.Max(a.W, b.W));
                }
                nodes[index] = new PathNode
                {
                    Bounds = bounds,
                    Next = nodeCount,
                    Primitive = count == 1 ? start : -1,
                };
                return bounds;
            }
        }
    }
}
