namespace Socotra;

internal static class ColorPickerTextures
{
    public const int WheelSize = 220;
    public const float RingOuter = 108;
    public const float RingInner = 95;
    public const float DiscRadius = 108;
    public const int CheckerSize = CheckerCell * 2;

    private const int CheckerCell = 8;

    private static Texture? _checkerboard;
    private static Texture? _hueRing;
    private static Texture? _hueDisc;

    public static Texture Checkerboard => _checkerboard ??= Build(CheckerSize, CheckerSize, (x, y) =>
    {
        var light = ((x / CheckerCell) + (y / CheckerCell)) % 2 == 0;
        return light ? Color.FromBytes(150, 150, 150) : Color.FromBytes(100, 100, 100);
    });

    public static Texture HueRing => _hueRing ??= Build(WheelSize, WheelSize, (x, y) =>
    {
        var (angle, distance) = Polar(x, y);
        return Color.FromHsv(angle, 1, 1, Edge(RingOuter - distance) * Edge(distance - RingInner));
    });

    public static Texture HueDisc => _hueDisc ??= Build(WheelSize, WheelSize, (x, y) =>
    {
        var (angle, distance) = Polar(x, y);
        return Color.FromHsv(angle, MathF.Min(1, distance / DiscRadius), 1, Edge(DiscRadius - distance));
    });

    private static (float Angle, float Distance) Polar(int x, int y)
    {
        var dx = x + 0.5f - (WheelSize * 0.5f);
        var dy = y + 0.5f - (WheelSize * 0.5f);
        var angle = (float.RadiansToDegrees(MathF.Atan2(dy, dx)) + 360) % 360;
        return (angle, MathF.Sqrt((dx * dx) + (dy * dy)));
    }

    private static float Edge(float inside) => Math.Clamp(inside + 0.5f, 0, 1);

    private static Texture Build(int width, int height, Func<int, int, Color> pixel)
    {
        var data = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var c = pixel(x, y);
                var i = ((y * width) + x) * 4;
                data[i] = Color.ToByte(c.R);
                data[i + 1] = Color.ToByte(c.G);
                data[i + 2] = Color.ToByte(c.B);
                data[i + 3] = Color.ToByte(c.A);
            }
        }

        return Texture.FromPixels(width, height, data);
    }
}
