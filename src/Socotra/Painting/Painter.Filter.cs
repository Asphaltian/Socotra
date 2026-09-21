namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Blur and color changes for <see cref="BeginLayer"/> and <see cref="FilterBackdrop(Socotra.Rect, Filter, CornerRadii)"/>.
    /// Start from <c>new Filter()</c>, which changes nothing, and set only what you need.
    /// </summary>
    /// <example>
    /// <code>
    /// // frosted glass behind a panel-sized box
    /// painter.FilterBackdrop(painter.Bounds, new Painter.Filter { Blur = 8, Brightness = 0.8f }, 12);
    /// </code>
    /// </example>
    public readonly record struct Filter
    {
        /// <summary>Makes a filter that changes nothing.</summary>
        public Filter()
        {
        }

        /// <summary>The blur radius in pixels. 0 doesn't blur.</summary>
        public float Blur { get; init; }

        /// <summary>Multiplies brightness. 1 keeps it, 0 makes everything black.</summary>
        public float Brightness { get; init; } = 1;

        /// <summary>Multiplies contrast. 1 keeps it, 0 makes everything gray.</summary>
        public float Contrast { get; init; } = 1;

        /// <summary>Multiplies saturation. 1 keeps it, 0 removes all color.</summary>
        public float Saturation { get; init; } = 1;

        /// <summary>How much sepia tone to apply, from 0 to 1.</summary>
        public float Sepia { get; init; }

        /// <summary>How much to invert the colors, from 0 to 1.</summary>
        public float Invert { get; init; }

        /// <summary>How far to turn every hue around the color wheel, in degrees.</summary>
        public float HueRotation { get; init; }

        /// <summary>Multiplies the result, alpha included. White changes nothing.</summary>
        public Color Tint { get; init; } = Color.White;

        internal void Validate()
        {
            if (!float.IsFinite(Blur) || Blur < 0 || !float.IsFinite(Brightness) || Brightness < 0
                || !float.IsFinite(Contrast) || Contrast < 0 || !float.IsFinite(Saturation) || Saturation < 0
                || !float.IsFinite(Sepia) || Sepia < 0 || Sepia > 1 || !float.IsFinite(Invert) || Invert < 0 || Invert > 1 || !float.IsFinite(HueRotation))
            {
                throw new ArgumentOutOfRangeException(nameof(Filter));
            }

            if (!float.IsFinite(Tint.R) || !float.IsFinite(Tint.G) || !float.IsFinite(Tint.B) || !float.IsFinite(Tint.A))
            {
                throw new ArgumentOutOfRangeException(nameof(Tint), "The filter's tint must be a real color.");
            }
        }
    }

    /// <summary>An image that hides parts of a layer. Pass it to <see cref="BeginLayer"/>.</summary>
    /// <param name="Texture">The mask image.</param>
    /// <param name="Rect">Where the image sits.</param>
    /// <param name="Mode">Whether the image's transparency or its brightness does the hiding.</param>
    /// <param name="Repeat">How the image repeats past its rectangle.</param>
    /// <param name="Rotation">How far the image is turned around its middle, in degrees.</param>
    /// <param name="Sampling">How the image is sampled when scaled.</param>
    public readonly record struct Mask(Texture Texture, Rect Rect, MaskMode Mode = MaskMode.Alpha,
        BackgroundRepeat Repeat = BackgroundRepeat.NoRepeat, float Rotation = 0, FilterMode Sampling = FilterMode.Bilinear);

    /// <summary>
    /// Applies <paramref name="filter"/> to what's already drawn behind <paramref name="rect"/>, like frosted glass.
    /// Draw whatever goes on top afterwards.
    /// </summary>
    public void FilterBackdrop(Rect rect, Filter filter, CornerRadii corners = default)
    {
        if (!ValidBounds(rect))
        {
            throw new ArgumentOutOfRangeException(nameof(rect));
        }

        filter.Validate();
        var context = ActiveContext;
        if (!context.State.HasArea || context.State.Opacity == 0)
        {
            return;
        }

        context.Batcher.AddBackdrop(
            new BackdropData(rect, filter, corners.Resolve(rect), context.State.Opacity * context.InheritedOpacity),
            DrawingTransform(context),
            context.State.ClipIndex,
            context.State.OverrideBlendMode);
    }

    internal readonly record struct BackdropData(Rect Rect, Filter Filter, BorderRadii Radii, float Opacity);
}
