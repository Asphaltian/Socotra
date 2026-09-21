namespace Socotra;

/// <summary>
/// Makes a method on a panel listen for an event. Without a name the event is the method's name in lower
/// case, minus any <c>Event</c> ending. The method can take no arguments, the <see cref="PanelEvent"/>, or
/// the event's <see cref="PanelEvent.Value"/> converted to its parameter type. Returning false stops the event there.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PanelEventAttribute(string? name = null) : Attribute
{
    /// <summary>The name of the event to listen for.</summary>
    public string? Name { get; set; } = name;
}
