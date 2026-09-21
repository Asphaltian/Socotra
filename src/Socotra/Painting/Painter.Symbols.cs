using System.Buffers;

namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Draws a pill shape from <paramref name="from"/> to <paramref name="to"/> with <see cref="Fill"/> and
    /// <see cref="Stroke"/>. Give the ends different radii to make it taper.
    /// </summary>
    public void Capsule(Vector2 from, Vector2 to, float fromRadius, float toRadius)
    {
        if (!IsFinite(from) || !IsFinite(to) || !float.IsFinite(fromRadius) || !float.IsFinite(toRadius)
            || fromRadius < 0 || toRadius < 0)
        {
            return;
        }

        var delta = to - from;
        float distance = delta.Length();
        if (!float.IsFinite(distance))
        {
            return;
        }

        if (distance <= MathF.Abs(fromRadius - toRadius))
        {
            Circle(fromRadius >= toRadius ? from : to, MathF.Max(fromRadius, toRadius));
            return;
        }
        if (fromRadius == 0 && toRadius == 0)
        {
            return;
        }

        var min = Vector2.Min(from - new Vector2(fromRadius), to - new Vector2(toRadius));
        var max = Vector2.Max(from + new Vector2(fromRadius), to + new Vector2(toRadius));
        var bounds = new Rect(min, max - min);
        SdfFill(bounds, new()
        {
            Kind = ShapeKind.Capsule,
            Circle = new Vector4(from.X - min.X, from.Y - min.Y, fromRadius, toRadius),
            Polygon01 = new Vector4(to.X - min.X, to.Y - min.Y, 0, 0)
        });
        if (!HasStroke(Stroke))
        {
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X) * 180 / MathF.PI;
        float tangent = MathF.Acos(Math.Clamp((fromRadius - toRadius) / distance, -1, 1)) * 180 / MathF.PI;
        StrokeArcPair(from, fromRadius, angle + tangent, 360 - 2 * tangent,
            to, toRadius, angle - tangent, 2 * tangent);
    }

    /// <summary>
    /// Draws a crescent moon with <see cref="Fill"/> and <see cref="Stroke"/>: a circle with a second circle cut out of it.
    /// Move the cutout with <paramref name="cutoutOffset"/> to change how thin the crescent is and which way it faces.
    /// </summary>
    public void Crescent(Vector2 center, float radius, float cutoutRadius, Vector2 cutoutOffset)
    {
        if (!IsFinite(center) || !IsFinite(cutoutOffset) || !float.IsFinite(radius) || radius <= 0
            || !float.IsFinite(cutoutRadius) || cutoutRadius < 0)
        {
            return;
        }

        float distance = cutoutOffset.Length();
        if (!float.IsFinite(distance))
        {
            return;
        }

        if (distance + radius <= cutoutRadius)
        {
            return;
        }

        if (cutoutRadius == 0 || distance >= radius + cutoutRadius) { Circle(center, radius); return; }
        var cutout = center + cutoutOffset;
        var bounds = new Rect(center - new Vector2(radius), new Vector2(radius * 2));
        var shape = new ShapeInstance
        {
            Kind = ShapeKind.Crescent,
            Circle = new Vector4(radius, radius, radius, cutoutRadius),
            Polygon01 = new Vector4(cutout.X - bounds.Left, cutout.Y - bounds.Top, 0, 0)
        };
        SdfFill(bounds, shape);
        if (!HasStroke(Stroke))
        {
            return;
        }

        if (distance + cutoutRadius <= radius)
        {
            shape.Circle.X = center.X; shape.Circle.Y = center.Y;
            shape.Polygon01 = new Vector4(cutout.X, cutout.Y, 0, 0);
            var mask = new Path.Data(shape, []);
            Path.DrawArc(ActiveContext, center, radius, 0, 360, Stroke, mask);
            Path.DrawArc(ActiveContext, cutout, cutoutRadius, 0, 360, Stroke, mask);
            return;
        }
        float direction = MathF.Atan2(cutoutOffset.Y, cutoutOffset.X) * 180 / MathF.PI;
        float outerAngle = (float)(Math.Acos(Math.Clamp(((double)radius * radius + (double)distance * distance - (double)cutoutRadius * cutoutRadius) / (2.0 * radius * distance), -1, 1)) * 180 / Math.PI);
        float innerAngle = (float)(Math.Acos(Math.Clamp(((double)radius * radius - (double)distance * distance - (double)cutoutRadius * cutoutRadius) / (2.0 * cutoutRadius * distance), -1, 1)) * 180 / Math.PI);
        StrokeArcPair(center, radius, direction + outerAngle, 360 - 2 * outerAngle,
            cutout, cutoutRadius, direction - innerAngle, -(360 - 2 * innerAngle), sharedEndpoints: true);
    }

    /// <summary>
    /// Draws a heart <paramref name="size"/> wide, centered on <paramref name="center"/>, with <see cref="Fill"/> and
    /// <see cref="Stroke"/>.
    /// </summary>
    public void Heart(Vector2 center, float size)
    {
        if (!IsFinite(center) || !float.IsFinite(size) || size <= 0)
        {
            return;
        }

        const float lobeRadius = 0.35355339f, lobeOffset = 0.25f, tip = 0.5f;
        float scale = size / (2 * lobeOffset + 2 * lobeRadius);
        var origin = center - new Vector2(0, (tip - lobeOffset - lobeRadius) * 0.5f * scale);
        float height = (lobeOffset + lobeRadius + tip) * scale;
        var bounds = new Rect(center - new Vector2(size, height) * 0.5f, new Vector2(size, height));
        SdfFill(bounds, new()
        {
            Kind = ShapeKind.Heart,
            Circle = new Vector4(origin.X - bounds.Left, origin.Y - bounds.Top, scale, lobeRadius),
            Polygon01 = new Vector4(lobeOffset, lobeOffset, tip, 0)
        });
        if (!HasStroke(Stroke))
        {
            return;
        }

        int count = CurveSegments(lobeRadius * scale, 180) + 1;
        var rented = ArrayPool<Vector2>.Shared.Rent(count * 2 + 1);
        try
        {
            var points = rented.AsSpan(0, count * 2 + 1);
            SampleArc(points[..count], origin + new Vector2(lobeOffset, -lobeOffset) * scale, new Vector2(lobeRadius * scale), 225, 180);
            points[count] = origin + new Vector2(0, tip) * scale;
            SampleArc(points[(count + 1)..], origin + new Vector2(-lobeOffset, -lobeOffset) * scale, new Vector2(lobeRadius * scale), 135, 180);
            points[^1] = points[0];
            Path.DrawPolyline(ActiveContext, points, Stroke, closed: true);
        }
        finally { ArrayPool<Vector2>.Shared.Return(rented); }
    }

    void SdfFill(Rect bounds, ShapeInstance shape)
    {
        if (Fill.IsTransparent || !ValidBounds(bounds))
        {
            return;
        }

        var desc = Fill.CreateDescriptor(bounds, ActiveContext);
        desc.BorderShapeData = shape;
        Add(ActiveContext, desc);
    }

    void StrokeArcPair(Vector2 a, float ra, float startA, float sweepA, Vector2 b, float rb, float startB, float sweepB, bool sharedEndpoints = false)
    {
        int countA = CurveSegments(ra, sweepA) + 1;
        int countB = CurveSegments(rb, sweepB) + 1;
        var rented = ArrayPool<Vector2>.Shared.Rent(countA + countB);
        try
        {
            var points = rented.AsSpan(0, countA + countB);
            SampleArc(points[..countA], a, new Vector2(ra), startA, sweepA);
            SampleArc(points[countA..], b, new Vector2(rb), startB, sweepB);
            if (sharedEndpoints) { points[countA] = points[countA - 1]; points[^1] = points[0]; }
            Path.DrawPolyline(ActiveContext, points, Stroke, closed: true);
        }
        finally { ArrayPool<Vector2>.Shared.Return(rented); }
    }
}
