namespace Socotra;

internal sealed partial class PainterBatcher
{
    public void Resolve(in Painter.BoxDescriptor desc, in Matrix4x4 localTransform, int clipIndex, out BoxInstance instance)
    {
        BoxInstance.From(desc, out instance);
        var spatial = Destination.ResolveSpatial(this, localTransform);
        if (desc.HasTextMask)
        {
            instance.TextMaskIndex = list.TextureIndex(desc.TextMask!);
        }

        if (desc.HasImage)
        {
            instance.TextureIndex = list.TextureIndex(desc.BackgroundImage!);
        }
        else if (desc.HasGradient)
        {
            instance.TextureIndex = -GetOrAddGradient(in desc.BackgroundGradient) - 1;
        }

        if (desc.HasBorderImage)
        {
            instance.BorderImageIndex = list.TextureIndex(desc.BorderImage.Texture!);
        }

        instance.ShapeIndex = desc.ShapeIndex ?? (desc.PathData is null ? GetOrAddShape(desc.BorderShapeData) : GetOrAddPath(desc.PathData));
        ResolveSpatial(ref instance, clipIndex, spatial);
    }

    public void Resolve(in Painter.ShadowDescriptor desc, in Matrix4x4 localTransform, int clipIndex, out BoxInstance instance)
    {
        instance = BoxInstance.FromShadow(desc);
        var spatial = Destination.ResolveSpatial(this, localTransform);
        ResolveSpatial(ref instance, clipIndex, spatial);
        instance.InverseScissorIndex = GetOrAddScissor(Painter.Scissoring.Single(desc.Rect, desc.Radii, spatial.Transform.Inverted, invert: !desc.Inset));
    }

    public void Resolve(in Painter.OutlineDescriptor desc, in Matrix4x4 localTransform, int clipIndex, out BoxInstance instance)
    {
        instance = BoxInstance.FromOutline(desc);
        ResolveSpatial(ref instance, clipIndex, Destination.ResolveSpatial(this, localTransform));
    }

    public static bool OverlapsScissor(Rect rect, Matrix4x4 transform, in Painter.Scissoring scissor)
    {
        for (int i = 0; i < scissor.Count; i++)
        {
            ref readonly var clip = ref scissor.Clips[i];
            if (!(transform * clip.Transform).Transform(rect).Overlaps(clip.Rect))
            {
                return false;
            }
        }

        return true;
    }

    private void ResolveSpatial(ref BoxInstance instance, int clipIndex, in Spatial spatial)
    {
        instance.ScissorIndex = clipIndex < 0 ? spatial.ScissorIndex : GetOrAddDrawClip(clipIndex, Destination.Transform, spatial.ScissorIndex);
        instance.TransformIndex = spatial.TransformIndex;
    }
}
