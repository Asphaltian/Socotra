namespace Socotra;

/// <summary>
/// One frame of drawing, ready to hand to a renderer. Fill it with <see cref="RootPanel.Paint"/> or
/// <see cref="Painter.Begin(DrawList, Rect)"/>. Keep one per window and fill it again each frame.
/// </summary>
/// <example>
/// <code>
/// var list = new DrawList();
///
/// // every frame, after drawing your scene into the image; the UI draws over it
/// root.Update(new Rect(0, 0, width, height), deltaTime);
/// root.Paint(list);
/// renderer.Record(commandBuffer, list, target, ImageLayout.ColorAttachmentOptimal, ImageLayout.PresentSrcKhr);
/// </code>
/// </example>
public sealed class DrawList
{
    private readonly Dictionary<Texture, int> _textureIndices = [];

    /// <summary>Makes an empty draw list.</summary>
    public DrawList()
    {
        Batcher = new PainterBatcher(this);
        PainterContext = new Painter.Context(Batcher);
    }

    /// <summary>The size of the area that was drawn, in pixels.</summary>
    public Vector2 Size { get; private set; }

    internal PainterBatcher Batcher { get; }

    internal Painter.Context PainterContext { get; }

    internal List<DrawCommand> Commands { get; } = [];

    internal List<Texture> Textures { get; } = [];

    internal int LayerCount { get; private set; }

    internal void Reset(Vector2 size)
    {
        Size = size;
        Commands.Clear();
        Textures.Clear();
        _textureIndices.Clear();
        LayerCount = 0;
        PainterContext.Reset();
        Batcher.Clear();
    }

    internal int TextureIndex(Texture texture)
    {
        if (!_textureIndices.TryGetValue(texture, out var index))
        {
            Textures.Add(texture);
            index = Textures.Count;
            _textureIndices[texture] = index;
        }

        return index;
    }

    internal int NextLayer() => LayerCount++;
}

internal abstract record DrawCommand;

internal sealed record ClearCommand(Color Color) : DrawCommand;

internal sealed record BoxesCommand(int Offset, int Count, BlendMode Blend, Matrix4x4 LayerMatrix) : DrawCommand;

internal sealed record BeginLayerCommand(int Layer, int Width, int Height) : DrawCommand;

internal sealed record EndLayerCommand : DrawCommand;

internal readonly record struct QuadPlacement(Rect Quad, Matrix4x4 Transform, Matrix4x4 LayerMatrix, int Scissor, BlendMode Blend);

internal sealed record FilterCommand(int Layer, QuadPlacement Placement, Rect Box, Painter.Filter Filter, Painter.Mask? Mask, MaskScope MaskScope) : DrawCommand;

internal sealed record DropShadowCommand(int Layer, QuadPlacement Placement, Rect Box, Vector2 Offset, float Blur, Color Color) : DrawCommand;

internal sealed record BorderWrapCommand(int Layer, QuadPlacement Placement, Rect Box, float Width, Color Color) : DrawCommand;

internal sealed record BackdropCommand(QuadPlacement Placement, Rect Box, BorderRadii Radii, Painter.Filter Filter, Color Tint, bool ReuseGrab) : DrawCommand;
