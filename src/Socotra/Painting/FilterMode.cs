namespace Socotra;

/// <summary>How a texture is sampled when it's drawn bigger or smaller than it is.</summary>
public enum FilterMode
{
    /// <summary>The nearest pixel, for hard pixel edges.</summary>
    Point,

    /// <summary>A blend of the four nearest pixels.</summary>
    Bilinear,

    /// <summary>Bilinear, blending between mipmaps too.</summary>
    Trilinear,

    /// <summary>Trilinear, and sharper at steep angles.</summary>
    Anisotropic,
}

internal enum TextureAddress
{
    Wrap,
    Clamp,
    Border,
}

internal static class Samplers
{
    public const int TrilinearBorder = 0;

    public const int TrilinearClamp = 1;

    public const int First = 2;

    public const int Count = First + (4 * 3 * 3);

    public static int Index(FilterMode filter, TextureAddress u, TextureAddress v) => First + ((int)filter * 9) + ((int)u * 3) + (int)v;

    public static (FilterMode Filter, TextureAddress U, TextureAddress V) At(int index)
    {
        index -= First;
        return ((FilterMode)(index / 9), (TextureAddress)(index / 3 % 3), (TextureAddress)(index % 3));
    }
}
