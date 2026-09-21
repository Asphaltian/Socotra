using SkiaSharp;

namespace Socotra;

internal static class SkiaCompat
{
    extension(Color color)
    {
        public SKColorF ToSkF() => new(color.R, color.G, color.B, color.A);
    }
}
