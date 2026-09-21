namespace Socotra;

/// <summary>
/// Files or text dragged out of your UI, to drop on other panels, other windows, other apps or the desktop. Fill it in,
/// then call <see cref="Start"/>. The drag itself runs through your platform: set <see cref="StartHandler"/> once at startup.
/// </summary>
/// <example>
/// <code>
/// // At startup, run drags with your platform's drag and drop
/// Drag.StartHandler = drag =&gt; RunPlatformDrag(drag.Files, drag.Text);
///
/// // Then, from a panel the user is dragging
/// var drag = new Drag(this);
/// drag.SetFile(path);
/// if (drag.Start() == DropAction.Move)
///     RemoveItem();
/// </code>
/// </example>
public class Drag(Panel panel)
{
    private readonly List<string> _files = [];

    /// <summary>
    /// Set this to a function that runs a drag on your platform until the user lets go, and returns what the receiver did
    /// with it. Drops that land on your own windows should reach them through <see cref="RootPanel.DragOver"/> and
    /// <see cref="RootPanel.Drop"/> as usual. Until you set it, <see cref="Start"/> does nothing.
    /// </summary>
    public static Func<Drag, DropAction>? StartHandler { get; set; }

    /// <summary>The panel the drag comes from.</summary>
    public Panel Panel => panel;

    /// <summary>The files the drag carries.</summary>
    public IReadOnlyList<string> Files => _files;

    /// <summary>The text the drag carries, or null.</summary>
    public string? Text { get; private set; }

    /// <summary>Carries this file. Call it again to carry more than one.</summary>
    public void SetFile(string path) => _files.Add(path);

    /// <summary>Carries this text.</summary>
    public void SetText(string text) => Text = text;

    /// <summary>
    /// Starts the drag. It returns once the user lets go, with what the receiver did. That's <see cref="DropAction.None"/>
    /// when the drag was canceled, carries nothing, or there's no <see cref="StartHandler"/>.
    /// </summary>
    public DropAction Start()
    {
        if (_files.Count == 0 && string.IsNullOrEmpty(Text))
        {
            return DropAction.None;
        }

        return StartHandler?.Invoke(this) ?? DropAction.None;
    }
}
