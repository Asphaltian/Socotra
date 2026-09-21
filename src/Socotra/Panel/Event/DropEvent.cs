namespace Socotra;

/// <summary>What happens to something dropped on a panel.</summary>
public enum DropAction
{
    /// <summary>The drop is rejected.</summary>
    None,

    /// <summary>The payload would be copied.</summary>
    Copy,

    /// <summary>The payload would be moved.</summary>
    Move,
}

/// <summary>
/// Files or text dragged in from outside the app, like from the desktop. Handle it in an <c>ondrop</c> listener: while
/// <see cref="IsDrop"/> is false it's still hovering, so set <see cref="Action"/> to say whether you'd take it. When
/// <see cref="IsDrop"/> is true, use <see cref="Files"/> or <see cref="Text"/>.
/// </summary>
/// <example>
/// <code>
/// panel.AddEventListener("ondrop", e =>
/// {
///     var drop = (DropEvent)e;
///     if (!drop.Files.Any(f => f.EndsWith(".png")))
///         return; // leave Action at None to refuse it
///
///     drop.Action = DropAction.Copy;
///     if (drop.IsDrop)
///         LoadImages(drop.Files);
/// });
/// </code>
/// </example>
public class DropEvent : PanelEvent
{
    /// <summary>Makes an <c>ondrop</c> event on <paramref name="target"/>.</summary>
    public DropEvent(Panel target)
        : base("ondrop", target)
    {
    }

    internal DropEvent(Panel target, string name)
        : base(name, target)
    {
    }

    /// <summary>Full paths of the dragged files. Empty when text is being dragged.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>The dragged text, or null when files are being dragged.</summary>
    public string? Text { get; init; }

    /// <summary>Where the drag is, in the same pixels as <see cref="RootPanel.Bounds"/>.</summary>
    public Vector2 Position { get; init; }

    /// <summary>False while the drag is still hovering: look at the payload and set <see cref="Action"/> to answer. True when it lands.</summary>
    public bool IsDrop { get; init; }

    /// <summary>What dropping here would do. Leave it at <see cref="DropAction.None"/> to reject the drop.</summary>
    public DropAction Action { get; set; }
}
