namespace Socotra;

/// <summary>Quick ways to add common child panels, through <see cref="Panel.Add"/>. Write extension methods on it to add your own.</summary>
/// <example>
/// <code>
/// // Add a title label and an image under this panel
/// panel.Add.Label("Settings", "title");
/// panel.Add.Image("assets/ui/icon.png", "icon");
/// </code>
/// </example>
public readonly ref struct PanelCreator
{
    /// <summary>The panel new children go under.</summary>
    public readonly Panel panel;

    internal PanelCreator(Panel panel)
    {
        this.panel = panel;
    }

    /// <summary>Adds an empty panel with the classes in <paramref name="classname"/>.</summary>
    public Panel Panel(string? classname = null) => panel.AddChild<Panel>(classname);
}
