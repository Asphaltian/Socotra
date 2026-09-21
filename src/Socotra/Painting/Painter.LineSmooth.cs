using System.Buffers;
using System.Runtime.InteropServices;

namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Draws a smooth curve through every one of <paramref name="points"/> with <see cref="Stroke"/>, like a line graph
    /// with rounded bends. The curve can bulge a little past the points.
    /// </summary>
    public void LineSmooth(ReadOnlySpan<Vector2> points)
    {
        if (!HasStroke(Stroke) || points.Length < 2)
        {
            return;
        }

        var rented = ArrayPool<Vector2>.Shared.Rent(points.Length);
        try
        {
            var clean = rented.AsSpan(0, points.Length);
            int count = 0;
            foreach (var point in points)
            {
                if (!IsFinite(point))
                {
                    return;
                }

                if (count == 0 || point != clean[count - 1])
                {
                    clean[count++] = point;
                }
            }
            if (count < 2)
            {
                return;
            }

            if (count == 2)
            {
                Line(clean[..count]);
                return;
            }

            var curve = new List<Vector2> { clean[0] };
            for (int i = 0; i < count - 1; i++)
            {
                var from = clean[i];
                var to = clean[i + 1];
                var interval = SmoothInterval(from, to);
                var control1 = i == 0 ? from * (2f / 3f) + to * (1f / 3f)
                    : from + SmoothTangent(clean[i - 1], from, to, interval / 3);
                var control2 = i + 2 == count ? from * (1f / 3f) + to * (2f / 3f)
                    : to - SmoothTangent(from, to, clean[i + 2], interval / 3);
                if (!IsFinite(control1) || !IsFinite(control2))
                {
                    return;
                }

                FlattenBezier(curve, from, control1, control2, to);
            }
            Line(CollectionsMarshal.AsSpan(curve));
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(rented);
        }
    }

    static double SmoothInterval(Vector2 from, Vector2 to)
    {
        double x = (double)to.X - from.X, y = (double)to.Y - from.Y;
        return Math.Sqrt(Math.Sqrt(x * x + y * y));
    }

    static Vector2 SmoothTangent(Vector2 before, Vector2 point, Vector2 after, double scale)
    {
        var previous = SmoothInterval(before, point);
        var next = SmoothInterval(point, after);
        var incoming = next * scale / (previous * (previous + next));
        var outgoing = previous * scale / (next * (previous + next));
        return new Vector2(
            (float)(((double)point.X - before.X) * incoming + ((double)after.X - point.X) * outgoing),
            (float)(((double)point.Y - before.Y) * incoming + ((double)after.Y - point.Y) * outgoing));
    }
}
