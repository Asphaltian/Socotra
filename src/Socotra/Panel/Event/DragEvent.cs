namespace Socotra;

/// <summary>
/// Sent while a panel is dragged with the mouse: <c>ondragstart</c> once it moves far enough,
/// <c>ondrag</c> as it moves, and <c>ondragend</c> when it's let go.
/// </summary>
public class DragEvent : PanelEvent
{
    /// <summary>Makes a drag event on <paramref name="active"/> for a drag that started at the given positions.</summary>
    public DragEvent(string eventName, Panel active, Vector2 localDragStart, Vector2 globalDragStart)
        : base(eventName, active)
    {
        LocalGrabPosition = localDragStart;
        ScreenGrabPosition = globalDragStart;
        LocalPosition = active.MousePosition + active.ScrollOffset;
        ScreenPosition = active.ScreenMousePosition;
    }

    /// <summary>For <c>ondrag</c>, how far the mouse moved since the last one.</summary>
    public Vector2 MouseDelta { get; set; }

    /// <summary>Where on the target the drag started, including its scroll offset.</summary>
    public Vector2 LocalGrabPosition { get; set; }

    /// <summary>Where on screen the drag started.</summary>
    public Vector2 ScreenGrabPosition { get; set; }

    /// <summary>Where the mouse is now, relative to the target and including its scroll offset.</summary>
    public Vector2 LocalPosition { get; set; }

    /// <summary>Where the mouse is now, on screen.</summary>
    public Vector2 ScreenPosition { get; set; }
}
