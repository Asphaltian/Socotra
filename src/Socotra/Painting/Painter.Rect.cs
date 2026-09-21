namespace Socotra;

public readonly ref partial struct Painter
{
    /// <summary>
    /// Draws a rectangle with <see cref="Fill"/> and a CSS-style border inside it, each side with its own width and
    /// color. Pass the widths as left, top, right, bottom. The border's look comes from <see cref="Stroke"/>'s
    /// <see cref="Socotra.Stroke.Style"/>; the rest of the stroke doesn't apply. Throws if a width is negative.
    /// </summary>
    public void Rect(Rect rect, Vector4 borderWidths, Color colorLeft, Color colorTop, Color colorRight, Color colorBottom, CornerRadii corners = default)
    {
        var border = ResolveBoxStroke(borderWidths, colorLeft, colorTop, colorRight, colorBottom, Stroke.Style);
        if (!ValidBounds(rect))
        {
            return;
        }

        var context = ActiveContext;
        if (context.State.Fill.IsTransparent && !border.HasInk)
        {
            return;
        }

        var desc = context.State.Fill.CreateDescriptor(rect, context);
        desc.Radii = corners.Resolve(rect);
        desc.Stroke = border.WithAlphaMultiplied(context.InheritedOpacity);
        Add(context, desc);
    }

    internal static BoxStroke ResolveBoxStroke(Vector4 widths, Color left, Color top, Color right, Color bottom, BorderStyle style)
    {
        for (int i = 0; i < 4; i++)
        {
            if (!float.IsFinite(widths[i]) || widths[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(widths));
            }
        }

        return new BoxStroke
        {
            Size = style is BorderStyle.None or BorderStyle.Hidden ? default : widths,
            Style = style,
            ColorL = left,
            ColorT = top,
            ColorR = right,
            ColorB = bottom
        };
    }

    internal static bool TryGetBoxStroke(Stroke stroke, out BoxStroke result)
    {
        result = default;
        if (!HasStroke(stroke) || stroke.Alignment != Stroke.StrokeAlignment.Inside
            || stroke.Style is BorderStyle.Dashed or BorderStyle.Dotted
            || !stroke.Fill.TryGetSolidColor(out var color))
        {
            return false;
        }

        result = ResolveBoxStroke(new Vector4(stroke.Width), color, color, color, color, stroke.Style);
        return true;
    }
}
