namespace Socotra;

/// <summary>How drawing combines with what's already there.</summary>
public enum BlendMode
{
    /// <summary>Drawing covers what's behind it, as far as it's opaque.</summary>
    Normal,

    /// <summary>Colors multiply, so drawing darkens what's behind it.</summary>
    Multiply,

    /// <summary>Colors add, so drawing lightens what's behind it.</summary>
    Lighten,

    /// <summary>Like <see cref="Normal"/>, for textures whose colors are already multiplied by their alpha.</summary>
    PremultipliedAlpha,
}
