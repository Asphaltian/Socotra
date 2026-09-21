namespace Socotra;

public partial class RootPanel
{
    /// <summary>
    /// Draws the UI into <paramref name="list"/>, replacing what it held, ready to hand to a renderer. Call it once a frame,
    /// after <see cref="Update"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// root.Update(new Rect(0, 0, width, height), deltaTime);
    /// root.Paint(drawList);
    /// // then give drawList to your renderer, like VulkanRenderer.Record
    /// </code>
    /// </example>
    public void Paint(DrawList list)
    {
        PushRootValues();
        using var painter = Painter.Begin(list, Bounds);
        painter.SetViewport(Bounds);
        using (var destination = painter.WithDestination(Bounds, ScaleToScreen, 1, BlendMode.Normal, Matrix4x4.Identity))
        {
            Render(destination.Painter);
            foreach (var overlay in FixedOverlays)
            {
                using var scope = destination.Painter.WithDestination(Bounds, ScaleToScreen, overlay.Parent?.Opacity ?? 1, BlendMode.Normal, Matrix4x4.Identity);
                overlay.RenderShadow(scope.Painter);
                overlay.Render(scope.Painter);
            }
        }

        painter.Flush();
    }
}
