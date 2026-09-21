namespace Socotra.Vulkan.Tests;

public static class Gaussian
{
    public static float Coverage(float x, float from, float to, float sigma) =>
        0.5f * (Erf((to - x) / (sigma * MathF.Sqrt(2))) - Erf((from - x) / (sigma * MathF.Sqrt(2))));

    private static float Erf(float x)
    {
        var t = 1 / (1 + (0.3275911f * MathF.Abs(x)));
        var y = 1 - ((((((((1.061405429f * t) - 1.453152027f) * t) + 1.421413741f) * t) - 0.284496736f) * t) + 0.254829592f) * t * MathF.Exp(-x * x);
        return MathF.Sign(x) * y;
    }
}
