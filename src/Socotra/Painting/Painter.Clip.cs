namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Cuts off anything you draw from now on that falls outside <paramref name="rect"/>, optionally with rounded corners.
    /// Each clip narrows the ones before it. Call it inside <see cref="Scope"/> to get rid of it again, and set up your
    /// transform first: moving the transform afterwards doesn't move the clip.
    /// </summary>
    /// <example>
    /// <code>
    /// using (painter.Scope())
    /// {
    ///     // only the part of the circle inside the rounded box shows
    ///     painter.Clip(new Rect(0, 0, 100, 100), 12);
    ///     painter.Circle(new Vector2(100, 100), 80);
    /// }
    /// </code>
    /// </example>
    public void Clip(Rect rect, CornerRadii corners = default) => ClipRect(rect, corners.Resolve(rect));

    internal bool IsRectVisible(Rect rect)
    {
        var context = ActiveContext;
        var output = context.Batcher;
        var transform = DrawingTransform(context) * output.Destination.Transform;
        if (!output.Destination.Scissor.Invert && !PainterBatcher.OverlapsScissor(rect, transform, output.Destination.Scissor))
        {
            return false;
        }

        for (int index = context.State.ClipIndex; index >= 0; index = output.DrawClips[index].Parent)
        {
            var clip = output.DrawClips[index];
            var matrix = output.Destination.Transform.Inverted * clip.Transform;
            if (!PainterBatcher.OverlapsScissor(rect, transform, Scissoring.Single(clip.Rect, clip.Radii, matrix)))
            {
                return false;
            }
        }
        return true;
    }

    void ClipRect(Rect rect, BorderRadii radii)
    {
        if (!IsFinite(rect.Position) || !float.IsFinite(rect.Width) || !float.IsFinite(rect.Height)
            || !float.IsFinite(rect.Right) || !float.IsFinite(rect.Bottom))
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Clip bounds must be finite.");
        }

        var buffer = ActiveContext;
        var matrix = Matrix4x4.Identity;
        if (buffer.State.HasArea)
        {
            matrix = DrawingTransform(buffer).Inverted;
        }
        else
        {
            rect = default;
        }

        rect.Size = Vector2.Max(rect.Size, Vector2.Zero);
        var clips = buffer.Batcher.DrawClips;
        clips.Add(new Painter.ClipEntry(rect, radii, matrix, buffer.State.ClipIndex));
        buffer.State.ClipIndex = clips.Count - 1;
    }

    internal readonly record struct ClipEntry(Rect Rect, BorderRadii Radii, Matrix4x4 Transform, int Parent);

    internal void ClipFill(Vector4 insets)
    {
        for (int i = 0; i < 4; i++)
        {
            if (!float.IsFinite(insets[i]) || insets[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(insets));
            }
        }

        ActiveContext.State.FillInsets = insets;
        ActiveContext.State.FillMask = null;
    }

    internal void MaskFill(Texture texture, Rect rect)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (!ValidBounds(rect))
        {
            throw new ArgumentOutOfRangeException(nameof(rect));
        }

        ActiveContext.State.FillMask = texture;
        ActiveContext.State.FillMaskRect = rect;
        ActiveContext.State.FillInsets = default;
    }
}
