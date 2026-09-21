namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Draws <paramref name="text"/> inside <paramref name="rect"/> with the current <see cref="TextStyle"/>. It wraps at
    /// the rectangle's width and sits where the style's alignment puts it.
    /// </summary>
    /// <example>
    /// <code>
    /// // bold, centered in the bottom 40 pixels
    /// painter.TextStyle = new TextStyle("Poppins", 18).WithBold().WithAlignment(TextFlag.Center);
    /// painter.Text("Press Start", new Rect(0, painter.Bounds.Height - 40, painter.Bounds.Width, 40));
    /// </code>
    /// </example>
    public void Text(string text, Rect rect) => DrawText(text, rect, TextStyle);

    /// <summary>Draws <paramref name="texture"/> stretched over <paramref name="rect"/>. Pass <paramref name="tint"/> to color it.</summary>
    public void Texture(Texture texture, Rect rect, Color? tint = null) => Texture(texture, rect, tint ?? Color.White, FilterMode.Bilinear);

    /// <summary>
    /// Draws a soft shadow around <paramref name="rect"/>, or inside it with <paramref name="inset"/>, like
    /// <c>box-shadow</c>. Corners work like <see cref="Rect(Socotra.Rect, CornerRadii)"/>.
    /// </summary>
    /// <param name="rect">The rectangle casting the shadow.</param>
    /// <param name="corners">The rectangle's corner radii.</param>
    /// <param name="color">The shadow's color. Black if left out.</param>
    /// <param name="blur">How soft the shadow is, in pixels.</param>
    /// <param name="spread">How much bigger the shadow is than the rectangle, in pixels. Negative shrinks it.</param>
    /// <param name="offset">How far the shadow is moved, in pixels.</param>
    /// <param name="inset">Draws the shadow inside the rectangle instead.</param>
    public void RectShadow(Rect rect, CornerRadii corners = default, Color? color = null, float blur = 0, float spread = 0, Vector2 offset = default, bool inset = false) =>
        Add(ActiveContext, new ShadowDescriptor(rect, (color ?? Color.Black).WithAlphaMultiplied(ActiveContext.InheritedOpacity))
        {
            Radii = corners.Resolve(rect),
            Blur = blur,
            Spread = spread,
            Offset = offset,
            Inset = inset,
        });

    /// <summary>
    /// How big <paramref name="text"/> is when drawn with the current <see cref="TextStyle"/>, shadow and outline
    /// included. Pass <paramref name="maxSize"/> to wrap it at that width, or leave it out to keep lines from wrapping.
    /// </summary>
    public Vector2 MeasureText(string text, Vector2 maxSize = default)
    {
        if (maxSize != default && (!IsFinite(maxSize) || maxSize.X <= 0 || maxSize.Y <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Expected positive finite layout bounds, or default for unconstrained text.");
        }

        return GetTextBlock(text, maxSize, TextStyle)?.Size ?? Vector2.Zero;
    }

    /// <summary>
    /// Where <paramref name="text"/> ends up when you draw it in <paramref name="rect"/> with the current
    /// <see cref="TextStyle"/>. Use it to put a background or cursor right behind the text.
    /// </summary>
    public Rect MeasureText(string text, Rect rect)
    {
        var size = MeasureText(text, rect.Size);
        return size == default ? new Rect(rect.Position, Vector2.Zero) : rect.Align(size, TextStyle.Alignment).Floor();
    }

    internal void Glyphs(List<TextInstance> glyphs, BlendMode blendMode, in GradientInfo gradient) =>
        GetActiveContext().Batcher.AddText(glyphs, blendMode, gradient, Matrix4x4.Identity, -1);

    internal void DrawText(string text, Rect rect, TextStyle style)
    {
        var context = ActiveContext;
        var opacity = style.Color.A * context.InheritedOpacity * context.State.Opacity;
        if (opacity == 0 || !context.State.HasArea)
        {
            return;
        }

        var block = GetTextBlock(text, rect.Size, style);
        if (block is null || block.IsEmpty)
        {
            return;
        }

        var textRect = rect.Align(block.Size, style.Alignment).Floor();
        var glyphs = context.TextInstances;
        glyphs.Clear();
        GpuFontText.Build(block.Layout, textRect.Position + block.BlockOrigin, GpuFontText.Options.Default, glyphs);
        context.Batcher.AddText(glyphs, context.State.OverrideBlendMode, default, DrawingTransform(context), context.State.ClipIndex, opacity);
    }

    internal void Texture(Texture texture, Rect rect, Color tint, FilterMode filterMode) =>
        Add(ActiveContext, new BoxDescriptor(rect, Color.Transparent)
        {
            BackgroundImage = texture,
            BackgroundTint = tint.WithAlphaMultiplied(ActiveContext.InheritedOpacity),
            BackgroundRepeat = BackgroundRepeat.Clamp,
            FilterMode = filterMode,
        });

    internal void RectShadow(Rect rect, in BorderRadii radii, Color color, float blur, float spread, Vector2 offset, bool inset)
    {
        var context = GetActiveContext();
        context.Batcher.Add(
            new ShadowDescriptor(rect, color.WithAlphaMultiplied(context.InheritedOpacity))
            {
                Radii = radii.Clamped(rect.Width, rect.Height),
                Blur = blur,
                Spread = spread,
                Offset = offset,
                Inset = inset,
                OverrideBlendMode = context.InitialBlendMode,
            },
            Matrix4x4.Identity,
            -1);
    }

    private TextRendering.TextBlock? GetTextBlock(string text, Vector2 size, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var scope = style.CreateScope(text, ActiveContext.ScaleToScreen);
        return TextRendering.GetOrCreateTextBlock(scope, style.Alignment, size);
    }
}
