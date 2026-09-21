using System.Collections.Immutable;

namespace Socotra;

public partial class Styles
{
    private static bool TryParseGradient(GradientType type, string token, out GradientInfo info)
    {
        info = default;
        var p = new Parse(token);
        var start = p.Pointer;
        var prelude = p.ReadSentence();
        var gradient = new GradientInfo { Type = type, CenterX = Length.Percent(50), CenterY = Length.Percent(50), Size = RadialSize.FarthestCorner };
        if (TryParsePrelude(type, prelude, ref gradient))
        {
            p.Pointer++;
        }
        else
        {
            p.Pointer = start;
        }

        if (!TryParseStops(p.ReadRemaining(), out var stops))
        {
            return false;
        }

        info = gradient with { Stops = stops };
        return true;
    }

    private static bool TryParsePrelude(GradientType type, string prelude, ref GradientInfo gradient)
    {
        switch (type)
        {
            case GradientType.Linear when TryParseCorner(prelude, out var corner):
                gradient.Corner = corner;
                return true;
            case GradientType.Linear when TryParseAngle(prelude, out var angle):
                gradient.Angle = angle;
                return true;
            case GradientType.Radial:
                return TryParseRadialPrelude(prelude, ref gradient);
            case GradientType.Conic:
                return TryParseConicPrelude(prelude, ref gradient);
            default:
                return false;
        }
    }

    private static bool TryParseCorner(string text, out GradientCorner corner)
    {
        corner = GradientCorner.None;
        var p = new Parse(text).SkipWhitespaceAndNewlines();
        if (!p.TrySkip("to ", ignoreCase: true))
        {
            return false;
        }

        bool top = false;
        bool bottom = false;
        bool left = false;
        bool right = false;
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            switch (p.ReadWord(null, readUntilEnd: true)!.ToLowerInvariant())
            {
                case "top":
                    top = true;
                    break;
                case "bottom":
                    bottom = true;
                    break;
                case "left":
                    left = true;
                    break;
                case "right":
                    right = true;
                    break;
                default:
                    return false;
            }
        }

        corner = (top, bottom, left, right) switch
        {
            (true, _, true, _) => GradientCorner.TopLeft,
            (true, _, _, true) => GradientCorner.TopRight,
            (_, true, true, _) => GradientCorner.BottomLeft,
            (_, true, _, true) => GradientCorner.BottomRight,
            _ => GradientCorner.None,
        };

