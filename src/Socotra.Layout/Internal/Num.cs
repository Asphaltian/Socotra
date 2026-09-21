using System.Runtime.CompilerServices;

namespace Socotra.Layout;

internal static class Num
{
    public const float Undefined = float.NaN;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUndefined(float value) => float.IsNaN(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDefined(float value) => !float.IsNaN(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaxOrDefined(float a, float b)
    {
        if (IsDefined(a) && IsDefined(b))
        {
            return MathF.Max(a, b);
        }

        return IsUndefined(a) ? b : a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MinOrDefined(float a, float b)
    {
        if (IsDefined(a) && IsDefined(b))
        {
            return MathF.Min(a, b);
        }

        return IsUndefined(a) ? b : a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InexactEquals(float a, float b)
    {
        if (IsDefined(a) && IsDefined(b))
        {
            return MathF.Abs(a - b) < 0.0001f;
        }

        return IsUndefined(a) && IsUndefined(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OptionalEquals(float a, float b) => a == b
        || (IsUndefined(a) && IsUndefined(b));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float UnwrapOrDefault(float value, float fallback) => IsUndefined(value) ? fallback : value;
}
