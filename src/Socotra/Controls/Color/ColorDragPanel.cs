namespace Socotra;

internal abstract class ColorDragPanel : Panel
{
    private bool _dragging;

    public Action? DragEnded { get; set; }

    protected abstract void OnDrag(Vector2 fraction);

    protected override void OnMouseDown(MousePanelEvent e)
    {
        base.OnMouseDown(e);
        if (e.Button != "mouseleft")
        {
            return;
        }

        FindRootPanel()?.Focused?.Blur();
        _dragging = true;
        Drag(e);
        e.StopPropagation();
    }

    protected override void OnMouseMove(MousePanelEvent e)
    {
        base.OnMouseMove(e);
        if (!_dragging || !HasActive)
        {
            return;
        }

        Drag(e);
        e.StopPropagation();
    }

    protected override void OnMouseUp(MousePanelEvent e)
    {
        base.OnMouseUp(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        DragEnded?.Invoke();
        e.StopPropagation();
    }

    private void Drag(MousePanelEvent e)
    {
        var rect = Box.Rect;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var fraction = new Vector2(e.LocalPosition.X / rect.Width, e.LocalPosition.Y / rect.Height);
        OnDrag(Vector2.Clamp(fraction, Vector2.Zero, Vector2.One));
    }
}
