namespace Socotra.Vulkan.Tests;

public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static Rgba Black => new(0, 0, 0);

    public static Rgba White => new(255, 255, 255);

    public static Rgba Red => new(255, 0, 0);

    public static Rgba Green => new(0, 255, 0);

    public static Rgba Blue => new(0, 0, 255);

    public static Rgba Of(float r, float g, float b, float a = 1) => new(Byte(r), Byte(g), Byte(b), Byte(a));

    public override string ToString() => $"({R}, {G}, {B}, {A})";

    private static byte Byte(float value) => (byte)Math.Clamp(MathF.Round(value * 255), 0, 255);
}

public sealed class Snapshot(int width, int height, byte[] pixels, bool bgra)
{
    public int Width => width;

    public int Height => height;

    public Rgba this[int x, int y]
    {
        get
        {
            var i = ((y * width) + x) * 4;
            return bgra ? new Rgba(pixels[i + 2], pixels[i + 1], pixels[i], pixels[i + 3]) : new Rgba(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
        }
    }

    public void Expect(int x, int y, Rgba expected, int tolerance = 2)
    {
        var actual = this[x, y];
        var close = Math.Abs(actual.R - expected.R) <= tolerance && Math.Abs(actual.G - expected.G) <= tolerance
            && Math.Abs(actual.B - expected.B) <= tolerance && Math.Abs(actual.A - expected.A) <= tolerance;
        Assert.True(close, $"Pixel ({x}, {y}) is {actual}, expected {expected} within {tolerance}.");
    }
}
