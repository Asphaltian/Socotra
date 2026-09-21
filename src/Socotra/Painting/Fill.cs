namespace Socotra;

/// <summary>
/// What to paint a shape with: a color, an image or a gradient. Set it on <see cref="Painter.Fill"/>, or pass it to a
/// <see cref="Stroke"/>. A <see cref="Color"/> or <see cref="Texture"/> works wherever a fill is asked for.
/// </summary>
/// <remarks>
/// Gradients take 2 to 8 stops, with offsets from 0 to 1 in order; two stops at the same offset make a hard edge.
/// Where a gradient takes <see cref="Length"/> points, percentages are of the shape's size, measured from its top left.
/// </remarks>
/// <example>
/// <code>
/// // solid
/// painter.Fill = Color.White;
///
/// // top to bottom, white to black
/// painter.Fill = Fill.LinearGradient(Color.White, Color.Black, 90);
///
/// // an image tiled every 32 pixels
/// painter.Fill = Fill.Image(texture, width: 32, height: 32, repeat: BackgroundRepeat.Repeat);
///
/// painter.Rect(painter.Bounds);
/// </code>
/// </example>
public readonly partial struct Fill
{
    readonly Color _color;
    readonly Texture? _texture;
    readonly Length _imageWidth;
    readonly Length _imageHeight;
    readonly Length _imageOffsetX;
    readonly Length _imageOffsetY;
    readonly GradientCoordinates? _gradientCoordinates;
    readonly BackgroundRepeat _repeat;
    readonly FilterMode _filter;
    readonly GradientInfo _gradient;
    readonly bool _hasLayoutRect;
    internal float Rotation { get; init; }
    Color Background { get; init; }
    BlendMode BackgroundBlend { get; init; }

    /// <summary>
    /// Paints nothing. Set it on <see cref="Painter.Fill"/> to draw only the stroke.
    /// </summary>
    public static Fill None => default;

    /// <summary>
    /// Paints with <paramref name="color"/>.
    /// </summary>
    public static Fill Solid(Color color) => new(color);

    /// <summary>
    /// Paints with <paramref name="color"/>.
    /// </summary>
    public Fill(Color color)
    {
        _color = color;
    }

    Fill(Texture texture, Color tint, Length width, Length height, Length offsetX, Length offsetY, BackgroundRepeat repeat, FilterMode filter)
    {
        _texture = texture;
        _color = tint;
        _imageWidth = width;
        _imageHeight = height;
        _imageOffsetX = offsetX;
        _imageOffsetY = offsetY;
        _repeat = repeat;
        _filter = filter;
    }

    Fill(GradientInfo gradient, GradientCoordinates? coordinates = null)
    {
        _gradient = gradient;
        _gradientCoordinates = coordinates;
        _color = Color.White;
        _repeat = BackgroundRepeat.Clamp;
    }

    internal Fill(Color color, Texture? texture, in GradientInfo gradient, Vector4 tile, Color tint,
        BackgroundRepeat repeat, FilterMode filter, float angle, BlendMode blend)
    {
        _texture = texture;
        _gradient = gradient;
        _color = texture is not null || !gradient.IsEmpty ? tint : color;
        Background = color;
        _repeat = repeat;
        _filter = filter;
        Rotation = angle;
        BackgroundBlend = blend;
        _imageWidth = tile.Z;
        _imageHeight = tile.W;
        _imageOffsetX = tile.X;
        _imageOffsetY = tile.Y;
        _hasLayoutRect = true;
    }

    /// <summary>
    /// Lets you use a color as a fill.
    /// </summary>
    public static implicit operator Fill(Color color) => new(color);

    /// <summary>
    /// Lets you use a texture as a fill, stretched over the shape. A null texture paints nothing.
    /// </summary>
    public static implicit operator Fill(Texture texture) => texture is null ? None : Image(texture);

    /// <summary>
    /// Paints the shape with <paramref name="texture"/>, stretched over it unless you give a size. Give only
    /// <paramref name="width"/> or <paramref name="height"/> to keep the image's proportions, and <see cref="Length.Auto"/>
    /// for both to use its own size. Sizes and offsets are pixels or percentages of the shape.
    /// </summary>
    /// <example>
    /// <code>
    /// // 100 pixels wide, keeping its proportions, tiled from 50 pixels in
    /// painter.Fill = Fill.Image(texture, width: 100, offsetX: 50, repeat: BackgroundRepeat.Repeat);
    /// painter.Rect(painter.Bounds);
    /// </code>
    /// </example>
    public static Fill Image(Texture texture, Color? tint = null, Length? width = null, Length? height = null, Length? offsetX = null, Length? offsetY = null, BackgroundRepeat repeat = BackgroundRepeat.Clamp, FilterMode filter = FilterMode.Bilinear)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (!width.HasValue && !height.HasValue)
        {
            width = height = Length.Percent(100);
        }

        var w = width ?? Length.Auto;
        var h = height ?? Length.Auto;
        var x = offsetX ?? 0;
        var y = offsetY ?? 0;
        ValidateImageLength(w, nameof(width), size: true);
        ValidateImageLength(h, nameof(height), size: true);
        ValidateImageLength(x, nameof(offsetX), size: false);
        ValidateImageLength(y, nameof(offsetY), size: false);
        if (!Enum.IsDefined(repeat))
        {
            throw new ArgumentOutOfRangeException(nameof(repeat));
        }

        if (!Enum.IsDefined(filter))
        {
            throw new ArgumentOutOfRangeException(nameof(filter));
        }

        return new Fill(texture, tint ?? Color.White, w, h, x, y, repeat, filter);
    }

    /// <summary>
    /// A copy with the image or gradient turned clockwise by <paramref name="degrees"/>. The shape itself doesn't turn.
    /// </summary>
    public Fill WithRotation(float degrees)
    {
        if (!float.IsFinite(degrees))
        {
            throw new ArgumentOutOfRangeException(nameof(degrees));
        }

        return this with { Rotation = float.DegreesToRadians(degrees) };
    }

    static void ValidateImageLength(Length length, string parameter, bool size)
    {
        if (size && length.Unit == LengthUnit.Auto)
        {
            return;
        }

        if (!(length.Unit is LengthUnit.Pixels or LengthUnit.Percentage || length.Unit.IsDynamic())
            || !float.IsFinite(length.Value) || (size && length.Unit != LengthUnit.Expression && length.Value <= 0))
        {
            throw new ArgumentOutOfRangeException(parameter, "Expected a finite numeric length, positive for image sizes, or Auto for an image size.");
        }
    }

    Vector4 GetImageRect(Rect bounds)
    {
        var texture = _texture!;
        var autoWidth = _imageWidth.Unit == LengthUnit.Auto;
        var autoHeight = _imageHeight.Unit == LengthUnit.Auto;
        float w = autoWidth ? texture.Width : _imageWidth.GetPixels(bounds.Width);
        float h = autoHeight ? texture.Height : _imageHeight.GetPixels(bounds.Height);
        if (autoWidth && !autoHeight)
        {
            w = h * texture.Width / texture.Height;
        }

        if (autoHeight && !autoWidth)
        {
            h = w * texture.Height / texture.Width;
        }

        float x = _imageOffsetX.GetPixels(bounds.Width);
        float y = _imageOffsetY.GetPixels(bounds.Height);
        if (!float.IsFinite(w) || !float.IsFinite(h) || w <= 0 || h <= 0
            || !float.IsFinite(bounds.Width / w) || !float.IsFinite(bounds.Height / h)
            || !float.IsFinite(x / w) || !float.IsFinite(y / h))
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Image lengths must resolve to finite offsets and positive sizes within the shape's bounds.");
        }

        return new Vector4(x, y, w, h);
    }

    internal bool IsTransparent => _color.A == 0 && Background.A == 0;

    internal bool TryGetSolidColor(out Color color)
    {
        color = _color;
        return _texture is null && _gradient.IsEmpty;
    }

    internal Painter.BoxDescriptor CreateDescriptor(Rect bounds, Painter.Context buffer, bool clipFill = true)
    {
        ref var state = ref buffer.State;
        return CreateDescriptor(bounds, buffer.InheritedOpacity, state.OverrideBlendMode, state.FillInsets, state.FillMask, state.FillMaskRect, clipFill);
    }

    internal Painter.BoxDescriptor CreateDescriptor(Rect bounds, float opacity, BlendMode blendMode, in Vector4 fillInsets, Texture? fillMask = null, Rect fillMaskRect = default, bool clipFill = true)
    {
        var hasImage = _texture != null;
        var hasBackground = hasImage || !_gradient.IsEmpty;
        var backgroundRect = _hasLayoutRect
            ? new Vector4(_imageOffsetX.Value, _imageOffsetY.Value, _imageWidth.Value, _imageHeight.Value)
            : hasImage ? GetImageRect(bounds) : new Vector4(0, 0, bounds.Width, bounds.Height);
        var backgroundGradient = _gradient;
        _gradientCoordinates?.Resolve(bounds, ref backgroundGradient, out backgroundRect);

        return new Painter.BoxDescriptor(bounds, hasBackground ? Background.WithAlphaMultiplied(opacity) : _color.WithAlphaMultiplied(opacity))
        {
            BackgroundImage = _texture,
            BackgroundAngle = Rotation,
            BackgroundBlendMode = BackgroundBlend,
            BackgroundClip = clipFill && fillMask is not null ? BackgroundClip.Text : clipFill && fillInsets != Vector4.Zero ? BackgroundClip.ContentBox : BackgroundClip.BorderBox,
            BackgroundClipInset = fillInsets,
            TextMask = clipFill ? fillMask : null,
            TextMaskRect = new Vector4(fillMaskRect.Left - bounds.Left, fillMaskRect.Top - bounds.Top, fillMaskRect.Width, fillMaskRect.Height),
            BackgroundGradient = backgroundGradient,
            BackgroundRect = backgroundRect,
            BackgroundTint = hasBackground ? _color.WithAlphaMultiplied(opacity) : Color.Transparent,
            BackgroundRepeat = _repeat,
            FilterMode = _filter,
            OverrideBlendMode = blendMode,
        };
    }
}
