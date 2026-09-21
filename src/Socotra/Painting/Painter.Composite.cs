namespace Socotra;

public readonly ref partial struct Painter
{
    internal void Composite(int layer, Rect bounds, Filter filter, Mask? mask, MaskScope maskScope, IReadOnlyList<Shadow> shadows, float borderWidth, Color borderColor)
    {
        var output = Output;
        output.Flush();
        var target = output.Destination;
        var scissor = output.GetOrAddScissor(target.Scissor);
        QuadPlacement Place(Rect quad, BlendMode blend) => new(quad, target.Transform, target.LayerMatrix, scissor, blend);

        foreach (var shadow in shadows)
        {
            var offset = new Vector2(shadow.OffsetX, shadow.OffsetY);
            var padding = MathF.Max(MathF.Abs(offset.X), MathF.Abs(offset.Y)) + MathF.Ceiling(shadow.Blur * 3) + 1;
            output.List.Commands.Add(new DropShadowCommand(layer, Place(bounds.Grow(padding), BlendMode.Normal), bounds, offset, shadow.Blur, shadow.Color));
        }

        if (borderWidth > 0)
        {
            output.List.Commands.Add(new BorderWrapCommand(layer, Place(bounds.Grow(borderWidth), BlendMode.Normal), bounds, borderWidth, borderColor));
        }

        var filtered = bounds.Grow(MathF.Ceiling(filter.Blur * 3)).Ceiling();
        output.List.Commands.Add(new FilterCommand(layer, Place(filtered, InheritedBlendMode), bounds, filter, mask, maskScope));
    }

    private void CompositeLayer(int layer, Rect bounds, Filter filter, Mask? mask, float opacity)
    {
        var context = ActiveContext;
        var output = context.Batcher;
        output.Flush();
        var target = output.Destination;
        var transform = DrawingTransform(context) * target.Transform;
        var scissor = output.GetOrAddDrawClip(context.State.ClipIndex, target.Transform, output.GetOrAddScissor(target.Scissor));
        filter = filter with { Tint = filter.Tint.WithAlphaMultiplied(context.State.Opacity * context.InheritedOpacity * opacity) };
        var placement = new QuadPlacement(bounds.Grow(MathF.Ceiling(filter.Blur * 3)), transform, target.LayerMatrix, scissor, context.State.OverrideBlendMode);
        output.List.Commands.Add(new FilterCommand(layer, placement, bounds, filter, mask, MaskScope.Default));
    }
}
