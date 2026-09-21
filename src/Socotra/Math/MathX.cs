namespace Socotra;

internal static class MathX
{
    public static float Clamp(float value, float min, float max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }

        return value < min ? min : value < max ? value : max;
    }

    public static float Lerp(float from, float to, float fraction, bool clamp = true)
    {
        if (clamp)
        {
            fraction = Clamp(fraction, 0, 1);
        }

        return from + (fraction * (to - from));
    }

    public static float LerpInverse(float value, float from, float to, bool clamp = true)
    {
        if (clamp)
        {
            value = Clamp(value, from, to);
        }

        value -= from;
        to -= from;
        return to == 0 ? 0 : value / to;
    }

    public static float Remap(float value, float oldLow, float oldHigh, float newLow, float newHigh, bool clamp)
    {
        if (MathF.Abs(oldHigh - oldLow) < 0.0001f)
        {
            return clamp ? newLow : value;
        }

        var result = newLow + ((value - oldLow) * (newHigh - newLow) / (oldHigh - oldLow));
        return clamp ? Clamp(result, newLow, newHigh) : result;
    }

    public static float SnapToGrid(float value, float gridSize)
    {
        if (MathF.Abs(gridSize) <= 0.0001f)
        {
            return value;
        }

        var snapped = MathF.Round(value / gridSize) * gridSize;
        return snapped == 0 ? 0 : snapped;
    }
}
