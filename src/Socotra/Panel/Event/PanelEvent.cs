namespace Socotra;

/// <summary>
/// An event traveling up the panel tree, like a click. Send one with <see cref="Panel.CreateEvent(PanelEvent)"/>
/// and listen for it with <see cref="Panel.AddEventListener(string, Action{PanelEvent})"/>.
/// </summary>
public class PanelEvent
{
    /// <summary>Makes an event called <paramref name="eventName"/>, starting on <paramref name="active"/>.</summary>
    public PanelEvent(string eventName, Panel? active = null)
    {
        Name = eventName;
        Target = active;
    }

    /// <summary>The event's name, like <c>onclick</c>.</summary>
    public string Name { get; init; }

    /// <summary>A value that came with the event, like the new value for <c>onchange</c>.</summary>
    public object? Value { get; set; }

    /// <summary>When the event happens, in <see cref="RootPanel.Time"/> seconds.</summary>
    public float Time { get; set; }

    /// <summary>The button involved, like <c>mouseleft</c>, if any.</summary>
    public string? Button { get; set; }

    /// <summary>
    /// The panel handling the event right now. When a click on a label inside a button bubbles up to the
    /// button, <see cref="This"/> is the button and <see cref="Target"/> is still the label.
    /// </summary>
    public Panel? This { get; set; }

    /// <summary>The panel the event started on.</summary>
    public Panel? Target { get; set; }

    internal bool Propagate { get; set; } = true;

    /// <summary>Whether the event is called <paramref name="name"/>, ignoring case.</summary>
    public bool Is(string name) => string.Equals(name, Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>Stops the event reaching any more listeners or ancestors.</summary>
    public void StopPropagation() => Propagate = false;
}
