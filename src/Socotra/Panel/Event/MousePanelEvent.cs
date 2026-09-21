namespace Socotra;

/// <summary>A mouse event, like <c>onclick</c> or <c>onmousedown</c>.</summary>
public class MousePanelEvent : PanelEvent
{
    /// <summary>Makes a mouse event on <paramref name="active"/>, for a button named like <c>mouseleft</c>, <c>mouseright</c> or <c>mousemiddle</c>.</summary>
    public MousePanelEvent(string eventName, Panel active, string button)
        : base(eventName, active)
    {
        LocalPosition = active.MousePosition;
        Button = button;
        MouseButton = button switch
        {
            "mouseleft" => MouseButtons.Left,
            "mouseright" => MouseButtons.Right,
            "mousemiddle" => MouseButtons.Middle,
            _ => MouseButtons.None,
        };
    }

    /// <summary>Where the mouse was, relative to the target's top left corner, when the event happened.</summary>
    public Vector2 LocalPosition { get; set; }

    /// <summary>How many clicks in quick succession this press is part of. 2 for the second press of a double click.</summary>
    public int ClickCount { get; set; } = 1;

    /// <summary>The button involved.</summary>
    public MouseButtons MouseButton { get; set; }

    /// <summary>The modifier keys held at the time, so a shift-click can do something different to a plain click.</summary>
    public KeyboardModifiers KeyboardModifiers { get; set; }

    /// <summary>Whether Shift was held.</summary>
    public bool HasShift => (KeyboardModifiers & KeyboardModifiers.Shift) != 0;

    /// <summary>Whether Ctrl was held.</summary>
    public bool HasCtrl => (KeyboardModifiers & KeyboardModifiers.Ctrl) != 0;

    /// <summary>Whether Alt was held.</summary>
    public bool HasAlt => (KeyboardModifiers & KeyboardModifiers.Alt) != 0;
}
