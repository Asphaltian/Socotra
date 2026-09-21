namespace Socotra;

/// <summary>
/// Easing functions for transitions and animations. Stylesheets pick them by name, like <c>ease-out</c>,
/// <c>bounce-in</c>, <c>cubic-bezier(0.4, 0, 0.2, 1)</c> or <c>steps(4, end)</c>, and you can call them yourself too.
/// </summary>
/// <example><code>
/// // Ease your own 0 to 1 progress the same way a stylesheet would
/// var ease = Easing.GetFunction("ease-out");
/// var eased = ease(0.25f);
/// </code></example>
public static class Easing
{
    private static readonly Dictionary<string, Function> Functions = new()
    {
        ["linear"] = Linear,
        ["ease"] = QuadraticInOut,
        ["ease-in-out"] = ExpoInOut,
        ["ease-out"] = QuadraticOut,
        ["ease-in"] = QuadraticIn,
        ["bounce-in"] = BounceIn,
        ["bounce-out"] = BounceOut,
        ["bounce-in-out"] = BounceInOut,
        ["sin-ease-in"] = SineEaseIn,
        ["sin-ease-out"] = SineEaseOut,
        ["sin-ease-in-out"] = SineEaseInOut,
        ["step-start"] = StepStart,
        ["step-end"] = StepEnd,
    };

    /// <summary>Takes progress from 0 to 1 and gives it back eased.</summary>
    public delegate float Function(float delta);

    /// <summary><c>ease-in-out</c>, the same as <see cref="ExpoInOut"/>.</summary>
    public static float EaseInOut(float f) => ExpoInOut(f);

    /// <summary><c>ease-in</c>, the same as <see cref="QuadraticIn"/>.</summary>
    public static float EaseIn(float f) => QuadraticIn(f);

    /// <summary><c>ease-out</c>, the same as <see cref="QuadraticOut"/>.</summary>
    public static float EaseOut(float f) => QuadraticOut(f);

    /// <summary><c>linear</c>: no easing.</summary>
    public static float Linear(float f) => f;

    /// <summary>Starts slow and speeds up.</summary>
    public static float QuadraticIn(float f) => f * f;

    /// <summary>Starts fast and slows down.</summary>
    public static float QuadraticOut(float f) => f * (2.0f - f);

    /// <summary><c>ease</c>: starts slow, speeds up, then slows down.</summary>
    public static float QuadraticInOut(float f)
    {
        f *= 2.0f;
        if (f < 1.0f)
        {
            return 0.5f * f * f;
        }

        f -= 1.0f;
        return -0.5f * ((f * (f - 2.0f)) - 1.0f);
    }

    /// <summary>Starts very slow and speeds up sharply.</summary>
    public static float ExpoIn(float f) => f == 0 ? 0 : MathF.Pow(1024, f - 1);

    /// <summary>Starts very fast and slows down sharply.</summary>
    public static float ExpoOut(float f) => f == 1 ? 1 : 1 - MathF.Pow(2, -10 * f);

    /// <summary><c>ease-in-out</c>: <see cref="ExpoIn"/> for the first half and <see cref="ExpoOut"/> for the second.</summary>
    public static float ExpoInOut(float f) => f < 0.5f ? ExpoIn(f * 2.0f) * 0.5f : (ExpoOut((f - 0.5f) * 2.0f) * 0.5f) + 0.5f;

    /// <summary><c>bounce-in</c>: bounces before leaving the start.</summary>
    public static float BounceIn(float f) => 1 - BounceOut(1 - f);

    /// <summary><c>bounce-out</c>: bounces when it reaches the end.</summary>
    public static float BounceOut(float f)
    {
        if (f < 1 / 2.75f)
        {
            return 7.5625f * f * f;
        }

        if (f < 2 / 2.75f)
        {
            f -= 1.5f / 2.75f;
            return (7.5625f * f * f) + 0.75f;
        }

        if (f < 2.5f / 2.75f)
        {
            f -= 2.25f / 2.75f;
            return (7.5625f * f * f) + 0.9375f;
        }

        f -= 2.625f / 2.75f;
        return (7.5625f * f * f) + 0.984375f;
    }

    /// <summary><c>bounce-in-out</c>: bounces at both ends.</summary>
    public static float BounceInOut(float f) => f < 0.5f ? BounceIn(f * 2.0f) * 0.5f : (BounceOut((f - 0.5f) * 2.0f) * 0.5f) + 0.5f;

