namespace Socotra;

internal sealed class FloatingWindowTitleBar : Panel
{
    private FloatingWindow? _window;
    private bool _leftPressed;

    public FloatingWindowTitleBar()
    {
        AddClass("floating-window-titlebar");
        CanDragScroll = false;
    }

    public override bool WantsDrag => true;

    public override void OnDeleted()
    {
        _window?.EndMove(true);
        base.OnDeleted();
    }

    protected override void OnMouseDown(MousePanelEvent e)
    {
        _window = Ancestors.OfType<FloatingWindow>().FirstOrDefault();
        _window?.BringToFront();
        _leftPressed = e.Button == "mouseleft";
        for (var panel = e.Target; panel is not null && panel != this; panel = panel.Parent)
        {
            if (panel.HasClass("floating-window-nodrag") || panel is Button)
            {
                _leftPressed = false;
            }
        }

        if (!_leftPressed)
        {
            e.StopPropagation();
        }
    }

    protected override void OnDragStart(DragEvent e)
    {
        e.StopPropagation();
        if (_leftPressed && _window is { IsDeleted: false } window)
        {
            window.BeginMove(e.ScreenPosition - e.ScreenGrabPosition);
        }
    }

    protected override void OnDrag(DragEvent e)
    {
        e.StopPropagation();
        if (_window is { IsDeleted: false } window)
        {
            window.Move(e.ScreenPosition - e.ScreenGrabPosition);
        }
    }

    protected override void OnDragEnd(DragEvent e)
    {
        e.StopPropagation();
        if (_window is { IsDeleted: false } window)
        {
            window.Move(e.ScreenPosition - e.ScreenGrabPosition);
            window.EndMove(false);
        }
    }

    protected override void OnDragCancel(DragEvent e)
    {
        e.StopPropagation();
        if (_window is { IsDeleted: false } window)
        {
            window.EndMove(true);
        }
    }
}
