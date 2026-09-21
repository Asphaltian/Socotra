namespace Socotra;

internal sealed partial class PainterBatcher
{
    public void AddText(List<TextInstance> glyphs, BlendMode blendMode, in GradientInfo gradient, Matrix4x4 transform, int clipIndex, float opacity = 1)
    {
        var spatial = Destination.ResolveSpatial(this, transform);
        int gradientIndex = gradient.IsEmpty ? -1 : GetOrAddGradient(gradient);
        foreach (var glyph in glyphs)
        {
            var text = glyph;
            if (gradientIndex >= 0 && GpuFontText.WantsGradient(text))
            {
                text.TextureIndex = -gradientIndex - 1;
            }

            var box = new BoxInstance
            {
                Rect = text.Rect,
                Color = text.Color.WithAlphaMultiplied(opacity),
                TextureIndex = Texts.Add(text),
                Flags = BoxInstance.TextFlag,
                ShapeIndex = -1,
                InverseScissorIndex = -1,
                TextMaskIndex = -1,
            };
            ResolveSpatial(ref box, clipIndex, spatial);
            Append(box, blendMode);
        }
    }
}
