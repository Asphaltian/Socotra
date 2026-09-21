namespace Socotra;

public readonly partial struct Fill
{
    /// <summary>
    /// Starts a gradient across the shape, heading <paramref name="angle"/> degrees clockwise from the right. Add stops
    /// with <see cref="GradientBuilder.WithStop"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // left half red, right half blue, with a hard edge between them
    /// painter.Fill = Fill.LinearGradient()
    ///     .WithStop(0, new Color(1, 0, 0))
    ///     .WithStop(0.5f, new Color(1, 0, 0))
    ///     .WithStop(0.5f, new Color(0, 0, 1))
    ///     .WithStop(1, new Color(0, 0, 1));
    /// </code>
    /// </example>
    public static GradientBuilder LinearGradient(float angle = 0) => new(GradientType.Linear, angle);

    /// <summary>
    /// Starts a gradient from the middle of the shape out to its sides. Add stops with <see cref="GradientBuilder.WithStop"/>.
    /// </summary>
    public static GradientBuilder RadialGradient() => new(GradientType.Radial, 0);

    /// <summary>
    /// Starts a gradient that sweeps clockwise around the middle of the shape, beginning <paramref name="angle"/> degrees
    /// clockwise from the right. Add stops with <see cref="GradientBuilder.WithStop"/>.
    /// </summary>
    public static GradientBuilder ConicGradient(float angle = 0) => new(GradientType.Conic, angle);

    /// <summary>
    /// A gradient you build one stop at a time. Add 2 to 8 stops, then use it as a <see cref="Fill"/>.
    /// </summary>
    public readonly struct GradientBuilder
    {
        readonly GradientInfo _gradient;

        internal GradientBuilder(GradientType type, float angle)
        {
            _gradient = CreateGradientInfo(type, angle);
        }

        GradientBuilder(GradientInfo gradient) => _gradient = gradient;

        /// <summary>
        /// A copy with <paramref name="color"/> at <paramref name="offset"/>, from 0 (start) to 1 (end). Add stops in order;
        /// two at the same offset make a hard edge. Throws if an offset is smaller than the one before it.
        /// </summary>
        public GradientBuilder WithStop(float offset, Color color)
        {
            var previous = _gradient.Stops.Count == 0 ? 0 : _gradient.Stops[_gradient.Stops.Count - 1].Offset!.Value;
            ValidateStop(offset, color, previous);
            return new(_gradient with { Stops = _gradient.Stops.Add(new ColorStop(color, offset)) });
        }

        /// <summary>
        /// A copy pointing <paramref name="degrees"/> clockwise from the right. Throws on a radial gradient.
        /// </summary>
        public GradientBuilder WithAngle(float degrees) => new(WithGradientAngle(_gradient, degrees));

        /// <summary>Lets you use the gradient as a fill. Throws if it has fewer than two stops.</summary>
        public static implicit operator Fill(GradientBuilder builder)
        {
            if (builder._gradient.Stops.Count < 2)
            {
                throw new InvalidOperationException("Add at least two stops before using the gradient as a fill.");
            }

            return new Fill(builder._gradient);
        }
    }

    /// <summary>
    /// A copy of this linear or conic gradient pointing <paramref name="degrees"/> clockwise from the right. Throws on a
    /// radial gradient, one made from points, or a fill that isn't a gradient.
    /// </summary>
    public Fill WithAngle(float degrees)
    {
        if (_gradient.IsEmpty || _gradientCoordinates is not null)
        {
            throw new InvalidOperationException("WithAngle requires a bounds-based linear or conic gradient.");
        }

        return new Fill(this, WithGradientAngle(_gradient, degrees));
    }

    Fill(Fill source, GradientInfo gradient)
    {
        this = source;
        _gradient = gradient;
    }

    static GradientInfo WithGradientAngle(GradientInfo gradient, float degrees)
    {
        if (gradient.Type == GradientType.Radial)
        {
            throw new InvalidOperationException("Radial gradients have no angle.");
        }

        return gradient with { Angle = GradientAngle(gradient.Type, degrees), Corner = GradientCorner.None };
    }

    static float GradientAngle(GradientType type, float angle)
    {
        if (!float.IsFinite(angle))
        {
            throw new ArgumentOutOfRangeException(nameof(angle));
        }

        return (type == GradientType.Linear ? 90 - angle % 360 : angle % 360 + 90) * (MathF.PI / 180);
    }

    static GradientInfo CreateGradientInfo(GradientType type, float angle) => new()
    {
        Type = type,
        Angle = GradientAngle(type, angle),
        CenterX = Length.Percent(50),
        CenterY = Length.Percent(50),
        Size = RadialSize.FarthestSide
    };

    static void ValidateStop(float offset, Color color, float previous)
    {
        if (!float.IsFinite(offset) || offset < previous || offset > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Stop offsets must be finite, ordered and in [0, 1].");
        }

        if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B) || !float.IsFinite(color.A))
        {
            throw new ArgumentOutOfRangeException(nameof(color), "Stop colors must be finite.");
        }
    }

    /// <summary>
    /// One color in a gradient and where it sits. Pass a list of these to the gradient methods.
    /// </summary>
    /// <example>
    /// <code>
    /// painter.Fill = Fill.LinearGradient([new(0, Color.White), new(0.8f, Color.Black), new(1, Color.Transparent)], 90);
    /// </code>
    /// </example>
    public readonly record struct GradientStop
    {
        /// <summary>
        /// Puts <paramref name="color"/> at <paramref name="offset"/>, from 0 (start) to 1 (end).
        /// </summary>
        public GradientStop(float offset, Color color)
        {
            Offset = offset;
            Color = color;
        }

        /// <summary>
        /// Where the color sits, from 0 (start) to 1 (end). Stops must be in order.
        /// </summary>
        public float Offset { get; init; }

        /// <summary>
        /// The color at this stop.
        /// </summary>
        public Color Color { get; init; }
    }

    /// <summary>
    /// Fades from <paramref name="start"/> to <paramref name="end"/> across the shape, heading <paramref name="angle"/>
    /// degrees clockwise from the right: 0 runs left to right, 90 top to bottom.
    /// </summary>
    public static Fill LinearGradient(Color start, Color end, float angle = 0)
    {
        return LinearGradient([new(0, start), new(1, end)], angle);
    }

    /// <summary>
    /// A gradient through <paramref name="stops"/> across the shape, heading <paramref name="angle"/> degrees clockwise
    /// from the right: 0 runs left to right, 90 top to bottom.
    /// </summary>
    public static Fill LinearGradient(ReadOnlySpan<GradientStop> stops, float angle = 0)
    {
        return CreateGradient(stops, GradientType.Linear, angle);
    }

    /// <summary>
    /// Fades from <paramref name="startColor"/> at <paramref name="start"/> to <paramref name="endColor"/> at
    /// <paramref name="end"/>. The end colors carry on past the two points.
    /// </summary>
    public static Fill LinearGradient(Vector2 start, Vector2 end, Color startColor, Color endColor)
    {
        return LinearGradient(start, end, [new(0, startColor), new(1, endColor)]);
    }

    /// <summary>
    /// A gradient through <paramref name="stops"/> from <paramref name="start"/> to <paramref name="end"/>. The end
    /// colors carry on past the two points.
    /// </summary>
    public static Fill LinearGradient(Vector2 start, Vector2 end, ReadOnlySpan<GradientStop> stops)
    {
        return LinearGradient(start.X, start.Y, end.X, end.Y, stops);
    }

    /// <summary>
    /// Fades from <paramref name="startColor"/> to <paramref name="endColor"/> between two points given in pixels or
    /// percentages of the shape, like <c>Length.Percent(50)</c>. The end colors carry on past the points.
    /// </summary>
    public static Fill LinearGradient(Length? startX, Length? startY, Length? endX, Length? endY, Color startColor, Color endColor)
    {
        return LinearGradient(startX, startY, endX, endY, [new(0, startColor), new(1, endColor)]);
    }

    /// <summary>
    /// A gradient through <paramref name="stops"/> between two points given in pixels or percentages of the shape.
    /// The end colors carry on past the points.
    /// </summary>
    public static Fill LinearGradient(Length? startX, Length? startY, Length? endX, Length? endY, ReadOnlySpan<GradientStop> stops)
    {
        return CreateGradient(stops, GradientType.Linear, 0, new GradientCoordinates(startX, startY, endX, endY));
    }

    /// <summary>
    /// Fades from <paramref name="center"/> in the middle of the shape to <paramref name="edge"/> at its sides.
    /// </summary>
    public static Fill RadialGradient(Color center, Color edge)
    {
        return RadialGradient([new(0, center), new(1, edge)]);
    }

    /// <summary>
    /// A gradient through <paramref name="stops"/> from the middle of the shape out to its sides.
    /// </summary>
    public static Fill RadialGradient(ReadOnlySpan<GradientStop> stops)
    {
        return CreateGradient(stops, GradientType.Radial, 0);
    }

    /// <summary>
    /// A round gradient from <paramref name="start"/> at <paramref name="center"/> to <paramref name="end"/> at
    /// <paramref name="edge"/>. The last color carries on past the edge.
    /// </summary>
    public static Fill RadialGradient(Vector2 center, Vector2 edge, Color start, Color end)
    {
        return RadialGradient(center, edge, [new(0, start), new(1, end)]);
    }

    /// <summary>
    /// A round gradient through <paramref name="stops"/> from <paramref name="center"/> out to <paramref name="edge"/>.
    /// The last color carries on past the edge.
    /// </summary>
    public static Fill RadialGradient(Vector2 center, Vector2 edge, ReadOnlySpan<GradientStop> stops)
    {
        return RadialGradient(center.X, center.Y, edge.X, edge.Y, stops);
    }

    /// <summary>
    /// A round gradient from <paramref name="start"/> to <paramref name="end"/>, with the center and a point on the edge
    /// given in pixels or percentages of the shape. The last color carries on past the edge.
    /// </summary>
    public static Fill RadialGradient(Length? centerX, Length? centerY, Length? edgeX, Length? edgeY, Color start, Color end)
    {
        return RadialGradient(centerX, centerY, edgeX, edgeY, [new(0, start), new(1, end)]);
    }

    /// <summary>
    /// A round gradient through <paramref name="stops"/>, with the center and a point on the edge given in pixels or
    /// percentages of the shape. The last color carries on past the edge.
    /// </summary>
    public static Fill RadialGradient(Length? centerX, Length? centerY, Length? edgeX, Length? edgeY, ReadOnlySpan<GradientStop> stops)
    {
        return CreateGradient(stops, GradientType.Radial, 0, new GradientCoordinates(centerX, centerY, edgeX, edgeY));
    }

    /// <summary>
    /// Sweeps from <paramref name="start"/> to <paramref name="end"/> clockwise around the middle of the shape, beginning
    /// <paramref name="angle"/> degrees clockwise from the right.
    /// </summary>
    public static Fill ConicGradient(Color start, Color end, float angle = 0)
    {
        return ConicGradient([new(0, start), new(1, end)], angle);
    }

    /// <summary>
    /// Sweeps through <paramref name="stops"/> clockwise around the middle of the shape, beginning
    /// <paramref name="angle"/> degrees clockwise from the right.
    /// </summary>
    public static Fill ConicGradient(ReadOnlySpan<GradientStop> stops, float angle = 0)
    {
        return CreateGradient(stops, GradientType.Conic, angle);
    }

    /// <summary>
    /// Sweeps from <paramref name="startColor"/> to <paramref name="endColor"/> clockwise around <paramref name="center"/>,
    /// beginning in the direction of <paramref name="start"/>.
    /// </summary>
    public static Fill ConicGradient(Vector2 center, Vector2 start, Color startColor, Color endColor)
    {
        return ConicGradient(center, start, [new(0, startColor), new(1, endColor)]);
    }

    /// <summary>
    /// Sweeps through <paramref name="stops"/> clockwise around <paramref name="center"/>, beginning in the direction of
    /// <paramref name="start"/>. The stops cover one full turn.
    /// </summary>
    public static Fill ConicGradient(Vector2 center, Vector2 start, ReadOnlySpan<GradientStop> stops)
    {
        return ConicGradient(center.X, center.Y, start.X, start.Y, stops);
    }

    /// <summary>
    /// Sweeps from <paramref name="startColor"/> to <paramref name="endColor"/> clockwise around a center, beginning in
    /// the direction of a start point. Both points are in pixels or percentages of the shape.
    /// </summary>
    public static Fill ConicGradient(Length? centerX, Length? centerY, Length? startX, Length? startY, Color startColor, Color endColor)
    {
        return ConicGradient(centerX, centerY, startX, startY, [new(0, startColor), new(1, endColor)]);
    }

    /// <summary>
    /// Sweeps through <paramref name="stops"/> clockwise around a center, beginning in the direction of a start point. Both
    /// points are in pixels or percentages of the shape, and the stops cover one full turn.
    /// </summary>
    public static Fill ConicGradient(Length? centerX, Length? centerY, Length? startX, Length? startY, ReadOnlySpan<GradientStop> stops)
    {
        return CreateGradient(stops, GradientType.Conic, 0, new GradientCoordinates(centerX, centerY, startX, startY));
    }

    static Fill CreateGradient(ReadOnlySpan<GradientStop> stops, GradientType type, float angle, GradientCoordinates? coordinates = null)
    {
        var gradient = CreateGradientInfo(type, angle);
        if (stops.Length < 2 || stops.Length > GradientInfo.MaxStops)
        {
            throw new ArgumentOutOfRangeException(nameof(stops), "Gradients require 2 to 8 stops.");
        }

        float previous = 0;
        foreach (var stop in stops)
        {
            ValidateStop(stop.Offset, stop.Color, previous);
            previous = stop.Offset;
            gradient.Stops = gradient.Stops.Add(new ColorStop(stop.Color, stop.Offset));
        }

        gradient.Circle = type == GradientType.Radial && coordinates is not null;
        return new Fill(gradient, coordinates);
    }

    sealed class GradientCoordinates
    {
        readonly Length _x, _y, _endX, _endY;

        public GradientCoordinates(Length? x, Length? y, Length? endX, Length? endY)
        {
            _x = Validate(x, nameof(x));
            _y = Validate(y, nameof(y));
            _endX = Validate(endX, nameof(endX));
            _endY = Validate(endY, nameof(endY));
            if (_x.Equals(_endX) && _y.Equals(_endY))
            {
                throw new ArgumentOutOfRangeException(nameof(endX), "Gradient points must be distinct.");
            }

            if (_x.Unit == LengthUnit.Pixels && _y.Unit == LengthUnit.Pixels && _endX.Unit == LengthUnit.Pixels && _endY.Unit == LengthUnit.Pixels)
            {
                GetPoints(default, out _, out _, out _);
            }
        }

        static Length Validate(Length? value, string parameter)
        {
            if (value is not Length length || !float.IsFinite(length.Value)
                || !(length.Unit is LengthUnit.Pixels or LengthUnit.Percentage || length.Unit.IsDynamic()))
            {
                throw new ArgumentOutOfRangeException(parameter, "Expected a finite coordinate length.");
            }

            return length;
        }

        static float Coordinate(Length length, float origin, float size)
            => length.GetPixels(size) + (length.Unit is LengthUnit.Percentage or LengthUnit.Expression ? origin : 0);

        void GetPoints(Rect bounds, out Vector2 start, out Vector2 delta, out float distance)
        {
            start = new Vector2(Coordinate(_x, bounds.Left, bounds.Width), Coordinate(_y, bounds.Top, bounds.Height));
            var end = new Vector2(Coordinate(_endX, bounds.Left, bounds.Width), Coordinate(_endY, bounds.Top, bounds.Height));
            delta = end - start;
            distance = delta.Length();
            if (!Painter.IsFinite(start) || !Painter.IsFinite(end) || !float.IsFinite(distance * 2) || distance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "Gradient points must resolve to finite, distinct positions.");
            }
        }

        public void Resolve(Rect bounds, ref GradientInfo gradient, out Vector4 backgroundRect)
        {
            GetPoints(bounds, out var start, out var delta, out var distance);
            if (gradient.Type == GradientType.Conic)
            {
                var offset = start - bounds.Position;
                if (!Painter.IsFinite(offset))
                {
                    throw new ArgumentOutOfRangeException(nameof(bounds), "Gradient center must resolve to finite coordinates.");
                }

                gradient.CenterX = offset.X;
                gradient.CenterY = offset.Y;
                gradient.Angle = MathF.Atan2(delta.X, -delta.Y);
                backgroundRect = new Vector4(0, 0, bounds.Width, bounds.Height);
                return;
            }

            float size = distance * 2;
            var center = start;
            if (gradient.Type == GradientType.Linear)
            {
                var direction = delta / distance;
                size = distance / (MathF.Abs(direction.X) + MathF.Abs(direction.Y));
                center += delta * 0.5f;
                gradient.Angle = MathF.Atan2(delta.X, delta.Y);
            }
            var position = center - new Vector2(size * 0.5f) - bounds.Position;
            if (!Painter.IsFinite(position) || !float.IsFinite(position.X / size) || !float.IsFinite(position.Y / size))
            {
                throw new ArgumentOutOfRangeException(nameof(bounds), "Gradient bounds must resolve to finite coordinates.");
            }

            backgroundRect = new Vector4(position.X, position.Y, size, size);
        }
    }
}