    /// <summary><c>sin-ease-in</c>: starts slow along a sine curve.</summary>
    public static float SineEaseIn(float f) => 1.0f - MathF.Cos(f * MathF.PI * 0.5f);

    /// <summary><c>sin-ease-out</c>: slows down along a sine curve.</summary>
    public static float SineEaseOut(float f) => MathF.Sin(f * MathF.PI * 0.5f);

    /// <summary><c>sin-ease-in-out</c>: slow at both ends along a sine curve.</summary>
    public static float SineEaseInOut(float f) => -(MathF.Cos(MathF.PI * f) - 1.0f) * 0.5f;

    /// <summary><c>step-start</c>: jumps to the end as soon as it starts.</summary>
    public static float StepStart(float f) => f > 0 ? 1 : 0;

    /// <summary><c>step-end</c>: stays at the start until the very end.</summary>
    public static float StepEnd(float f) => f < 1 ? 0 : 1;

    /// <summary><c>steps(count, start|end)</c>: jumps in <paramref name="count"/> equal steps. Pass <paramref name="atStart"/> as true to jump at the start of each step instead of the end.</summary>
    public static Function Steps(int count, bool atStart = false)
    {
        count = Math.Max(count, 1);
        return t => t <= 0 ? 0 : t >= 1 ? 1 : atStart ? MathF.Ceiling(t * count) / count : MathF.Floor(t * count) / count;
    }

    /// <summary><c>cubic-bezier(x1, y1, x2, y2)</c>: a curve from 0, 0 to 1, 1 bent toward the control points (<paramref name="x1"/>, <paramref name="y1"/>) and (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    public static Function CubicBezier(float x1, float y1, float x2, float y2)
    {
        static float Sample(float t, float p1, float p2)
        {
            var u = 1 - t;
            return (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t);
        }

        return t =>
        {
            if (t <= 0)
            {
                return 0;
            }

            if (t >= 1)
            {
                return 1;
            }

            var s = t;
            for (int i = 0; i < 8; i++)
            {
                var x = Sample(s, x1, x2) - t;
                if (MathF.Abs(x) < 1e-5f)
                {
                    break;
                }

                var u = 1 - s;
                var dx = (3 * u * u * x1) + (6 * u * s * (x2 - x1)) + (3 * s * s * (1 - x2));
                if (MathF.Abs(dx) < 1e-6f)
                {
                    break;
                }

                s -= x / dx;
            }

            return Sample(s, y1, y2);
        };
    }

    /// <summary>The easing function called <paramref name="name"/>, or <see cref="QuadraticInOut"/> if there isn't one.</summary>
    public static Function GetFunction(string? name) => TryGetFunction(name, out var function) ? function : QuadraticInOut;

    /// <summary>Finds the easing function called <paramref name="name"/>. Returns false if there isn't one.</summary>
    public static bool TryGetFunction(string? name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Function? function)
    {
        function = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Functions.TryGetValue(name, out function))
        {
            return true;
        }

        var p = new Parse(name).SkipWhitespaceAndNewlines();
        if (p.TrySkip("cubic-bezier"))
        {
            var inner = new Parse(p.SkipWhitespaceAndNewlines().ReadInnerBrackets() ?? "");
            if (inner.TryReadFloat(out var x1) && inner.TrySkipCommaSeparation() && inner.TryReadFloat(out var y1) && inner.TrySkipCommaSeparation()
                && inner.TryReadFloat(out var x2) && inner.TrySkipCommaSeparation() && inner.TryReadFloat(out var y2))
            {
                function = CubicBezier(x1, y1, x2, y2);
            }
        }
        else if (p.TrySkip("steps"))
        {
            var inner = new Parse(p.SkipWhitespaceAndNewlines().ReadInnerBrackets() ?? "");
            if (inner.TryReadFloat(out var count) && count >= 1)
            {
                function = !inner.TrySkipCommaSeparation() ? Steps((int)count)
                    : inner.ReadWord(null, readUntilEnd: true) switch
                    {
                        "start" or "jump-start" => Steps((int)count, atStart: true),
                        "end" or "jump-end" => Steps((int)count),
                        _ => null,
                    };
            }
        }

        return function is not null;
    }
}
