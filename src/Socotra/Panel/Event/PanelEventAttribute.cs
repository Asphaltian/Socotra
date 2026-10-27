namespace Socotra;

/// <summary>
/// Put this on a method of your panel to make it listen for an event. If you don't give it a name, the event is the
/// method's name in lower case, without any <c>Event</c> at the end. The method can take no arguments, the
/// <see cref="PanelEvent"/>, or the event's <see cref="PanelEvent.Value"/> converted to its parameter type. Returning
/// <c>false</c> stops the event right there.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class PanelEventAttribute(string? name = null) : Attribute
{
    /// <summary>The name of the event to listen for.</summary>
    public string? Name { get; set; } = name;
}
