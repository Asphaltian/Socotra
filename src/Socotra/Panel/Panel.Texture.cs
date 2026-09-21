namespace Socotra;

public partial class Panel
{
    /// <summary>
    /// Draws <paramref name="texture"/> over the panel from <see cref="OnDraw"/>, placed and sized by the panel's
    /// <c>background-size</c>, <c>background-position</c>, <c>background-repeat</c>, <c>background-tint</c>,
    /// <c>image-rendering</c> and rounded corners. Pass <paramref name="defaultSize"/> for when <c>background-size</c>
    /// isn't set, like <see cref="Length.Contain"/> or <see cref="Length.Cover"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// public override void OnDraw(Painter painter)
    /// {
    ///     // Fit the whole picture inside the panel
    ///     DrawTexture(painter, Picture, Length.Contain);
    /// }
    /// </code>
    /// </example>
    public void DrawTexture(Painter painter, Texture? texture, Length defaultSize)
    {
        if (texture is null || ComputedStyle is not { } style)
        {
            return;
        }

        var rect = new Rect(Vector2.Zero, Box.Rect.Size);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var tile = ImageRect.Calculate(new ImageRect.Input
        {
            Image = texture,
            PanelRect = rect,
            ScaleToScreen = ScaleToScreen,
            DefaultSize = defaultSize,
            ImageSizeX = style.BackgroundSizeX is { Unit: not LengthUnit.Undefined } sizeX ? sizeX : defaultSize,
            ImageSizeY = style.BackgroundSizeY is { Unit: not LengthUnit.Undefined } sizeY ? sizeY : defaultSize,
            ImagePositionX = style.BackgroundPositionX,
            ImagePositionY = style.BackgroundPositionY,
        });

        if (tile.Z <= 0 || tile.W <= 0)
        {
            return;
        }

        using var scope = painter.Scope();
        painter.Stroke = Stroke.None;
        var border = Box.Border;
        var inset = GetBackgroundClipInset(style.BackgroundClip ?? BackgroundClip.BorderBox);
        painter.ClipFill(new Vector4(MathF.Max(border.Left, inset.X), MathF.Max(border.Top, inset.Y), MathF.Max(border.Right, inset.Z), MathF.Max(border.Bottom, inset.W)));
        var fill = Fill.Image(texture, style.BackgroundTint!.Value, tile.Z, tile.W, tile.X, tile.Y, style.BackgroundRepeat ?? BackgroundRepeat.Repeat, PaintCache.SamplingFor(style.ImageRendering));
        painter.Fill = fill with { Rotation = style.BackgroundAngle!.Value.GetPixels(1) };
        painter.DrawRect(rect, style.GetBorderRadii(rect), style.BorderShape);
    }
}
