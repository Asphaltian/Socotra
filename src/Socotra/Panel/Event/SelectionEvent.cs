namespace Socotra;

/// <summary>Sent as <c>ondragselect</c> to the panel a mouse drag started on, as the drag selects text.</summary>
public class SelectionEvent : PanelEvent
{
    /// <summary>Makes a selection event on <paramref name="active"/>.</summary>
    public SelectionEvent(string eventName, Panel active)
        : base(eventName, active)
    {
    }

    /// <summary>The rectangle between <see cref="StartPoint"/> and <see cref="EndPoint"/>, on screen.</summary>
    public Rect SelectionRect { get; set; }

    /// <summary>Where the drag started, on screen.</summary>
    public Vector2 StartPoint { get; set; }

    /// <summary>Where the drag is now, on screen.</summary>
    public Vector2 EndPoint { get; set; }
}