        return corner != GradientCorner.None;
    }

    private static bool TryParseAngle(string value, out float radians)
    {
        radians = 0;
        if (GetAngleInDegrees(value) is not { } degrees)
        {
            return false;
        }

        radians = float.DegreesToRadians((((180 - degrees) % 360) + 360) % 360);
        return true;
    }

    private static float? GetAngleInDegrees(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (p.TrySkip("to ", ignoreCase: true))
        {
            p.SkipWhitespaceAndNewlines();
            foreach (var (name, angle) in (ReadOnlySpan<(string, float)>)[("top", 0), ("right", 90), ("bottom", 180), ("left", 270)])
            {
                if (p.Is(name, ignoreCase: true))
                {
                    return angle;
                }
            }
        }

        var start = p.Pointer;
        if (p.TryReadLength(out var length) && length.Unit != LengthUnit.Pixels)
        {
            return length.Value;
        }

        p.Pointer = start;
        if (!p.TryReadFloat(out var number))
        {
            return null;
        }

        return PanelTransform.RotationDegrees(number, p.IsLetter ? p.ReadUntilWhitespaceOrNewlineOrEnd(",") : "deg");
    }

    private static bool TryParseRadialPrelude(string text, ref GradientInfo gradient)
    {
        bool recognized = false;
        var p = new Parse(text);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (p.TrySkip("at", ignoreCase: true))
            {
                if (!TryParsePosition(ref p, ref gradient))
                {
                    return false;
                }

                recognized = true;
                continue;
            }

            switch (p.ReadWord(null, readUntilEnd: true)!.ToLowerInvariant())
            {
                case "circle":
                    gradient.Circle = true;
                    break;
                case "ellipse":
                    gradient.Circle = false;
                    break;
                case "closest-side":
                    gradient.Size = RadialSize.ClosestSide;
                    break;
                case "closest-corner":
                    gradient.Size = RadialSize.ClosestCorner;
                    break;
                case "farthest-side":
                    gradient.Size = RadialSize.FarthestSide;
                    break;
                case "farthest-corner":
                    gradient.Size = RadialSize.FarthestCorner;
                    break;
                default:
                    return false;
            }

            recognized = true;
        }

        return recognized;
    }

    private static bool TryParseConicPrelude(string text, ref GradientInfo gradient)
    {
        bool recognized = false;
        var p = new Parse(text);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (p.TrySkip("from", ignoreCase: true))
            {
                p.SkipWhitespaceAndNewlines();
                if (GetAngleInDegrees(p.ReadWord(null, readUntilEnd: true)!) is not { } degrees)
                {
                    return false;
                }

                gradient.Angle = float.DegreesToRadians(degrees);
            }
            else if (!p.TrySkip("at", ignoreCase: true) || !TryParsePosition(ref p, ref gradient))
            {
                return false;
            }

            recognized = true;
        }

        return recognized;
    }

    private static bool TryParsePosition(ref Parse p, ref GradientInfo gradient)
    {
        bool hasX = false;
        bool hasY = false;
        var loose = new List<Length>();
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            if (p.TrySkip("left", ignoreCase: true))
            {
                gradient.CenterX = Length.Percent(0);
                hasX = true;
            }
            else if (p.TrySkip("right", ignoreCase: true))
            {
                gradient.CenterX = Length.Percent(100);
                hasX = true;
            }
            else if (p.TrySkip("top", ignoreCase: true))
            {
                gradient.CenterY = Length.Percent(0);
                hasY = true;
            }
            else if (p.TrySkip("bottom", ignoreCase: true))
            {
                gradient.CenterY = Length.Percent(100);
                hasY = true;
            }
            else if (p.TrySkip("center", ignoreCase: true))
            {
                loose.Add(Length.Percent(50));
            }
            else if (p.TryReadLength(out var length))
            {
                loose.Add(length);
            }
            else
            {
                return false;
            }
        }

        foreach (var length in loose)
        {
            if (!hasX)
            {
                gradient.CenterX = length;
                hasX = true;
            }
            else if (!hasY)
            {
                gradient.CenterY = length;
                hasY = true;
            }
            else
            {
                return false;
            }
        }

        return hasX || hasY;
    }

    private static bool TryParseStops(string token, out GradientStops stops)
    {
        stops = default;
        var segments = ParseGradientSegments(token);
        if (segments.Count == 0)
        {
            return false;
        }

        var count = Math.Min(segments.Count + 1, GradientInfo.MaxStops);
        stops = stops.Add(segments[0].From);
        foreach (var segment in segments)
        {
            if (stops.Count >= count)
            {
                break;
            }

            stops = stops.Add(segment.To);
        }

        return true;
    }

    private static List<(ColorStop From, ColorStop To)> ParseGradientSegments(string token)
    {
        var segments = new List<(ColorStop From, ColorStop To)>();
        ColorStop? last = null;
        var p = new Parse(token);
        while (!(p = p.SkipWhitespaceAndNewlines()).IsEnd)
        {
            var sentence = p.ReadSentence();
            var stop = new Parse(sentence);
            if (Color.Parse(ref stop) is not { } color)
            {
                Log.Warning($"Can't read a color from '{sentence}'");
                break;
            }

            stop.SkipWhitespaceAndNewlines();
            float? offset = null;
            bool pixels = false;
            if (stop.IsDigit && stop.TryReadFloat(out var position))
            {
                if (stop.TrySkip("%"))
                {
                    offset = position / 100;
                }
                else if (stop.TrySkip("px", ignoreCase: true))
                {
                    offset = position;
                    pixels = true;
                }
                else
                {
                    Log.Warning($"Color stops take a percentage or a pixel length: '{sentence}'");
                    break;
                }
            }

            if (!stop.SkipWhitespaceAndNewlines().IsEnd)
            {
                Log.Warning($"Extra text after a color stop: '{sentence}'");
                break;
            }

            var current = new ColorStop(color, offset, pixels);
            if (last is { } previous)
            {
                segments.Add((previous, current));
            }

            last = current;
            if (!p.TrySkip(","))
            {
                break;
            }
        }

        if (segments.Count == 0)
        {
            var solid = last?.Color ?? Color.Black;
            return [(new ColorStop(solid, 0), new ColorStop(solid, 1))];
        }

        var slice = 1.0f / segments.Count;
        for (int i = 0; i < segments.Count; i++)
        {
            var (start, end) = segments[i];
            segments[i] = (start with { Offset = start.Offset ?? i * slice }, end with { Offset = end.Offset ?? (i + 1) * slice });
        }

        if (segments[^1].To is { Offset: < 1 } final)
        {
            segments.Add((final, new ColorStop(final.Color, 1)));
        }

        return segments;
    }

    private bool SetTextGradient(string value)
    {
        var p = new Parse(value).SkipWhitespaceAndNewlines();
        if (ReadFunction(ref p, "linear-gradient") is { } linear)
        {
            TextGradient = new TextGradientInfo { Type = GradientType.Linear, Angle = 180 };
            var gradient = new Parse(linear).SkipWhitespaceAndNewlines();
            var probe = gradient;
            if (!probe.TryReadColor(out _))
            {
                var angle = gradient.ReadUntilOrEnd(",", respectParens: true);
                gradient.SkipWhitespaceAndNewlines(",");
                if (GetAngleInDegrees(angle) is { } degrees)
                {
                    TextGradient.Angle = degrees;
                }
            }

            TextGradient.Stops = TextStops(gradient.ReadRemaining());
            return true;
        }

        if (ReadFunction(ref p, "radial-gradient") is { } radial)
        {
            TextGradient = new TextGradientInfo
            {
                Type = GradientType.Radial,
                CenterX = Length.Percent(50),
                CenterY = Length.Percent(50),
                Size = RadialSize.FarthestSide,
            };

            var gradient = new Parse(radial).SkipWhitespaceAndNewlines();
            var probe = gradient;
            if (!probe.TryReadColor(out _))
            {
                var size = gradient.ReadUntilOrEnd(", ", respectParens: true);
                gradient.SkipWhitespaceAndNewlines();
                var position = gradient.ReadUntilOrEnd(",", respectParens: true);
                gradient.SkipWhitespaceAndNewlines(",");
                SetTextGradientSize(size);
                SetTextGradientPosition(position);
            }

            TextGradient.Stops = TextStops(gradient.ReadRemaining());
            return true;
        }

        return false;
    }

    private void SetTextGradientSize(string value)
    {
        TextGradient.Size = value switch
        {
            "circle" => RadialSize.Circle,
            "closest-corner" => RadialSize.ClosestCorner,
            "closest-side" => RadialSize.ClosestSide,
            "farthest-corner" => RadialSize.FarthestCorner,
            "farthest-side" => RadialSize.FarthestSide,
            _ => TextGradient.Size,
        };
    }

    private void SetTextGradientPosition(string value)
    {
        var p = new Parse(value);
        p.TrySkip("at ");
        if (p.TryReadLength(out var x))
        {
            TextGradient.CenterX = x;
            TextGradient.CenterY = p.TryReadLength(out var y) ? y : x;
        }
    }

    private static ImmutableArray<ColorStop> TextStops(string token) =>
        [.. ParseGradientSegments(token).SelectMany(segment => new[] { segment.From, segment.To })];

    private Texture BakeGradient(GradientType type, string token, out float angle)
    {
        angle = 0;
        var p = new Parse(token);
        var start = p.Pointer;
        var prelude = p.ReadSentence();
        var gradient = default(GradientInfo);
        if (type == GradientType.Linear ? TryParseAngle(prelude, out angle) : TryParsePrelude(type, prelude, ref gradient))
        {
            p.Pointer++;
        }
        else
        {
            p.Pointer = start;
        }

        var width = OptimalGradientWidth();
        var ramp = BakeRamp(p.ReadRemaining(), width);
        if (type == GradientType.Linear)
        {
            return Texture.FromPixels(1, width, ramp);
        }

        var pixels = new byte[width * width * 4];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < width; y++)
            {
                var position = (new Vector2(x, y) / width) - new Vector2(0.5f);
                var sample = type == GradientType.Radial
                    ? (int)(position.Length() * width)
                    : width - (int)(((MathF.Atan2(position.Y, position.X) + MathF.PI) / MathF.Tau) * width);
                ramp.AsSpan(Math.Clamp(sample, 0, width - 1) * 4, 4).CopyTo(pixels.AsSpan(((x * width) + y) * 4));
            }
        }

        return Texture.FromPixels(width, width, pixels);
    }

    private int OptimalGradientWidth()
    {
        var width = _backgroundSizeX?.GetPixels(1) ?? _width?.GetPixels(1) ?? 0;
        var height = _backgroundSizeY?.GetPixels(1) ?? _height?.GetPixels(1) ?? 0;
        return Math.Clamp((int)MathF.Max(width, height), 256, 2048);
    }

    private static byte[] BakeRamp(string token, int width)
    {
        var ramp = new byte[width * 4];
        foreach (var (start, end) in ParseGradientSegments(token))
        {
            var startPixel = (int)(StopFraction(start, width) * width);
            var endPixel = (int)(StopFraction(end, width) * width);
            for (int i = Math.Max(startPixel, 0); i < Math.Min(endPixel, width); i++)
            {
                var color = LerpPremultiplied(start.Color, end.Color, (float)(i - startPixel) / (endPixel - startPixel));
                ramp[(i * 4) + 0] = ToByte(color.R);
                ramp[(i * 4) + 1] = ToByte(color.G);
                ramp[(i * 4) + 2] = ToByte(color.B);
                ramp[(i * 4) + 3] = ToByte(color.A);
            }
        }

        return ramp;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value * 255), 0, 255);

    private static float StopFraction(ColorStop stop, int width) =>
        stop.OffsetIsPixels ? Math.Clamp((stop.Offset ?? 0) / Math.Max(width, 1), 0, 1) : stop.Offset ?? 0;

    private static Color LerpPremultiplied(Color from, Color to, float t)
    {
        var a = from.A + (t * (to.A - from.A));
        var r = (from.R * from.A) + (t * ((to.R * to.A) - (from.R * from.A)));
        var g = (from.G * from.A) + (t * ((to.G * to.A) - (from.G * from.A)));
        var b = (from.B * from.A) + (t * ((to.B * to.A) - (from.B * from.A)));
        return a > 0.0001f ? new Color(r / a, g / a, b / a, a) : new Color(r, g, b, a);
    }
}
